using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ETCS.Pos.Bridge.Services;

internal static class TerminalConnectionHelper
{
    public const int DefaultSoapPort = 18083;

    public static string NormalizeTerminalHost(string? raw, out int port)
    {
        port = DefaultSoapPort;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                value = uri.Host;
                if (uri.Port > 0)
                {
                    port = uri.Port;
                }
            }
        }

        var hostPortMatch = Regex.Match(value, @"^(?<host>[^:]+)(?::(?<port>\d+))?$");
        if (hostPortMatch.Success)
        {
            value = hostPortMatch.Groups["host"].Value.Trim();
            if (hostPortMatch.Groups["port"].Success
                && int.TryParse(hostPortMatch.Groups["port"].Value, out var parsedPort)
                && parsedPort > 0)
            {
                port = parsedPort;
            }
        }

        return value;
    }

    public static string BuildSoapUrl(string host, int port) => "http://" + host + ":" + port;

    public static bool IsPrivateLanAddress(string host)
    {
        if (!IPAddress.TryParse(host, out var ip))
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        if (bytes[0] == 10)
        {
            return true;
        }

        if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        {
            return true;
        }

        return bytes[0] == 192 && bytes[1] == 168;
    }

    public static TerminalReachabilityResult TestReachability(string terminalIp, int port = DefaultSoapPort, int timeoutMs = 3000)
    {
        var host = NormalizeTerminalHost(terminalIp, out var resolvedPort);
        if (string.IsNullOrWhiteSpace(host))
        {
            return TerminalReachabilityResult.Fail("Terminal IP is required.");
        }

        port = resolvedPort > 0 ? resolvedPort : DefaultSoapPort;
        var soapUrl = BuildSoapUrl(host, port);
        var details = new System.Collections.Generic.List<string>
        {
            "Host: " + host,
            "SOAP URL: " + soapUrl
        };

        if (!IsPrivateLanAddress(host)
            && !(IPAddress.TryParse(host, out var parsed) && IPAddress.IsLoopback(parsed)))
        {
            details.Add("Warning: IP is not a private LAN address (192.168.x.x / 10.x.x.x). Use the reader IP from the same local network as this PC, not a public/WAN address.");
        }

        try
        {
            using var ping = new Ping();
            var pingReply = ping.Send(host, timeoutMs);
            if (pingReply.Status != IPStatus.Success)
            {
                return TerminalReachabilityResult.Fail(
                    "Cannot ping reader at " + host + " (" + pingReply.Status + "). Ensure this PC is on the same network as the iBonus terminal.",
                    soapUrl,
                    details);
            }

            details.Add("Ping: OK (" + pingReply.RoundtripTime + " ms)");
        }
        catch (Exception ex)
        {
            return TerminalReachabilityResult.Fail(
                "Ping to reader failed: " + ex.Message,
                soapUrl,
                details);
        }

        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            if (!connectTask.Wait(timeoutMs))
            {
                return TerminalReachabilityResult.Fail(
                    "Cannot open TCP port " + port + " on " + host + ". Check reader power, cable/Wi‑Fi, firewall, and that port " + port + " is open on the device.",
                    soapUrl,
                    details);
            }

            details.Add("TCP " + port + ": OK");
        }
        catch (Exception ex)
        {
            return TerminalReachabilityResult.Fail(
                "Cannot connect to " + soapUrl + ". " + ex.Message + " Use the reader's local LAN IP (same subnet as this POS PC).",
                soapUrl,
                details);
        }

        return TerminalReachabilityResult.Ok(soapUrl, details);
    }
}

internal sealed class TerminalReachabilityResult
{
    public bool IsReachable { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SoapUrl { get; set; } = string.Empty;
    public System.Collections.Generic.IReadOnlyList<string> Details { get; set; } = System.Array.Empty<string>();

    public static TerminalReachabilityResult Ok(string soapUrl, System.Collections.Generic.IReadOnlyList<string> details) =>
        new TerminalReachabilityResult
        {
            IsReachable = true,
            Message = "Reader reachable at " + soapUrl,
            SoapUrl = soapUrl,
            Details = details
        };

    public static TerminalReachabilityResult Fail(
        string message,
        string soapUrl = "",
        System.Collections.Generic.IReadOnlyList<string>? details = null) =>
        new TerminalReachabilityResult
        {
            IsReachable = false,
            Message = message,
            SoapUrl = soapUrl,
            Details = details ?? System.Array.Empty<string>()
        };
}
