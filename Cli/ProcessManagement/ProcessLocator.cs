using System.Diagnostics;
using System.IO;

namespace MiniRouter.Cli.ProcessManagement;

public class ProcessLocator : IProcessLocator
{
    private const string PidFile = ".minirouter.pid";

    public ServerProcessInfo? FindRunningServerProcess()
    {
        var currentPid = Process.GetCurrentProcess().Id;

        if (File.Exists(PidFile))
        {
            var content = File.ReadAllText(PidFile).Trim();
            var parts = content.Split('|');
            if (parts.Length == 2 && int.TryParse(parts[0], out int pid))
            {
                if (pid != currentPid)
                {
                    try
                    {
                        var process = Process.GetProcessById(pid);
                        if (!process.HasExited && (process.ProcessName.Contains("MininRouter") || process.ProcessName.Contains("dotnet")))
                        {
                            return new ServerProcessInfo(process, parts[1]);
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
        
        var processes = Process.GetProcessesByName("MininRouter");
        foreach (var p in processes)
        {
            if (p.Id != currentPid && !p.HasExited)
            {
                return new ServerProcessInfo(p, "http://localhost:8080"); // fallback
            }
        }

        return null;
    }
}
