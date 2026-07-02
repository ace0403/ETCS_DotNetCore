using System;
using System.ServiceProcess;
using ETCS.Pos.Bridge.Http;

namespace ETCS.Pos.Bridge;

public sealed class PosBridgeService : ServiceBase
{
    private BridgeHttpServer? _server;

    public PosBridgeService()
    {
        ServiceName = "ETCSPosBridge";
        CanStop = true;
        CanPauseAndContinue = false;
        // AutoLog triggers EventLogInstaller during installutil; fails on some PCs (SecurityException).
        AutoLog = false;
    }

    protected override void OnStart(string[] args)
    {
        _server = new BridgeHttpServer();
        _server.Start();
    }

    protected override void OnStop()
    {
        _server?.Stop();
        _server = null;
    }

    internal void StartForDebug()
    {
        OnStart(Array.Empty<string>());
    }

    internal void StopForDebug()
    {
        OnStop();
    }
}
