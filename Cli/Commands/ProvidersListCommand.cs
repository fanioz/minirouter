using System.Threading.Tasks;
using MiniRouter.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MiniRouter.Cli.Commands;

public class ProvidersListCommand : AsyncCommand<ProvidersListCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    private readonly IProviderService _providerService;

    public ProvidersListCommand(IProviderService providerService)
    {
        _providerService = providerService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // Load providers explicitly since the web host hasn't done it
        await _providerService.LoadProvidersAsync();
        var providers = await _providerService.ListProvidersAsync();

        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Enabled");
        table.AddColumn("Models");

        foreach (var provider in providers)
        {
            table.AddRow(
                provider.Id ?? "",
                provider.Name ?? "",
                provider.Enabled ? "[green]Yes[/]" : "[red]No[/]",
                provider.Models != null ? string.Join(", ", provider.Models) : "Any"
            );
        }

        AnsiConsole.Write(table);

        return 0;
    }
}
