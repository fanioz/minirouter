using System.Threading.Tasks;
using System.Linq;
using MiniRouter.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MiniRouter.Cli.Commands;

public class ProvidersDeleteCommand : AsyncCommand<ProvidersDeleteCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    private readonly IProviderService _providerService;

    public ProvidersDeleteCommand(IProviderService providerService)
    {
        _providerService = providerService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await _providerService.LoadProvidersAsync();

        var providers = (await _providerService.ListProvidersAsync()).ToList();
        if (!providers.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No providers found.[/]");
            return 0;
        }

        var providerId = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select a [red]provider[/] to delete:")
                .PageSize(10)
                .AddChoices(providers.Select(p => p.Id)));

        if (!AnsiConsole.Confirm($"Are you sure you want to delete the provider [red]'{providerId}'[/]?", defaultValue: false))
        {
            AnsiConsole.MarkupLine("Deletion cancelled.");
            return 0;
        }

        try
        {
            var result = await _providerService.DeleteProviderAsync(providerId);
            if (result)
            {
                AnsiConsole.MarkupLine($"[green]Successfully deleted provider '{providerId}'.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Provider '{providerId}' not found.[/]");
                return 1;
            }
            return 0;
        }
        catch (System.Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error deleting provider: {ex.Message}[/]");
            return 1;
        }
    }
}
