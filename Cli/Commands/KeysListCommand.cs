using System.Threading.Tasks;
using MiniRouter.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace MiniRouter.Cli.Commands;

public class KeysListCommand : AsyncCommand<KeysListCommand.Settings>
{
    public class Settings : CommandSettings
    {
    }

    private readonly IApiKeyService _apiKeyService;

    public KeysListCommand(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // Load the schema explicitly since the web host hasn't done it
        await _apiKeyService.InitializeAsync();
        var keys = await _apiKeyService.ListApiKeysAsync();

        var table = new Table();
        table.AddColumn("Prefix");
        table.AddColumn("Name");
        table.AddColumn("Enabled");
        table.AddColumn("Last Used");

        foreach (var key in keys)
        {
            table.AddRow(
                key.KeyPrefix,
                key.Name,
                key.Enabled ? "[green]Yes[/]" : "[red]No[/]",
                key.LastUsedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "Never"
            );
        }

        AnsiConsole.Write(table);

        return 0;
    }
}
