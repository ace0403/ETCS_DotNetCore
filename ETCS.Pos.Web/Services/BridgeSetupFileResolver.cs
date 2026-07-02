using ETCS.Pos.Web.Options;
using Microsoft.Extensions.Options;

namespace ETCS.Pos.Web.Services;

public sealed class BridgeSetupFileResolver : IBridgeSetupFileResolver
{
    public const string SetupFileName = "ETCS.Pos.Bridge.Setup.exe";

    private readonly PosWebOptions _options;
    private readonly IWebHostEnvironment _environment;
    private string? _resolvedPath;
    private bool _resolved;

    public BridgeSetupFileResolver(
        IOptions<PosWebOptions> options,
        IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public bool IsAvailable => Resolve() is not null;

    public string? Resolve()
    {
        if (_resolved)
        {
            return _resolvedPath;
        }

        _resolved = true;
        _resolvedPath = ResolveInternal();
        return _resolvedPath;
    }

    private string? ResolveInternal()
    {
        if (!string.IsNullOrWhiteSpace(_options.BridgeSetupPath))
        {
            var configuredPath = Path.IsPathRooted(_options.BridgeSetupPath)
                ? _options.BridgeSetupPath
                : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.BridgeSetupPath));

            if (File.Exists(configuredPath))
            {
                return configuredPath;
            }
        }

        var wwwrootPath = Path.Combine(_environment.WebRootPath, "downloads", SetupFileName);
        return File.Exists(wwwrootPath) ? wwwrootPath : null;
    }
}
