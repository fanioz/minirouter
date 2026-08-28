using System.Diagnostics;

namespace MiniRouter.Cli.ProcessManagement;

public record ServerProcessInfo(Process Process, string Address);

public interface IProcessLocator
{
    ServerProcessInfo? FindRunningServerProcess();
}
