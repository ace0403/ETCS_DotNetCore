using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using ETCS.Pos.Bridge.Configuration;
using ETCS.Pos.Bridge.Models;

namespace ETCS.Pos.Bridge.Services;

public sealed class ReceiptPrintService
{
    private const float RegularFontSize = 8f;
    private const float BoldFontSize = 8f;
    private const float TitleFontSize = 9f;
    private const float LogoMaxWidth = 90f;
    private const float LineSpacing = 4f;
    private const float SectionSpacing = 8f;
    private const float PageWidth = 280f;
    private const float MaxRenderHeight = 2400f;

    private ReceiptPrintRequest? _request;
    private Font? _regular;
    private Font? _bold;
    private Font? _title;
    private float _left;
    private float _right;
    private float _contentWidth;
    private float _y;

    public ReceiptPrintResult Print(ReceiptPrintRequest request)
    {
        request = NormalizeRequest(request ?? throw new ArgumentNullException(nameof(request)));
        var mode = BridgeSettings.ReceiptPrintMode;
        var result = new ReceiptPrintResult { IsSuccess = true };

        if (mode == ReceiptPrintMode.Preview || mode == ReceiptPrintMode.PrintAndPreview)
        {
            var preview = SavePreview(request);
            result.PreviewImageBase64 = preview.Base64;
            result.PreviewFilePath = preview.FilePath;
        }

        if (mode == ReceiptPrintMode.Print || mode == ReceiptPrintMode.PrintAndPreview)
        {
            try
            {
                SendToPrinter(request);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.Message = ex.Message;
                return result;
            }
        }

        result.Message = mode switch
        {
            ReceiptPrintMode.Preview => string.IsNullOrWhiteSpace(result.PreviewFilePath)
                ? "Receipt preview generated."
                : "Receipt preview saved to " + result.PreviewFilePath,
            ReceiptPrintMode.PrintAndPreview => "Receipt sent to printer and preview saved.",
            _ => "Receipt sent to printer."
        };

        return result;
    }

    public ReceiptPrintResult Preview(ReceiptPrintRequest request)
    {
        request = NormalizeRequest(request ?? throw new ArgumentNullException(nameof(request)));
        var preview = SavePreview(request);

        return new ReceiptPrintResult
        {
            IsSuccess = true,
            Message = string.IsNullOrWhiteSpace(preview.FilePath)
                ? "Receipt preview generated."
                : "Receipt preview saved to " + preview.FilePath,
            PreviewImageBase64 = preview.Base64,
            PreviewFilePath = preview.FilePath
        };
    }

    private void SendToPrinter(ReceiptPrintRequest request)
    {
        _request = request;

        using var document = new PrintDocument();
        document.PrintPage += OnPrintPage;
        document.Print();
    }

