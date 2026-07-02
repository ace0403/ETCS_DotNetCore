using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace ETCS.Pos.Bridge;

[RunInstaller(true)]
public sealed class ProjectInstaller : Installer
{
    public ProjectInstaller()
    {
        var processInstaller = new ServiceProcessInstaller
        {
            Account = ServiceAccount.LocalSystem
        };

        var serviceInstaller = new ServiceInstaller
        {
            ServiceName = "ETCSPosBridge",
            DisplayName = "ETCS POS Bridge",
            Description = "Local HTTP bridge for iBonus SOAP and receipt printing.",
            StartType = ServiceStartMode.Automatic
        };

        Installers.Add(processInstaller);
        Installers.Add(serviceInstaller);
    }
}
