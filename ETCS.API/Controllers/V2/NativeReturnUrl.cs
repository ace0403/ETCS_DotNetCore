using ETCS.PaymentGateway.Options;

namespace ETCS.API.Controllers.V2;

internal static class NativeReturnUrl
{
    private const string ReturnPath = "/api/v2/Payment/mobile-return";

    public static bool TryResolve(HttpRequest request, string? supplied, PaymentGatewayOptions options, out string template)
    {
        template = string.Empty;
        var value = string.IsNullOrWhiteSpace(supplied) ? options.NativeReturnBaseUrl : supplied;
        if (!TryParse(value, out var uri)) return false;

        // The public HTTPS endpoint can reach Kestrel over HTTP after TLS termination.
        // Validate the return URI's HTTPS scheme, rather than the proxy-to-API scheme.
        var sameApi = uri!.IdnHost.Equals(request.Host.Host.Trim('[', ']'), StringComparison.OrdinalIgnoreCase)
            && uri.Port == (request.Host.Port ?? 443);
        // When a tunnel rewrites Host to localhost, trust the explicitly configured
        // public return origin. Raw forwarded headers are not an origin allowlist.
        var configuredOrigin = TryParse(options.NativeReturnBaseUrl, out var configured)
            && configured!.Authority.Equals(uri.Authority, StringComparison.OrdinalIgnoreCase);
        if (!sameApi && !configuredOrigin) return false;
        template = uri.GetLeftPart(UriPartial.Authority) + ReturnPath + "?orderid={0}";
        return true;
    }

    private static bool TryParse(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        // Also accept the encoded placeholder emitted by older app builds.
        value = value.Trim().Replace("%7B0%7D", "{0}", StringComparison.OrdinalIgnoreCase);
        return Uri.TryCreate(value.Replace("{0}", "order"), UriKind.Absolute, out uri)
            && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
            && string.IsNullOrEmpty(uri.Fragment)
            && uri.AbsolutePath.Equals(ReturnPath, StringComparison.OrdinalIgnoreCase);
    }
}
