namespace ETCS.Pos.Web.Services;

public interface IBridgeSetupFileResolver
{
    string? Resolve();

    bool IsAvailable { get; }
}
