using System;
using System.ServiceProcess;
using System.Threading;

namespace ETCS.Pos.Bridge;

internal static class Program
{
    private static void Main(string[] args)
    {
        var runAsConsole = Array.Exists(args, a => string.Equals(a, "--console", StringComparison.OrdinalIgnoreCase))
            || (Environment.UserInteractive && !IsServiceEnvironment());

#if DEBUG
        runAsConsole = true;
#endif

        if (runAsConsole)
        {
            using var service = new PosBridgeService();
            using var stopSignal = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                stopSignal.Set();
            };

            service.StartForDebug();
            Console.WriteLine("ETCS Pos Bridge running on http://127.0.0.1:5050/ — press Ctrl+C to stop.");
            if (!Console.IsInputRedirected)
            {
                Console.WriteLine("Press Enter to stop.");
                var inputThread = new Thread(() =>
                {
                    Console.ReadLine();
                    stopSignal.Set();
                })
                { IsBackground = true };
                inputThread.Start();
            }

            stopSignal.Wait();
            service.StopForDebug();
            return;
        }

        ServiceBase.Run(new PosBridgeService());
    }

    private static bool IsServiceEnvironment() =>
        !Environment.UserInteractive
        || string.Equals(Environment.GetEnvironmentVariable("ETCS_POS_BRIDGE_SERVICE"), "1", StringComparison.Ordinal);
}
