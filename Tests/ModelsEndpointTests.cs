using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Tests
{
    /// <summary>
    /// Unit tests for the /v1/models endpoint aggregation logic (Story 10.1 AC 5).
    /// Exercises the real aggregation builder (Services/ModelsAggregator) used by the
    /// Program.cs GET /v1/models handler — no mirrored copy of the logic here.
    /// In the "ModelChainEnvVar" collection because the chain tests mutate
    /// MODEL_CHAINS_CONFIG_PATH (Issue #11).
    /// </summary>
    [Collection("ModelChainEnvVar")]
    public class ModelsEndpointTests
    {
        // The real builder used by the Program.cs GET /v1/models handler
        private static OpenAiModelList BuildOpenAiModelList(IEnumerable<Provider> providers, IEnumerable<ModelChain>? chains = null)
            => ModelsAggregator.Build(providers, chains);

        [Fact]
        public void GetV1Models_ShouldReturnObjectListFormat()
        {
            var providers = new List<Provider>
            {
                new Provider("openai", "OpenAI", "https://api.openai.com", "", true, null, new List<string> { "gpt-4o" })
            };

            var result = BuildOpenAiModelList(providers);

            Assert.Equal("list", result.Object);
            Assert.NotNull(result.Data);
            Assert.NotEmpty(result.Data);
            Assert.All(result.Data, entry => Assert.Equal("model", entry.Object));
        }

        [Fact]
        public void GetV1Models_ShouldPrefixUnprefixedModelIds()
        {
            var providers = new List<Provider>
            {
                new Provider("openai", "OpenAI", "https://api.openai.com", "", true, null, new List<string> { "gpt-4o" })
            };

            var result = BuildOpenAiModelList(providers);

            // "gpt-4o" has no slash → should become "openai/gpt-4o"
            Assert.Single(result.Data);
            Assert.Equal("openai/gpt-4o", result.Data[0].Id);
            Assert.Equal("openai", result.Data[0].OwnedBy);
        }

        [Fact]
        public void GetV1Models_AlreadyPrefixedModelId_ShouldNotDoublePrefix()
        {
            var providers = new List<Provider>
            {
                new Provider("router", "Router", "https://example.com", "", true, null, new List<string> { "openai/gpt-4o" })
            };

            var result = BuildOpenAiModelList(providers);

            // "openai/gpt-4o" already contains '/' → should stay as-is
            Assert.Single(result.Data);
            Assert.Equal("openai/gpt-4o", result.Data[0].Id);
        }

        [Fact]
        public void GetV1Models_ShouldDeduplicateAcrossProviders()
        {
            // Same model name from two providers with the same ID → deduplicates
            var providers = new List<Provider>
            {
                new Provider("provider-a", "A", "https://example.com", "", true, null, new List<string> { "gpt-4o" }),
                new Provider("provider-a", "A-dup", "https://example.com", "", true, null, new List<string> { "gpt-4o" })
            };

            var result = BuildOpenAiModelList(providers);

            // "provider-a/gpt-4o" appears once despite two providers having it
            Assert.Single(result.Data);
        }

        [Fact]
        public void GetV1Models_ShouldExcludeDisabledProviders()
        {
            var providers = new List<Provider>
            {
                new Provider("enabled", "Enabled", "https://example.com", "", true, null, new List<string> { "gpt-4o" }),
                new Provider("disabled", "Disabled", "https://example.com", "", false, null, new List<string> { "gpt-3.5-turbo" })
            };

            var result = BuildOpenAiModelList(providers);

            Assert.Single(result.Data);
            Assert.DoesNotContain(result.Data, e => e.OwnedBy == "disabled");
        }

        [Fact]
        public void GetV1Models_ProviderWithSingleModelField_ShouldIncludeIt()
        {
            // Provider uses Model (singular) instead of Models (list)
            var providers = new List<Provider>
            {
                new Provider("deepseek", "DeepSeek", "https://api.deepseek.com", "", true, "deepseek-chat", null)
            };

            var result = BuildOpenAiModelList(providers);

            Assert.Single(result.Data);
            Assert.Equal("deepseek/deepseek-chat", result.Data[0].Id);
        }

        [Fact]
        public void GetV1Models_EmptyProviders_ShouldReturnEmptyList()
        {
            var result = BuildOpenAiModelList(new List<Provider>());

            Assert.Equal("list", result.Object);
            Assert.Empty(result.Data);
        }

        // Issue #11 — chain names must surface in GET /v1/models with owned_by == "chain" (Story 13.1)
        [Fact]
        public async Task GetV1Models_Chain_AppearsAsVirtualModelOwnedByChain()
        {
            var tempConfigFile = Path.GetTempFileName();
            File.WriteAllText(tempConfigFile, "[]");
            Environment.SetEnvironmentVariable("MODEL_CHAINS_CONFIG_PATH", tempConfigFile);
            var chainService = new ModelChainService();
            try
            {
                await chainService.LoadChainsAsync();
                await chainService.CreateChainAsync(new CreateModelChainDto(
                    "tier1", "Production fallback", new List<string> { "openai/gpt-4o", "anthropic/claude-opus-4-5" }
                ));

                var providers = new List<Provider>
                {
                    new Provider("openai", "OpenAI", "https://api.openai.com", "", true, null, new List<string> { "gpt-4o" })
                };

                var result = BuildOpenAiModelList(providers, chainService.ListChains());

                // The chain name is listed as a virtual model owned by "chain"
                var chainEntry = Assert.Single(result.Data, e => e.OwnedBy == "chain");
                Assert.Equal("tier1", chainEntry.Id);
                Assert.Equal("model", chainEntry.Object);

                // Provider models keep their own owner and coexist with chain entries
                Assert.Contains(result.Data, e => e.Id == "openai/gpt-4o" && e.OwnedBy == "openai");
            }
            finally
            {
                chainService.Dispose();
                Environment.SetEnvironmentVariable("MODEL_CHAINS_CONFIG_PATH", null);
                if (File.Exists(tempConfigFile)) File.Delete(tempConfigFile);
                if (File.Exists(tempConfigFile + ".tmp")) File.Delete(tempConfigFile + ".tmp");
                if (File.Exists(tempConfigFile + ".bak")) File.Delete(tempConfigFile + ".bak");
            }
        }

        // Issue #11 — the aggregation's seen-set must not duplicate an id already listed
        // from a provider (defensive branch: chain names cannot contain '/', so this is
        // only reachable via direct construction, not via CreateChainAsync)
        [Fact]
        public void GetV1Models_ChainNameMatchingExistingModel_IsNotDuplicated()
        {
            var providers = new List<Provider>
            {
                new Provider("openai", "OpenAI", "https://api.openai.com", "", true, null, new List<string> { "openai/gpt-4o" })
            };
            var chains = new List<ModelChain>
            {
                new ModelChain("openai/gpt-4o", null, new List<string> { "openai/gpt-4o" })
            };

            var result = BuildOpenAiModelList(providers, chains);

            // "openai/gpt-4o" appears exactly once; the provider entry wins, no chain duplicate
            Assert.Single(result.Data, e => e.Id == "openai/gpt-4o");
            Assert.DoesNotContain(result.Data, e => e.OwnedBy == "chain");
        }
    }
}
