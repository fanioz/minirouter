using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Tests
{
    /// <summary>
    /// Isolates tests that mutate the MODEL_CHAINS_CONFIG_PATH environment variable
    /// (Issue #11: prevents flakes when parallel test classes change env vars mid-run).
    /// </summary>
    [CollectionDefinition("ModelChainEnvVar")]
    public class ModelChainEnvVarCollection
    {
    }

    [Collection("ModelChainEnvVar")]
    public class ModelChainServiceTests : IDisposable
    {
        private readonly string _tempConfigFile;

        public ModelChainServiceTests()
        {
            _tempConfigFile = Path.GetTempFileName();
            File.WriteAllText(_tempConfigFile, "[]");
            Environment.SetEnvironmentVariable("MODEL_CHAINS_CONFIG_PATH", _tempConfigFile);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("MODEL_CHAINS_CONFIG_PATH", null);
            if (File.Exists(_tempConfigFile)) File.Delete(_tempConfigFile);
            if (File.Exists(_tempConfigFile + ".tmp")) File.Delete(_tempConfigFile + ".tmp");
            if (File.Exists(_tempConfigFile + ".bak")) File.Delete(_tempConfigFile + ".bak");
        }

        private static ModelChainService CreateService() => new ModelChainService();

        // 8.1 — create chain, read back file, verify entry
        [Fact]
        public async Task CreateChain_ValidInput_PersistsToFile()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            var chain = await service.CreateChainAsync(new CreateModelChainDto(
                "tier1",
                "Production fallback",
                new List<string> { "openai/gpt-4o", "anthropic/claude-opus-4-5" }
            ));

            Assert.Equal("tier1", chain.Name);
            Assert.Equal("Production fallback", chain.Description);
            Assert.Equal(2, chain.Models.Count);

            // Verify file was written and is parseable
            var json = await File.ReadAllTextAsync(_tempConfigFile);
            var parsed = JsonSerializer.Deserialize(json, AppJsonContext.Default.ModelChainList);
            Assert.NotNull(parsed);
            Assert.Single(parsed);
            Assert.Equal("tier1", parsed[0].Name);
            Assert.Equal(2, parsed[0].Models.Count);
        }

        // 8.2 — create twice with same name → ArgumentException
        [Fact]
        public async Task CreateChain_DuplicateName_ThrowsArgumentException()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            await service.CreateChainAsync(new CreateModelChainDto(
                "tier1", null, new List<string> { "p1/m1" }
            ));

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.CreateChainAsync(new CreateModelChainDto(
                    "tier1", "duplicate", new List<string> { "p2/m2" }
                ))
            );
            Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // 8.3 — name contains slash → ArgumentException
        [Fact]
        public async Task CreateChain_NameContainsSlash_ThrowsArgumentException()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.CreateChainAsync(new CreateModelChainDto(
                    "a/b", null, new List<string> { "p1/m1" }
                ))
            );
            Assert.Contains("/", ex.Message);
        }

        [Fact]
        public async Task CreateAndUpdateChain_MultiSlashTarget_ThrowsArgumentException()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            var createException = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.CreateChainAsync(new CreateModelChainDto(
                    "invalid", null, new List<string> { "provider/model/variant" }
                ))
            );
            Assert.Contains("exactly one", createException.Message, StringComparison.OrdinalIgnoreCase);

            await service.CreateChainAsync(new CreateModelChainDto(
                "tier1", null, new List<string> { "provider/model" }
            ));

            var updateException = await Assert.ThrowsAsync<ArgumentException>(() =>
                service.UpdateChainAsync("tier1", new UpdateModelChainDto(
                    null, new List<string> { "provider/model/variant" }
                ))
            );
            Assert.Contains("exactly one", updateException.Message, StringComparison.OrdinalIgnoreCase);
        }

        // 8.4 — update description + models, verify name unchanged
        [Fact]
        public async Task UpdateChain_ExistingName_PreservesName()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            await service.CreateChainAsync(new CreateModelChainDto(
                "mychaine", "original desc", new List<string> { "p1/m1" }
            ));

            var updated = await service.UpdateChainAsync("mychaine", new UpdateModelChainDto(
                "updated desc",
                new List<string> { "p1/m1", "p2/m2" }
            ));

            Assert.Equal("mychaine", updated.Name);
            Assert.Equal("updated desc", updated.Description);
            Assert.Equal(2, updated.Models.Count);

            // Verify in-memory list
            var listed = service.ListChains().ToList();
            Assert.Single(listed);
            Assert.Equal("mychaine", listed[0].Name);
        }

        // 8.5 — update non-existent name → KeyNotFoundException
        [Fact]
        public async Task UpdateChain_NotFound_ThrowsKeyNotFoundException()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            await Assert.ThrowsAsync<KeyNotFoundException>(() =>
                service.UpdateChainAsync("ghost", new UpdateModelChainDto(
                    "desc", new List<string> { "p1/m1" }
                ))
            );
        }

        // 8.6 — delete, reload from file, verify gone
        [Fact]
        public async Task DeleteChain_ExistingName_RemovesFromFile()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            await service.CreateChainAsync(new CreateModelChainDto(
                "doomed", null, new List<string> { "p1/m1" }
            ));

            var deleted = await service.DeleteChainAsync("doomed");
            Assert.True(deleted);

            // Reload from file
            var service2 = CreateService();
            await service2.LoadChainsAsync();

            var chains = service2.ListChains().ToList();
            Assert.Empty(chains);
        }

        // 8.7 — delete non-existent → returns false
        [Fact]
        public async Task DeleteChain_NotFound_ReturnsFalse()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            var result = await service.DeleteChainAsync("nonexistent");
            Assert.False(result);
        }

        // 8.8 — missing file → ListChains returns empty, no exception
        [Fact]
        public async Task LoadChains_MissingFile_ReturnsEmpty()
        {
            // Point to a path that doesn't exist
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"chains-{Guid.NewGuid()}.json");
            Environment.SetEnvironmentVariable("MODEL_CHAINS_CONFIG_PATH", nonExistentPath);

            var service = new ModelChainService();
            await service.LoadChainsAsync(); // must not throw

            var chains = service.ListChains().ToList();
            Assert.Empty(chains);

            // Restore env for Dispose
            Environment.SetEnvironmentVariable("MODEL_CHAINS_CONFIG_PATH", _tempConfigFile);
        }

        // 8.9 — IsChain is case-insensitive
        [Fact]
        public async Task IsChain_KnownName_ReturnsTrue_CaseInsensitive()
        {
            var service = CreateService();
            await service.LoadChainsAsync();

            await service.CreateChainAsync(new CreateModelChainDto(
                "tier1", null, new List<string> { "p1/m1" }
            ));

            Assert.True(service.IsChain("tier1"));
            Assert.True(service.IsChain("TIER1"));
            Assert.True(service.IsChain("Tier1"));
            Assert.False(service.IsChain("tier2"));
        }
    }
}
