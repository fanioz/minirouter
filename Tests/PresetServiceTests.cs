using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MiniRouter.Models;
using MiniRouter.Services;
using MiniRouter.Services.Presets;

namespace MiniRouter.Tests
{
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _content;

        public FakeHttpMessageHandler(HttpStatusCode status = HttpStatusCode.OK, string content = "{\"data\":[]}")
        {
            _status = status;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_content)
            });
        }
    }

    public class PresetServiceTests : IDisposable
    {
        private readonly string _tempConfigFile;
        private readonly Mock<IProviderService> _mockProviderService;
        private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
        private readonly Mock<ILogger<PresetService>> _mockLogger;
        private readonly IMemoryCache _cache;

        public PresetServiceTests()
        {
            _tempConfigFile = Path.GetTempFileName();
            File.WriteAllText(_tempConfigFile, "[]");
            Environment.SetEnvironmentVariable("PROVIDERS_CONFIG_PATH", _tempConfigFile);
            
            _mockProviderService = new Mock<IProviderService>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _mockLogger = new Mock<ILogger<PresetService>>();
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        public void Dispose()
        {
            if (File.Exists(_tempConfigFile))
                File.Delete(_tempConfigFile);
            var tmp = _tempConfigFile + ".tmp";
            if (File.Exists(tmp))
                File.Delete(tmp);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnAllPresetsWithCorrectCounts()
        {
            var mockProviders = new List<Provider>
            {
                new Provider("oc-1", "Test 1", "url", "key", true, null, null, null, null, "opencode-free"),
                new Provider("oc-2", "Test 2", "url", "key", true, null, null, null, null, "opencode-free"),
                new Provider("groq-1", "Groq Test", "url", "key", true, null, null, null, null, "groq")
            };

            _mockProviderService.Setup(s => s.ListProvidersUnmaskedAsync())
                .ReturnsAsync(mockProviders.AsEnumerable());

            var service = new PresetService(
                _mockProviderService.Object,
                _mockHttpClientFactory.Object,
                _cache,
                _mockLogger.Object
            );

            var presets = await service.GetAllAsync();

            Assert.NotNull(presets);
            Assert.Equal(4, presets.Count); // All 4 hardcoded presets
            Assert.Single(presets.Where(p => p.Id == "opencode-free" && p.ConnectedCount == 2));
            Assert.Single(presets.Where(p => p.Id == "groq" && p.ConnectedCount == 1));
        }

        [Fact]
        public async Task EnablePresetAsync_FreeShouldUseDefaultApiKey()
        {
            // For this test, we use a fake HTTP handler to simulate successful connection validation
            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[{\"id\":\"test-model\"}]}");
            
            _mockHttpClientFactory.Setup(f => f.CreateClient("upstream"))
                .Returns(new System.Net.Http.HttpClient(fakeHandler));

            _mockProviderService.Setup(s => s.ListProvidersUnmaskedAsync())
                .ReturnsAsync(new List<Provider>().AsEnumerable());

            _mockProviderService.Setup(s => s.CreateProviderAsync(It.IsAny<CreateProviderDto>()))
                .ReturnsAsync((CreateProviderDto dto) => new Provider(
                    dto.Id, dto.Name, dto.BaseUrl, dto.ApiKey, dto.Enabled,
                    dto.Model, dto.Models, dto.SupportsStreamOptions, dto.ReportsStreamUsage, dto.PresetId));

            var service = new PresetService(
                _mockProviderService.Object,
                _mockHttpClientFactory.Object,
                _cache,
                _mockLogger.Object
            );

            var provider = await service.EnablePresetAsync("opencode-free");

            Assert.NotNull(provider);
            Assert.Equal("opencode-free", provider.Id);
            Assert.Equal("public", provider.ApiKey);
            Assert.Equal("opencode-free", provider.PresetId);
        }

        [Fact]
        public async Task EnablePresetAsync_ApiKeyWithoutKeyShouldThrow()
        {
            _mockProviderService.Setup(s => s.ListProvidersUnmaskedAsync())
                .ReturnsAsync(new List<Provider>().AsEnumerable());

            var service = new PresetService(
                _mockProviderService.Object,
                _mockHttpClientFactory.Object,
                _cache,
                _mockLogger.Object
            );

            await Assert.ThrowsAsync<ArgumentException>(() => 
                service.EnablePresetAsync("deepseek"));
        }

        [Fact]
        public async Task EnablePresetAsync_DuplicateGeneration_ShouldCreateIdSuffix()
        {
            var existing = new List<Provider>
            {
                new Provider("deepseek", "Existing", "url", "key", true, null, null, null, null, "deepseek"),
                new Provider("deepseek-2", "Existing2", "url", "key", true, null, null, null, null, "deepseek")
            };

            var fakeHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"data\":[]}");
            _mockHttpClientFactory.Setup(f => f.CreateClient("upstream"))
                .Returns(new System.Net.Http.HttpClient(fakeHandler));

            _mockProviderService.Setup(s => s.ListProvidersUnmaskedAsync())
                .ReturnsAsync(existing.AsEnumerable());

            _mockProviderService.Setup(s => s.CreateProviderAsync(It.IsAny<CreateProviderDto>()))
                .ReturnsAsync((CreateProviderDto dto) => new Provider(
                    dto.Id, dto.Name, dto.BaseUrl, dto.ApiKey, dto.Enabled,
                    dto.Model, dto.Models, dto.SupportsStreamOptions, dto.ReportsStreamUsage, dto.PresetId));

            var service = new PresetService(
                _mockProviderService.Object,
                _mockHttpClientFactory.Object,
                _cache,
                _mockLogger.Object
            );

            var provider = await service.EnablePresetAsync("deepseek", "fake-key");

            Assert.NotNull(provider);
            Assert.Equal("deepseek-3", provider.Id);
        }

        [Fact]
        public static void ApplyFilter_OpencodeFree_ShouldOnlyReturnFreeModels()
        {
            var json = @"{
                ""data"": [
                    {""id"": ""gpt-4"", ""pricing"": {""prompt"": ""0.01""}},
                    {""id"": ""claude-opus-free"", ""pricing"": {""prompt"": ""0.05""}},
                    {""id"": ""big-pickle"", ""pricing"": {""prompt"": ""0.01""}},
                    {""id"": ""deepseek-v4-flash-free"", ""pricing"": {""prompt"": ""0.01""}}
                ]
            }";

            var result = PresetService.ApplyFilter(json, "opencode-free");

            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            Assert.Contains(result, m => m == "claude-opus-free");
            Assert.Contains(result, m => m == "big-pickle");
            Assert.Contains(result, m => m == "deepseek-v4-flash-free");
        }

        [Fact]
        public static void ApplyFilter_OpenrouterFree_ShouldOnlyReturnZeroCostModels()
        {
            var json = @"{
                ""data"": [
                    {""id"": ""openai/gpt-4"", ""pricing"": {""prompt"": ""0.01"", ""completion"": ""0.03""}},
                    {""id"": ""meta-llama/llama-3-8b-instruct"", ""pricing"": {""prompt"": ""0.00"", ""completion"": ""0.00""}},
                    {""id"": ""anthropic/claude-haiku"", ""pricing"": {""prompt"": ""0.00"", ""completion"": ""0.00""}}
                ]
            }";

            var result = PresetService.ApplyFilter(json, "openrouter-free");

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Contains(result, m => m == "meta-llama/llama-3-8b-instruct");
            Assert.Contains(result, m => m == "anthropic/claude-haiku");
        }

        [Theory]
        [InlineData("opencode.ai")]
        [InlineData("api.openai.com")]
        [InlineData("https://example.com")]
        [InlineData("http://localhost:8080")]
        public void PresetCatalog_InvalidUrlShouldNotBeInList(string invalidUrl)
        {
            var preset = PresetCatalog.All.FirstOrDefault(p => p.BaseUrl.StartsWith(invalidUrl));
            Assert.Null(preset);
        }

        [Fact]
        public void PresetCatalog_ReturnsFourPresets()
        {
            var all = PresetCatalog.All;

            Assert.Equal(4, all.Count);
            Assert.Contains(all, p => p.Id == "opencode-free");
            Assert.Contains(all, p => p.Id == "openrouter");
            Assert.Contains(all, p => p.Id == "deepseek");
            Assert.Contains(all, p => p.Id == "groq");
        }

        [Fact]
        public void PresetCatalog_PresetPropertiesAreValid()
        {
            var oc = PresetCatalog.All.First(p => p.Id == "opencode-free");

            Assert.Equal(PresetCategory.Free, oc.Category);
            Assert.False(oc.ApiKeyRequired);
            Assert.Equal("public", oc.DefaultApiKey);
            Assert.Equal("https://opencode.ai/zen", oc.BaseUrl);
        }

        [Fact]
        public void PresetCatalog_OpenrouterIsApiKeyCategory()
        {
            var or = PresetCatalog.All.First(p => p.Id == "openrouter");

            Assert.Equal(PresetCategory.ApiKey, or.Category);
            Assert.True(or.ApiKeyRequired);
            Assert.Null(or.DefaultApiKey);
            Assert.NotNull(or.ModelsUrl);
        }
    }
}
