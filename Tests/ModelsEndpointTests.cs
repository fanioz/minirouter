using System.Collections.Generic;
using System.Linq;
using Xunit;
using MiniRouter.Models;

namespace MiniRouter.Tests
{
    /// <summary>
    /// Unit tests for the /v1/models endpoint aggregation logic (Story 10.1 AC 5).
    /// Tests the model-list building algorithm independently of the HTTP layer.
    /// </summary>
    public class ModelsEndpointTests
    {
        // Mirrors the aggregation logic in Program.cs GET /v1/models
        private static OpenAiModelList BuildOpenAiModelList(IEnumerable<Provider> providers)
        {
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var data = new List<OpenAiModelEntry>();

            foreach (var p in providers.Where(p => p.Enabled))
            {
                var models = p.Models ?? (p.Model != null ? new List<string> { p.Model } : new List<string>());
                foreach (var m in models)
                {
                    var id = m.Contains('/') ? m : $"{p.Id}/{m}";
                    if (seen.Add(id))
                        data.Add(new OpenAiModelEntry(id, "model", p.Id));
                }
            }

            return new OpenAiModelList("list", data);
        }

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
    }
}