    private (string Base64, string? FilePath) SavePreview(ReceiptPrintRequest request)
    {
        var pngBytes = RenderToPng(request);
        var base64 = "data:image/png;base64," + Convert.ToBase64String(pngBytes);

        var folder = BridgeSettings.ResolvePreviewFolder();
        Directory.CreateDirectory(folder);
        var filePath = Path.Combine(folder, "receipt_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".png");
        File.WriteAllBytes(filePath, pngBytes);

        if (BridgeSettings.ReceiptPreviewOpenViewer)
        {
            try
            {
                Process.Start(filePath);
            }
            catch
            {
                // Viewer launch is best-effort; preview file is still saved.
            }
        }

        return (base64, filePath);
    }

    public byte[] RenderToPng(ReceiptPrintRequest request)
    {
        using var scratch = new Bitmap((int)PageWidth, (int)MaxRenderHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(scratch))
        {
            graphics.Clear(Color.White);
            RenderLayout(graphics, request, PageWidth);
        }

        var pageWidthPx = (int)PageWidth;
        var height = (int)Math.Min(MaxRenderHeight, Math.Ceiling(_y + 16f));
        using var output = new Bitmap(pageWidthPx, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(output))
        {
            graphics.DrawImage(
                scratch,
                new Rectangle(0, 0, pageWidthPx, height),
                new Rectangle(0, 0, pageWidthPx, height),
                GraphicsUnit.Pixel);
        }

        using var stream = new MemoryStream();
        output.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private void OnPrintPage(object sender, PrintPageEventArgs e)
    {
        if (_request is null || e.Graphics is null)
        {
            return;
        }

        var pageWidth = e.PageBounds.Width > 0 ? e.PageBounds.Width : PageWidth;
        RenderLayout(e.Graphics, _request, pageWidth);
        e.HasMorePages = false;
    }

    private void RenderLayout(Graphics graphics, ReceiptPrintRequest request, float pageWidth)
    {
        _request = request;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        _regular = CreateReceiptFont(RegularFontSize, FontStyle.Regular);
        _bold = CreateReceiptFont(BoldFontSize, FontStyle.Bold);
        _title = CreateReceiptFont(TitleFontSize, FontStyle.Bold);

        try
        {
            _left = 12f;
            _right = pageWidth - 12f;
            _contentWidth = _right - _left;
            _y = 10f;

            DrawLogo(graphics);
            DrawCenteredLine(graphics, _request.CompanyLine, _bold);
            if (!string.IsNullOrWhiteSpace(_request.VatRegNoLine))
            {
                DrawCenteredLine(graphics, _request.VatRegNoLine.Trim(), _bold);
            }

            if (!string.IsNullOrWhiteSpace(_request.LocationLine))
            {
                DrawCenteredLine(graphics, _request.LocationLine.Trim(), _regular);
            }

            _y += SectionSpacing;
            var docTitle = _request.IsUndo ? "UNDO RECEIPT" : "TAX INVOICE";
            DrawCenteredLine(graphics, docTitle, _title);
            _y += LineSpacing;

            var printedAt = _request.PrintedAt ?? DateTime.Now;
            DrawLeftLine(graphics, printedAt.ToString("dd/MM/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture), _regular);
            if (!string.IsNullOrWhiteSpace(_request.TerminalLine))
            {
                DrawLeftLine(graphics, "Terminal: " + _request.TerminalLine.Trim(), _regular);
            }

            DrawDashedRule(graphics);

            DrawItemHeader(graphics);
            DrawDashedRule(graphics);

            var total = DrawItems(graphics);
            DrawDashedRule(graphics);

            DrawTotal(graphics, total);
            DrawDashedRule(graphics);

            DrawVatSummary(graphics, total);
            DrawDashedRule(graphics);

            DrawCenteredLine(graphics, "Thank You!", _regular);
            DrawCenteredLine(graphics, "Visit Again.", _regular);
        }
        finally
        {
            _regular?.Dispose();
            _bold?.Dispose();
            _title?.Dispose();
            _regular = null;
            _bold = null;
            _title = null;
        }
    }

    private void DrawLogo(Graphics graphics)
    {
        if (string.IsNullOrWhiteSpace(_request!.LogoBase64))
        {
            return;
        }

        try
        {
            var bytes = DecodeBase64Image(_request.LogoBase64);
            if (bytes.Length == 0)
            {
                return;
            }

            using var stream = new MemoryStream(bytes);
            using var image = Image.FromStream(stream);
            var scale = Math.Min(1f, LogoMaxWidth / image.Width);
            var width = image.Width * scale;
            var height = image.Height * scale;
            var x = _left + ((_contentWidth - width) / 2f);
            graphics.DrawImage(image, x, _y, width, height);
            _y += height + SectionSpacing;
        }
        catch
        {
            // Skip invalid logo data and continue printing text layout.
        }
    }

    private static byte[] DecodeBase64Image(string value)
    {
        var trimmed = value.Trim();
        var commaIndex = trimmed.IndexOf(',');
        if (commaIndex >= 0)
        {
            trimmed = trimmed.Substring(commaIndex + 1);
        }

        return Convert.FromBase64String(trimmed);
    }

    private decimal DrawItems(Graphics graphics)
    {
        decimal total = 0;
        var priceColumnWidth = 52f;
        var nameColumnWidth = _contentWidth - priceColumnWidth - 4f;

        foreach (var item in _request!.Items)
        {
            var lineTotal = item.Price * item.Quantity;
            if (_request.DiscountApplied && _request.DiscountPercent > 0)
            {
                lineTotal -= lineTotal * _request.DiscountPercent / 100m;
            }

            var nameHeight = DrawWrappedText(
                graphics,
                item.Name,
                _regular!,
                _left,
                _y,
                nameColumnWidth);

            DrawRightAlignedText(
                graphics,
                lineTotal.ToString("N2", CultureInfo.InvariantCulture),
                _regular!,
                _right - priceColumnWidth,
                _y,
                priceColumnWidth);

            var rowHeight = Math.Max(nameHeight, _regular!.GetHeight(graphics));
            _y += rowHeight + LineSpacing;
            total += lineTotal;
        }

        if (_request.DiscountApplied && _request.DiscountPercent > 0)
        {
            DrawLeftLine(graphics, "Discount Applied: " + _request.DiscountPercent.ToString("0.##", CultureInfo.InvariantCulture) + "%", _regular!);
        }

        if (_request.Total > 0)
        {
            total = _request.Total;
        }

        return total;
    }

    private void DrawItemHeader(Graphics graphics)
    {
        graphics.DrawString("Item Name", _bold!, Brushes.Black, _left, _y);
        DrawRightAlignedText(graphics, "Price", _bold!, _right - 52f, _y, 52f);
        _y += _bold!.GetHeight(graphics) + LineSpacing;
    }

    private void DrawTotal(Graphics graphics, decimal total)
    {
        var priceColumnWidth = 52f;
        graphics.DrawString("TOTAL AMOUNT", _bold!, Brushes.Black, _left, _y);
        DrawRightAlignedText(
            graphics,
            total.ToString("N2", CultureInfo.InvariantCulture),
            _bold!,
            _right - priceColumnWidth,
            _y,
            priceColumnWidth);
        _y += _bold!.GetHeight(graphics) + SectionSpacing;
    }

    private void DrawVatSummary(Graphics graphics, decimal total)
    {
        DrawCenteredLine(graphics, "VAT SUMMARY", _bold!);
        _y += LineSpacing;

        var columnWidth = _contentWidth / 4f;
        var headers = new[] { "RATE", "NET", "VAT", "TOTAL" };
        for (var i = 0; i < headers.Length; i++)
        {
            var x = _left + (columnWidth * i);
            graphics.DrawString(headers[i], _regular!, Brushes.Black, x, _y);
        }

        _y += _regular!.GetHeight(graphics) + LineSpacing;

        var vat = _request!.VatPercent;
        var net = total - total * vat / 100m;
        var vatAmount = total * vat / 100m;
        var values = new[]
        {
            vat.ToString("0.##", CultureInfo.InvariantCulture) + " %",
            net.ToString("N2", CultureInfo.InvariantCulture),
            vatAmount.ToString("N2", CultureInfo.InvariantCulture),
            total.ToString("N2", CultureInfo.InvariantCulture)
        };

        for (var i = 0; i < values.Length; i++)
        {
            var x = _left + (columnWidth * i);
            graphics.DrawString(values[i], _regular!, Brushes.Black, x, _y);
        }

        _y += _regular!.GetHeight(graphics) + SectionSpacing;
    }

    private void DrawDashedRule(Graphics graphics)
    {
        _y += LineSpacing;
        using var pen = new Pen(Color.Black, 1f) { DashStyle = DashStyle.Dash };
        graphics.DrawLine(pen, _left, _y, _right, _y);
        _y += SectionSpacing;
    }

    private void DrawCenteredLine(Graphics graphics, string text, Font font)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var size = graphics.MeasureString(text, font, (int)_contentWidth);
        var x = _left + ((_contentWidth - size.Width) / 2f);
        graphics.DrawString(text, font, Brushes.Black, x, _y);
        _y += size.Height + LineSpacing;
    }

    private void DrawLeftLine(Graphics graphics, string text, Font font)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        graphics.DrawString(text, font, Brushes.Black, _left, _y);
        _y += font.GetHeight(graphics) + LineSpacing;
    }

    private static float DrawWrappedText(Graphics graphics, string text, Font font, float x, float y, float width)
    {
        var rect = new RectangleF(x, y, width, 1000f);
        using var format = new StringFormat(StringFormatFlags.LineLimit);
        graphics.DrawString(text, font, Brushes.Black, rect, format);
        return graphics.MeasureString(text, font, (int)width, format).Height;
    }

    private static void DrawRightAlignedText(Graphics graphics, string text, Font font, float x, float y, float width)
    {
        using var format = new StringFormat { Alignment = StringAlignment.Far };
        var rect = new RectangleF(x, y, width, font.GetHeight(graphics) + 4f);
        graphics.DrawString(text, font, Brushes.Black, rect, format);
    }

    private static ReceiptPrintRequest NormalizeRequest(ReceiptPrintRequest request)
    {
        request.CompanyLine = ReceiptTextNormalizer.Normalize(request.CompanyLine);
        request.VatRegNoLine = ReceiptTextNormalizer.Normalize(request.VatRegNoLine);
        request.LocationLine = ReceiptTextNormalizer.Normalize(request.LocationLine);
        request.TerminalLine = ReceiptTextNormalizer.Normalize(request.TerminalLine);

        foreach (var item in request.Items)
        {
            item.Name = ReceiptTextNormalizer.Normalize(item.Name);
        }

        return request;
    }

    private static Font CreateReceiptFont(float size, FontStyle style)
    {
        foreach (var familyName in new[] { "Segoe UI", "Arial", "Tahoma" })
        {
            try
            {
                return new Font(familyName, size, style, GraphicsUnit.Point);
            }
            catch (ArgumentException)
            {
            }
        }

        return new Font(FontFamily.GenericSansSerif, size, style);
    }
}
