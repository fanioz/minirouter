using System.Threading.Tasks;
using System.Linq;
using System;
using MiniRouter.Models;
using MiniRouter.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MiniRouter.Cli.Commands;

public class ProvidersEditCommand : AsyncCommand<ProvidersEditCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    private readonly IProviderService _providerService;

    public ProvidersEditCommand(IProviderService providerService)
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
                .Title("Select a [green]provider[/] to edit:")
                .PageSize(10)
                .AddChoices(providers.Select(p => p.Id)));

        var existingProvider = await _providerService.GetProviderByIdUnmaskedAsync(providerId);
        if (existingProvider == null)
        {
            AnsiConsole.MarkupLine($"[red]Provider '{providerId}' not found.[/]");
            return 1;
        }

        var name = AnsiConsole.Prompt(new TextPrompt<string>("Enter Provider [green]Name[/]:").DefaultValue(existingProvider.Name));
        
        string baseUrl;
        while (true)
        {
            baseUrl = AnsiConsole.Prompt(new TextPrompt<string>("Enter [green]Base URL[/]:").DefaultValue(existingProvider.BaseUrl));
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                break;
            AnsiConsole.MarkupLine("[red]Invalid URL format. Must be a valid http or https URL.[/]");
        }

        var apiKey = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [green]API Key[/] (press Enter to keep current):")
            .AllowEmpty()
            .Secret());

        var apiKeyToSave = string.IsNullOrWhiteSpace(apiKey) ? existingProvider.ApiKey : apiKey;

        var enabled = AnsiConsole.Confirm("Is this provider [green]enabled[/]?", defaultValue: existingProvider.Enabled);
        
        var modelStr = AnsiConsole.Prompt(new TextPrompt<string>("Enter [green]Default Model[/] (optional, press Enter to skip):")
            .DefaultValue(existingProvider.Model ?? "")
            .AllowEmpty());
        var model = string.IsNullOrWhiteSpace(modelStr) ? null : modelStr;

        var existingModelsStr = existingProvider.Models != null ? string.Join(", ", existingProvider.Models) : "";
        var modelsStr = AnsiConsole.Prompt(new TextPrompt<string>("Enter [green]Supported Models[/] (optional, comma-separated, press Enter to skip):")
            .DefaultValue(existingModelsStr)
            .AllowEmpty());
        var models = string.IsNullOrWhiteSpace(modelsStr) ? null : modelsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var dto = new UpdateProviderDto(name, baseUrl, apiKeyToSave, enabled, model, models);

        try
        {
            await _providerService.UpdateProviderAsync(providerId, dto);
            AnsiConsole.MarkupLine($"[green]Successfully updated provider '{providerId}'.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error updating provider: {ex.Message}[/]");
            return 1;
        }
    }
}
