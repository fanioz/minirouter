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
    public class ProviderServiceTests : IDisposable
    {
        private readonly string _tempConfigFile;

        public ProviderServiceTests()
        {
            _tempConfigFile = Path.GetTempFileName();
            File.WriteAllText(_tempConfigFile, "[]");
            Environment.SetEnvironmentVariable("PROVIDERS_CONFIG_PATH", _tempConfigFile);
        }

        public void Dispose()
        {
            if (File.Exists(_tempConfigFile))
            {
                File.Delete(_tempConfigFile);
            }
            var tmp = _tempConfigFile + ".tmp";
            if (File.Exists(tmp))
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public async Task GetNextProviderAsync_ShouldRouteIndependentPerModel()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();

            await service.CreateProviderAsync(new CreateProviderDto("p1", "Provider 1", "url1", "key", true, null, new List<string> { "modelA" }));
            await service.CreateProviderAsync(new CreateProviderDto("p2", "Provider 2", "url2", "key", true, null, new List<string> { "modelA" }));
            await service.CreateProviderAsync(new CreateProviderDto("p3", "Provider 3", "url3", "key", true, null, new List<string> { "modelB" }));
            await service.CreateProviderAsync(new CreateProviderDto("p4", "Provider 4", "url4", "key", true, null, new List<string> { "modelB" }));

            var pA1 = await service.GetNextProviderAsync("modelA");
            var pA2 = await service.GetNextProviderAsync("modelA");
            
            var pB1 = await service.GetNextProviderAsync("modelB");
            
            var pA3 = await service.GetNextProviderAsync("modelA");
            
            var pB2 = await service.GetNextProviderAsync("modelB");

            Assert.NotNull(pA1);
            Assert.NotNull(pA2);
            Assert.NotNull(pB1);
            Assert.NotNull(pA3);
            Assert.NotNull(pB2);

            Assert.Equal("p1", pA1.Id);
            Assert.Equal("p2", pA2.Id);
            Assert.Equal("p1", pA3.Id);

            Assert.Equal("p3", pB1.Id);
            Assert.Equal("p4", pB2.Id);
        }

        [Fact]
        public async Task GetNextProviderAsync_WithConcurrentRequests_ShouldNotThrowAndShouldLoadBalance()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();

            await service.CreateProviderAsync(new CreateProviderDto("p1", "P1", "url1", "key", true, null, new List<string> { "modelA" }));
            await service.CreateProviderAsync(new CreateProviderDto("p2", "P2", "url2", "key", true, null, new List<string> { "modelA" }));
            await service.CreateProviderAsync(new CreateProviderDto("p3", "P3", "url3", "key", true, null, new List<string> { "modelA" }));

            var tasks = new List<Task<Provider?>>();
            for (int i = 0; i < 300; i++)
            {
                tasks.Add(Task.Run(() => service.GetNextProviderAsync("modelA")));
            }

            var results = await Task.WhenAll(tasks);

            var p1Count = results.Count(p => p?.Id == "p1");
            var p2Count = results.Count(p => p?.Id == "p2");
            var p3Count = results.Count(p => p?.Id == "p3");

            Assert.Equal(100, p1Count);
            Assert.Equal(100, p2Count);
            Assert.Equal(100, p3Count);
        }

        [Fact]
        public async Task GetNextProviderAsync_WildcardProviders_ShouldBeCountedProperly()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();

            await service.CreateProviderAsync(new CreateProviderDto("w1", "W1", "url1", "key", true, null, new List<string>()));
            await service.CreateProviderAsync(new CreateProviderDto("w2", "W2", "url2", "key", true, null, new List<string>()));

            var m1_1 = await service.GetNextProviderAsync("random1");
            var m2_1 = await service.GetNextProviderAsync("random2");
            var m1_2 = await service.GetNextProviderAsync("random1");

            Assert.Equal("w1", m1_1?.Id);
            Assert.Equal("w2", m1_2?.Id);

            Assert.Equal("w1", m2_1?.Id);
        }

        [Fact]
        public async Task CreateUpdateDelete_ShouldProduceParseableConfigAndNoTempFile()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();

            await service.CreateProviderAsync(new CreateProviderDto("a1", "A1", "url", "key", true, null, new List<string>()));
            await service.UpdateProviderAsync("a1", new UpdateProviderDto("A1", "url", "key", false, null, new List<string>()));
            await service.CreateProviderAsync(new CreateProviderDto("a2", "A2", "url", "key", true, null, new List<string>()));
            var deleted = await service.DeleteProviderAsync("a1");

            Assert.True(deleted);

            var json = File.ReadAllText(_tempConfigFile);
            var parsed = JsonSerializer.Deserialize(json, AppJsonContext.Default.ProviderList);
            Assert.NotNull(parsed);
            Assert.Single(parsed);
            Assert.Equal("a2", parsed[0].Id);

            Assert.False(File.Exists(_tempConfigFile + ".tmp"));
        }

        [Fact]
        public async Task LoadProvidersAsync_ShouldRemoveOrphanedTempFile()
        {
            var tmpPath = _tempConfigFile + ".tmp";
            File.WriteAllText(tmpPath, "{partial garbage");

            var service = new ProviderService();
            await service.LoadProvidersAsync();

            Assert.False(File.Exists(tmpPath));

            var providers = (await service.ListProvidersAsync()).ToList();
            Assert.Empty(providers);
        }

        [Fact]
        public async Task ConcurrentWrites_ShouldLeaveFileParseable()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();

            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                var id = $"c{i}";
                tasks.Add(Task.Run(() => service.CreateProviderAsync(new CreateProviderDto(id, id, "url", "key", true, null, new List<string>()))));
            }
            await Task.WhenAll(tasks);

            var json = File.ReadAllText(_tempConfigFile);
            var parsed = JsonSerializer.Deserialize(json, AppJsonContext.Default.ProviderList);
            Assert.NotNull(parsed);
            Assert.Equal(20, parsed.Count);
        }

        // --- Story 8.3: Error classification and backoff ---

        [Fact]
        public async Task RecordFailure_With429_BackoffsExponentially()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();
            await service.CreateProviderAsync(new CreateProviderDto("p1", "P1", "url1", "key", true, null, new List<string> { "m" }));

            // Trip with 429 → backoff level 1 → 2s cooldown
            service.RecordFailure("p1", "m", 429, "rate limit");
            service.RecordFailure("p1", "m", 429, "rate limit");
            service.RecordFailure("p1", "m", 429, "rate limit");

            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));

            // Wait for 2s backoff to expire
            await Task.Delay(2500);
            Assert.Equal(CircuitStatus.HalfOpen, service.GetCircuitStatus("p1", "m"));

            // Trip again → backoff level 2 → 4s cooldown
            service.RecordFailure("p1", "m", 429, "rate limit");
            service.RecordFailure("p1", "m", 429, "rate limit");
            service.RecordFailure("p1", "m", 429, "rate limit");
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));
        }

        [Fact]
        public async Task RecordSuccess_ResetsBackoffLevel()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();
            await service.CreateProviderAsync(new CreateProviderDto("p1", "P1", "url1", "key", true, null, new List<string> { "m" }));

            // Trip
            for (int i = 0; i < 3; i++) service.RecordFailure("p1", "m", 429, "rate limit");
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));

            // Wait for cooldown to expire, then recover
            await Task.Delay(2500);
            service.RecordSuccess("p1", "m");

            Assert.Equal(CircuitStatus.Healthy, service.GetCircuitStatus("p1", "m"));

            // Trip again — backoff level should be reset to 0, so level 1 = 2s
            for (int i = 0; i < 3; i++) service.RecordFailure("p1", "m", 429, "rate limit");
            await Task.Delay(2500);
            Assert.Equal(CircuitStatus.HalfOpen, service.GetCircuitStatus("p1", "m"));
        }

        [Fact]
        public async Task RecordFailure_WithRetryAfter_HonorsProviderReset()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();
            await service.CreateProviderAsync(new CreateProviderDto("p1", "P1", "url1", "key", true, null, new List<string> { "m" }));

            // Retry-After: 3 seconds — should be honored over the computed backoff
            for (int i = 0; i < 3; i++) service.RecordFailure("p1", "m", 429, "rate limit", "3");
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));

            // Wait 2.5s — should still be open (provider said 3s)
            await Task.Delay(2500);
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));

            // Wait another 1s — should be half-open now
            await Task.Delay(1000);
            Assert.Equal(CircuitStatus.HalfOpen, service.GetCircuitStatus("p1", "m"));
        }

        [Fact]
        public async Task RecordFailure_With401_Fixed2MinCooldown()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();
            await service.CreateProviderAsync(new CreateProviderDto("p1", "P1", "url1", "key", true, null, new List<string> { "m" }));

            for (int i = 0; i < 3; i++) service.RecordFailure("p1", "m", 401, "Unauthorized");
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));

            // After 3s — still open (401 = 2 min fixed, not backoff)
            await Task.Delay(3000);
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));
        }

        [Fact]
        public async Task ProbeGate_OnlyOneProbeAllowed()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();
            await service.CreateProviderAsync(new CreateProviderDto("p1", "P1", "url1", "key", true, null, new List<string> { "m" }));
            await service.CreateProviderAsync(new CreateProviderDto("p2", "P2", "url2", "key", true, null, new List<string> { "m" }));

            // Trip p1
            for (int i = 0; i < 3; i++) service.RecordFailure("p1", "m", 500, "internal error");
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));

            // Wait for cooldown (transient 30s is too long — use 429 backoff 2s instead)
            service.RecordSuccess("p1", "m");
            for (int i = 0; i < 3; i++) service.RecordFailure("p1", "m", 429, "rate limit");
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));

            await Task.Delay(2500);
            Assert.Equal(CircuitStatus.HalfOpen, service.GetCircuitStatus("p1", "m"));

            // First request admitted as probe
            var first = await service.GetNextProviderAsync("m");
            Assert.NotNull(first);

            // Second request — p1 is probing, so it should pick p2
            var second = await service.GetNextProviderAsync("m");
            Assert.NotNull(second);
            Assert.Equal("p2", second.Id);
        }

        [Fact]
        public async Task ProbeGate_FailedProbe_Retries()
        {
            var service = new ProviderService();
            await service.LoadProvidersAsync();
            await service.CreateProviderAsync(new CreateProviderDto("p1", "P1", "url1", "key", true, null, new List<string> { "m" }));

            for (int i = 0; i < 3; i++) service.RecordFailure("p1", "m", 429, "rate limit");
            await Task.Delay(2500);
            Assert.Equal(CircuitStatus.HalfOpen, service.GetCircuitStatus("p1", "m"));

            // Admit probe
            var probe = await service.GetNextProviderAsync("m");
            Assert.NotNull(probe);

            // Probe fails — re-trip at next backoff level
            service.RecordFailure("p1", "m", 429, "rate limit");
            Assert.Equal(CircuitStatus.Open, service.GetCircuitStatus("p1", "m"));
        }
    }
}
