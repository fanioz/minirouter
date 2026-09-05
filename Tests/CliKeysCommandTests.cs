using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using Xunit;
using MiniRouter.Cli.Commands;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Tests;

/// <summary>
/// CLI keys command tests for issue #9: `minirouter keys create` / `minirouter keys list`.
/// Both commands run against a real SQLite temp DB via the real ApiKeyService.
/// Single class so xUnit runs them sequentially (AnsiConsole recording is global).
/// </summary>
public class CliKeysCommandTests : IDisposable
{
    private readonly string _tempDbFile;
    private readonly ApiKeyService _service;

    public CliKeysCommandTests()
    {
        _tempDbFile = Path.Combine(Path.GetTempPath(), $"minirouter-cli-keys-test-{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "DbPath", _tempDbFile } })
            .Build();
        _service = new ApiKeyService(config, new MemoryCache(new MemoryCacheOptions()));
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbFile))
            File.Delete(_tempDbFile);
    }

    private sealed class StubRemainingArguments : IRemainingArguments
    {
        public IReadOnlyList<string> Raw { get; } = Array.Empty<string>();
        public ILookup<string, string?> Parsed { get; } = Enumerable.Empty<string?>().ToLookup(_ => string.Empty);
    }

    private static CommandContext NewContext() =>
        new(Array.Empty<string>(), new StubRemainingArguments(), "keys", null);

    [Fact]
    public async Task KeysCreate_MintsValidKey_PrintsItOnce_ReadsRealDb()
    {
        var command = new KeysCreateCommand(_service);
        var settings = new KeysCreateCommand.Settings { Name = "vps-bootstrap" };

        AnsiConsole.Record();
        var exitCode = await command.ExecuteAsync(NewContext(), settings);
        var output = AnsiConsole.ExportText();

        Assert.Equal(0, exitCode);

        // The key was persisted to the real DB
        var keys = await _service.ListApiKeysAsync();
        var created = Assert.Single(keys);
        Assert.Equal("vps-bootstrap", created.Name);
        Assert.True(created.Enabled);

        // The plaintext key is printed exactly once and carries the history warning
        var printedKey = output.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("sk-", StringComparison.Ordinal));
        Assert.NotNull(printedKey);
        Assert.Equal(1, output.Split(new[] { printedKey! }, StringSplitOptions.None).Length - 1);
        Assert.Contains("history", output, StringComparison.OrdinalIgnoreCase);

        // The printed key validates against the stored hash — create wrote a valid key
        var validated = await _service.ValidateAndRecordUsageAsync(printedKey!);
        Assert.NotNull(validated);
        Assert.Equal(created.Id, validated!.Id);
    }

    [Fact]
    public async Task KeysCreate_EmptyName_Fails()
    {
        await _service.InitializeAsync();

        var command = new KeysCreateCommand(_service);
        var settings = new KeysCreateCommand.Settings { Name = "" };

        var exitCode = await command.ExecuteAsync(NewContext(), settings);

        Assert.Equal(1, exitCode);
        Assert.Empty(await _service.ListApiKeysAsync());
    }

    [Fact]
    public async Task KeysList_ShowsExpectedColumns_AndKeyRows()
    {
        await _service.InitializeAsync();
        var used = await _service.CreateApiKeyAsync(new CreateApiKeyDto { Name = "ci-runner" });
        var unused = await _service.CreateApiKeyAsync(new CreateApiKeyDto { Name = "spare" });
        await _service.ValidateAndRecordUsageAsync(used.PlaintextKey); // stamps LastUsedAt
        await _service.UpdateApiKeyAsync(unused.Id, new UpdateApiKeyDto { Name = "spare", Enabled = false });

        var command = new KeysListCommand(_service);

        AnsiConsole.Record();
        var exitCode = await command.ExecuteAsync(NewContext(), new KeysListCommand.Settings());
        var output = AnsiConsole.ExportText();

        Assert.Equal(0, exitCode);

        // Expected columns
        Assert.Contains("Prefix", output);
        Assert.Contains("Name", output);
        Assert.Contains("Enabled", output);
        Assert.Contains("Last Used", output);

        // Row content: prefixes, names, disabled marker, never-used marker
        Assert.Contains(used.KeyPrefix, output);
        Assert.Contains(unused.KeyPrefix, output);
        Assert.Contains("ci-runner", output);
        Assert.Contains("spare", output);
        Assert.Contains("Never", output);
        Assert.DoesNotContain(used.PlaintextKey, output); // full key must never be listed
    }
}
