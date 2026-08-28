using System.Threading.Tasks;
using System.Linq;
using System;
using MiniRouter.Models;
using MiniRouter.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MiniRouter.Cli.Commands;

public class ProvidersAddCommand : AsyncCommand<ProvidersAddCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    private readonly IProviderService _providerService;

    public ProvidersAddCommand(IProviderService providerService)
    {
        _providerService = providerService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        await _providerService.LoadProvidersAsync();

        var id = AnsiConsole.Ask<string>("Enter Provider [green]ID[/]:");
        var name = AnsiConsole.Ask<string>("Enter Provider [green]Name[/]:");
        
        string baseUrl;
        while (true)
        {
            baseUrl = AnsiConsole.Ask<string>("Enter [green]Base URL[/]:");
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                break;
            AnsiConsole.MarkupLine("[red]Invalid URL format. Must be a valid http or https URL.[/]");
        }

        var apiKey = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]API Key[/]:")
            .Secret());

        var enabled = AnsiConsole.Confirm("Is this provider [green]enabled[/]?", defaultValue: true);
        
        var modelStr = AnsiConsole.Prompt(new TextPrompt<string>("Enter [green]Default Model[/] (optional, press Enter to skip):").AllowEmpty());
        var model = string.IsNullOrWhiteSpace(modelStr) ? null : modelStr;

        var modelsStr = AnsiConsole.Prompt(new TextPrompt<string>("Enter [green]Supported Models[/] (optional, comma-separated, press Enter to skip):").AllowEmpty());
        var models = string.IsNullOrWhiteSpace(modelsStr) ? null : modelsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var dto = new CreateProviderDto(id, name, baseUrl, apiKey, enabled, model, models);

        try
        {
            await _providerService.CreateProviderAsync(dto);
            AnsiConsole.MarkupLine($"[green]Successfully added provider '{id}'.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error adding provider: {ex.Message}[/]");
            return 1;
        }
    }
}
