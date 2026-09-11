using System;
using System.Configuration;
using System.IO;

namespace ETCS.Pos.Bridge.Configuration;

public enum ReceiptPrintMode
{
    Print,
    Preview,
    PrintAndPreview
}

public static class BridgeSettings
{
    public static ReceiptPrintMode ReceiptPrintMode
    {
        get
        {
            var raw = ConfigurationManager.AppSettings["ReceiptPrintMode"];
            if (Enum.TryParse(raw, true, out ReceiptPrintMode mode))
            {
                return mode;
            }

            return ReceiptPrintMode.Print;
        }
    }

    public static bool ReceiptPreviewOpenViewer
    {
        get
        {
            var raw = ConfigurationManager.AppSettings["ReceiptPreviewOpenViewer"];
            return !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase);
        }
    }

    public static string ReceiptPreviewFolder
    {
        get
        {
            var raw = ConfigurationManager.AppSettings["ReceiptPreviewFolder"];
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
        }
    }

    public static string ResolvePreviewFolder()
    {
        var configured = ReceiptPreviewFolder;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        return Path.Combine(Path.GetTempPath(), "ETCS", "ReceiptPreview");
    }
}
