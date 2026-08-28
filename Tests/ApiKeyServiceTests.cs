using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Tests
{
    public class ApiKeyServiceTests : IDisposable
    {
        private readonly string _tempDbFile;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;

        public ApiKeyServiceTests()
        {
            _tempDbFile = Path.GetTempFileName();
            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?> {
                {"DbPath", _tempDbFile}
            };

            _config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        public void Dispose()
        {
            if (File.Exists(_tempDbFile))
            {
                File.Delete(_tempDbFile);
            }
        }

        [Fact]
        public async Task CreateApiKeyAsync_ShouldReturnValidKey()
        {
            var service = new ApiKeyService(_config, _cache);
            await service.InitializeAsync();

            var dto = new CreateApiKeyDto { Name = "Test Key" };
            var result = await service.CreateApiKeyAsync(dto);

            Assert.NotNull(result);
            Assert.Equal("Test Key", result.Name);
            Assert.True(result.Enabled);
            Assert.StartsWith("sk-", result.PlaintextKey);
            Assert.StartsWith("ak_", result.Id);
        }

        [Fact]
        public async Task ListApiKeysAsync_ShouldReturnAllKeys()
        {
            var service = new ApiKeyService(_config, _cache);
            await service.InitializeAsync();

            await service.CreateApiKeyAsync(new CreateApiKeyDto { Name = "Key 1" });
            await service.CreateApiKeyAsync(new CreateApiKeyDto { Name = "Key 2" });

            var keys = await service.ListApiKeysAsync();
            Assert.Equal(2, keys.Count());
        }

        [Fact]
        public async Task GetApiKeyByIdAsync_ShouldReturnKey_IfExists()
        {
            var service = new ApiKeyService(_config, _cache);
            await service.InitializeAsync();

            var created = await service.CreateApiKeyAsync(new CreateApiKeyDto { Name = "Key 1" });
            var fetched = await service.GetApiKeyByIdAsync(created.Id);

            Assert.NotNull(fetched);
            Assert.Equal(created.Id, fetched.Id);
            
            var missing = await service.GetApiKeyByIdAsync("invalid");
            Assert.Null(missing);
        }

        [Fact]
        public async Task UpdateApiKeyAsync_ShouldUpdateKey()
        {
            var service = new ApiKeyService(_config, _cache);
            await service.InitializeAsync();

            var created = await service.CreateApiKeyAsync(new CreateApiKeyDto { Name = "Old Name" });
            var updated = await service.UpdateApiKeyAsync(created.Id, new UpdateApiKeyDto { Name = "New Name", Enabled = false });

            Assert.Equal("New Name", updated.Name);
            Assert.False(updated.Enabled);
        }

        [Fact]
        public async Task DeleteApiKeyAsync_ShouldRemoveKey()
        {
            var service = new ApiKeyService(_config, _cache);
            await service.InitializeAsync();

            var created = await service.CreateApiKeyAsync(new CreateApiKeyDto { Name = "Key to delete" });
            var success = await service.DeleteApiKeyAsync(created.Id);

            Assert.True(success);
            var fetched = await service.GetApiKeyByIdAsync(created.Id);
            Assert.Null(fetched);
        }

        [Fact]
        public async Task ValidateAndRecordUsageAsync_ShouldValidateCorrectly()
        {
            var service = new ApiKeyService(_config, _cache);
            await service.InitializeAsync();

            var created = await service.CreateApiKeyAsync(new CreateApiKeyDto { Name = "Key 1" });
            
            // Valid key
            var validated = await service.ValidateAndRecordUsageAsync(created.PlaintextKey!);
            Assert.NotNull(validated);
            Assert.True(validated.Enabled);

            // Invalid key
            var invalid = await service.ValidateAndRecordUsageAsync("sk-invalidkey");
            Assert.Null(invalid);

            // Disabled key
            await service.UpdateApiKeyAsync(created.Id, new UpdateApiKeyDto { Name = "Disabled Key", Enabled = false });
            var disabled = await service.ValidateAndRecordUsageAsync(created.PlaintextKey!);
            Assert.NotNull(disabled);
            Assert.False(disabled.Enabled);
        }
    }
}
