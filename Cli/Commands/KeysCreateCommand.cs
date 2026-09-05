using System.ComponentModel;
using System.Threading.Tasks;
using MiniRouter.Models;
using MiniRouter.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MiniRouter.Cli.Commands;

public class KeysCreateCommand : AsyncCommand<KeysCreateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Display name for the new key")]
        [CommandOption("-n|--name <NAME>")]
        public string Name { get; set; } = string.Empty;
    }

    private readonly IApiKeyService _apiKeyService;

    public KeysCreateCommand(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Error: --name is required.[/]");
            return 1;
        }

        // Load the schema explicitly since the web host hasn't done it
        await _apiKeyService.InitializeAsync();
        var created = await _apiKeyService.CreateApiKeyAsync(new CreateApiKeyDto { Name = settings.Name });

        AnsiConsole.MarkupLine($"[green]API key '{created.Name}' created ({created.Id}).[/]");
        AnsiConsole.WriteLine();
        // Key format is sk- + lowercase hex, so it cannot collide with Spectre markup
        AnsiConsole.MarkupLine($"[bold]{created.PlaintextKey}[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Warning: this key is shown only once and may be stored in your terminal history. Copy it somewhere safe now.[/]");

        return 0;
    }
}
