using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console.Cli;
using MiniRouter.Cli.ProcessManagement;

namespace MiniRouter.Cli.Commands;

public class RestartCommand : AsyncCommand<RestartCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-f|--force")]
        [Description("Forcefully restart the server without waiting for graceful shutdown.")]
        public bool Force { get; set; }

        [CommandOption("--verbose")]
        [Description("Enable verbose terminal output.")]
        public bool Verbose { get; set; }
    }

    private readonly IServerProcessManager _processManager;

    public RestartCommand(IServerProcessManager processManager)
    {
        _processManager = processManager;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var options = new RestartOptions 
        { 
            Force = settings.Force, 
            Verbose = settings.Verbose 
        };
        
        using var cts = new CancellationTokenSource();
        
        System.Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var result = await _processManager.RestartServerAsync(options, cts.Token);
        return result.Success ? 0 : 1;
    }
}
