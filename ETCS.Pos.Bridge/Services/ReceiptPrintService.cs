using System;
using System.Drawing;
using System.Drawing.Printing;
using ETCS.Pos.Bridge.Models;

namespace ETCS.Pos.Bridge.Services;

public sealed class ReceiptPrintService
{
    private ReceiptPrintRequest? _request;

    public void Print(ReceiptPrintRequest request)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));

        using var document = new PrintDocument();
        document.PrintPage += OnPrintPage;
        document.Print();
    }

    private void OnPrintPage(object sender, PrintPageEventArgs e)
    {
        if (_request is null || e.Graphics is null)
        {
            return;
        }

        var graphics = e.Graphics;
        using var regular = new Font(FontFamily.GenericSansSerif, 7f, FontStyle.Regular);
        using var bold = new Font(FontFamily.GenericSansSerif, 7f, FontStyle.Bold);

        var y = 20;
        graphics.DrawString("        " + _request.HeaderLine1 + "         ", bold, Brushes.Black, 20, y);
        y += 10;
        graphics.DrawString("                  " + _request.HeaderLine2 + "            ", bold, Brushes.Black, 20, y);
        y += 70;
        graphics.DrawString(DateTime.Now.ToString(), bold, Brushes.Black, 20, y);
        y += 35;
        var docTitle = _request.IsUndo ? "                    UNDO RECEIPT                 " : "                    TAX INVOICE                 ";
        graphics.DrawString(docTitle, bold, Brushes.Black, 20, y);
        y += 15;
        graphics.DrawString("Item Name", bold, Brushes.Black, 20, y);
        graphics.DrawString("Price", bold, Brushes.Black, 150, y);
        y += 10;

        decimal total = 0;
        foreach (var item in _request.Items)
        {
            var lineTotal = item.Price * item.Quantity;
            if (_request.DiscountApplied && _request.DiscountPercent > 0)
            {
                lineTotal -= lineTotal * _request.DiscountPercent / 100m;
            }

            graphics.DrawString(item.Name, regular, Brushes.Black, 20, y);
            graphics.DrawString(lineTotal.ToString("N2"), regular, Brushes.Black, 150, y);
            total += lineTotal;
            y += 20;
        }

        if (_request.DiscountApplied && _request.DiscountPercent > 0)
        {
            graphics.DrawString("Discount Applied: " + _request.DiscountPercent + "%", regular, Brushes.Black, 20, y);
            y += 20;
        }

        if (_request.Total > 0)
        {
            total = _request.Total;
        }

        y += 10;
        graphics.DrawString("TOTAL AMOUNT", regular, Brushes.Black, 50, y);
        graphics.DrawString(total.ToString("N2"), regular, Brushes.Black, 150, y);
        y += 30;

        graphics.DrawString("               VAT SUMMARY                     ", regular, Brushes.Black, 30, y);
        y += 20;
        graphics.DrawString("RATE", regular, Brushes.Black, 30, y);
        graphics.DrawString("NET", regular, Brushes.Black, 80, y);
        graphics.DrawString("VAT", regular, Brushes.Black, 110, y);
        graphics.DrawString("TOTAL", regular, Brushes.Black, 140, y);
        y += 20;

        var vat = _request.VatPercent;
        var net = total - total * vat / 100m;
        var vatAmount = total * vat / 100m;
        graphics.DrawString(vat + " % ", regular, Brushes.Black, 30, y);
        graphics.DrawString(net.ToString("N2"), regular, Brushes.Black, 80, y);
        graphics.DrawString(vatAmount.ToString("N2"), regular, Brushes.Black, 110, y);
        graphics.DrawString(total.ToString("N2"), regular, Brushes.Black, 140, y);
        y += 20;
        graphics.DrawString("    Thank You! Visit Again. ", regular, Brushes.Black, 30, y);

        e.HasMorePages = false;
    }
}
