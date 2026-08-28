using System.Threading;
using System.Threading.Tasks;

namespace MiniRouter.Cli.ProcessManagement;

public interface IServerProcessManager
{
    Task<ProcessResult> RestartServerAsync(RestartOptions options, CancellationToken cancellationToken);
}
