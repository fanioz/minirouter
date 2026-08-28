using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MiniRouter.Models;

namespace MiniRouter.Services;

public interface IProviderService
{
    Task<IEnumerable<MaskedProvider>> ListProvidersAsync();
    Task<IEnumerable<Provider>> ListProvidersUnmaskedAsync();
    Task<MaskedProvider?> GetProviderByIdAsync(string id);
    Task<Provider?> GetProviderByIdUnmaskedAsync(string id);
    Task<Provider> CreateProviderAsync(CreateProviderDto dto);
    Task<Provider> UpdateProviderAsync(string id, UpdateProviderDto dto);
    Task<bool> DeleteProviderAsync(string id);
    Task<Provider?> GetNextProviderAsync(string? requestedModel = null, HashSet<string>? excludedProviderIds = null);
    Task LoadProvidersAsync();
    void RecordSuccess(string providerId, string modelName);
    void RecordFailure(string providerId, string modelName, int statusCode = 0, string? errorText = null, string? retryAfterHeader = null, string? errorBody = null);
    CircuitStatus GetCircuitStatus(string providerId, string modelName);
    string GetCircuitStatusDisplay(string providerId, string modelName);
}

/// <summary>
/// Stores providers in a JSON file (read-modify-write on every CRUD op).
/// Round-robin selection over enabled providers is thread-safe via a lock.
/// </summary>
public class ProviderService : IProviderService, IDisposable
{
    private readonly ILogger<ProviderService>? _logger;
    private readonly string _configPath;
    private readonly List<Provider> _providers = new();
    private readonly ReaderWriterLockSlim _rwLock = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<string, int> _modelIndices = new();
    private readonly ConcurrentDictionary<string, CircuitState> _circuitStates = new();
    private readonly int _circuitFailureThreshold;
    private readonly int _circuitCooldownSeconds;

    private class CircuitState
    {
        public int ConsecutiveFailures;
        public DateTimeOffset? CooldownUntil;
        public int BackoffLevel;
        public bool ProbeInFlight;
    }

    public ProviderService(ILogger<ProviderService>? logger = null)
    {
        _logger = logger;
        _configPath = Environment.GetEnvironmentVariable("PROVIDERS_CONFIG_PATH") ?? "./providers.json";
        if (!int.TryParse(Environment.GetEnvironmentVariable("CIRCUIT_FAILURE_THRESHOLD"), out _circuitFailureThreshold))
            _circuitFailureThreshold = 3;
            
        if (!int.TryParse(Environment.GetEnvironmentVariable("CIRCUIT_COOLDOWN_SECONDS"), out _circuitCooldownSeconds))
            _circuitCooldownSeconds = 30;
    }

    public async Task LoadProvidersAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var tmpPath = GetTempPath();
            var backupPath = _configPath + ".bak";

            // If a previous tmp file was left over, we can ignore/delete it
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }

            // Recovery: If config is missing but backup exists, restore it.
            if (!File.Exists(_configPath) && File.Exists(backupPath))
            {
                File.Copy(backupPath, _configPath, overwrite: true);
            }

            if (!File.Exists(_configPath))
            {
                await File.WriteAllTextAsync(_configPath, "[]");
            }

            string json = "";
            try 
            {
                json = await File.ReadAllTextAsync(_configPath);
                if (string.IsNullOrWhiteSpace(json)) json = "[]";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"[Config] Failed to read {_configPath}. Reverting to empty array.");
                json = "[]";
            }

            var providers = JsonSerializer.Deserialize(json, AppJsonContext.Default.ProviderList);

            _rwLock.EnterWriteLock();
            try
            {
                _providers.Clear();
                if (providers != null)
                {
                    _providers.AddRange(providers);
                }
                _logger?.LogInformation($"[Config] Loaded {_providers.Count} provider(s) from {_configPath}");
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public Task<IEnumerable<MaskedProvider>> ListProvidersAsync()
    {
        _rwLock.EnterReadLock();
        try
        {
            return Task.FromResult<IEnumerable<MaskedProvider>>(
                _providers.Select(p => p.Mask()).ToList());
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public Task<IEnumerable<Provider>> ListProvidersUnmaskedAsync()
    {
        _rwLock.EnterReadLock();
        try
        {
            return Task.FromResult<IEnumerable<Provider>>(_providers.ToList());
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public Task<MaskedProvider?> GetProviderByIdAsync(string id)
    {
        _rwLock.EnterReadLock();
        try
        {
            var provider = _providers.FirstOrDefault(p => p.Id == id);
            return Task.FromResult(provider?.Mask());
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public Task<Provider?> GetProviderByIdUnmaskedAsync(string id)
    {
        _rwLock.EnterReadLock();
        try
        {
            var provider = _providers.FirstOrDefault(p => p.Id == id);
            return Task.FromResult(provider);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    private string GetTempPath() => _configPath + ".tmp";

    private async Task PersistAsync(List<Provider> providers, CancellationToken ct = default)
    {
        var tmpPath = GetTempPath();
        await using var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(fs);
        var json = JsonSerializer.Serialize(providers, AppJsonContext.Default.ProviderList);
        await writer.WriteAsync(json);
        await writer.FlushAsync();
        fs.Flush(true);
        
        // Backup existing valid config before overwriting
        if (File.Exists(_configPath))
        {
            var backupPath = _configPath + ".bak";
            File.Copy(_configPath, backupPath, overwrite: true);
        }

        File.Move(tmpPath, _configPath, overwrite: true);
    }

    public async Task<Provider> CreateProviderAsync(CreateProviderDto dto)
    {
        await _fileLock.WaitAsync();
        try
        {
            Provider created;
            List<Provider> snapshot;
            _rwLock.EnterWriteLock();
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Id))
                    throw new ArgumentException("Provider id must not be empty");

                if (_providers.Any(p => p.Id == dto.Id))
                    throw new ArgumentException($"Provider with id '{dto.Id}' already exists");

                created = new Provider(dto.Id, dto.Name, dto.BaseUrl, dto.ApiKey, dto.Enabled, dto.Model, dto.Models, dto.SupportsStreamOptions, dto.ReportsStreamUsage, dto.PresetId);
                _providers.Add(created);
                snapshot = _providers.ToList();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            await PersistAsync(snapshot);
            return created;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Provider> UpdateProviderAsync(string id, UpdateProviderDto dto)
    {
        await _fileLock.WaitAsync();
        try
        {
            Provider updated;
            List<Provider> snapshot;
            _rwLock.EnterWriteLock();
            try
            {
                var index = _providers.FindIndex(p => p.Id == id);
                if (index < 0)
                    throw new KeyNotFoundException($"Provider '{id}' not found");

                var existing = _providers[index];
                var newApiKey = string.IsNullOrWhiteSpace(dto.ApiKey) ? existing.ApiKey : dto.ApiKey;
                updated = new Provider(id, dto.Name, dto.BaseUrl, newApiKey, dto.Enabled, dto.Model, dto.Models, dto.SupportsStreamOptions, dto.ReportsStreamUsage, existing.PresetId);
                _providers[index] = updated;
                snapshot = _providers.ToList();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            await PersistAsync(snapshot);
            return updated;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> DeleteProviderAsync(string id)
    {
        await _fileLock.WaitAsync();
        try
        {
            List<Provider> snapshot;
            _rwLock.EnterWriteLock();
            try
            {
                var index = _providers.FindIndex(p => p.Id == id);
                if (index < 0)
                    return false;

                _providers.RemoveAt(index);
                snapshot = _providers.ToList();
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            await PersistAsync(snapshot);
            return true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public Task<Provider?> GetNextProviderAsync(string? requestedModel = null, HashSet<string>? excludedProviderIds = null)
    {
        _rwLock.EnterReadLock();
        try
        {
            var enabled = _providers
                .Where(p => p.Enabled)
                .Where(p => excludedProviderIds == null || !excludedProviderIds.Contains(p.Id))
                .Where(p => IsProviderEligibleForRouting(p.Id, p.Model ?? requestedModel ?? string.Empty))
                .Where(p => string.IsNullOrEmpty(requestedModel)
                            || p.Models is null
                            || p.Models.Count == 0
                            || p.Models.Contains(requestedModel))
                .ToList();
            
            if (enabled.Count == 0)
                return Task.FromResult<Provider?>(null);

            string key = requestedModel ?? string.Empty;
            int count = enabled.Count;
            int newIndex = _modelIndices.AddOrUpdate(key, 0, (k, existing) => (existing + 1) % count);

            return Task.FromResult<Provider?>(enabled[newIndex]);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }
    private string GetStateKey(string providerId, string modelName) => $"{providerId}::{modelName}";

    private bool IsProviderEligibleForRouting(string providerId, string modelName)
    {
        var key = GetStateKey(providerId, modelName);
        if (_circuitStates.TryGetValue(key, out var state))
        {
            lock (state)
            {
                if (state.CooldownUntil != null)
                {
                    if (DateTimeOffset.UtcNow < state.CooldownUntil.Value)
                        return false; // Still in cooldown

                    // Cooldown expired — half-open. Only one probe request allowed.
                    if (state.ProbeInFlight)
                        return false;

                    state.ProbeInFlight = true;
                    return true;
                }
            }
        }
        return true; // Healthy
    }

    private CircuitStatus GetCircuitStatusEnum(string providerId, string modelName)
    {
        var key = GetStateKey(providerId, modelName);
        if (_circuitStates.TryGetValue(key, out var state))
        {
            lock (state)
            {
                if (state.CooldownUntil != null)
                {
                    if (DateTimeOffset.UtcNow < state.CooldownUntil.Value)
                        return CircuitStatus.Open;
                    return CircuitStatus.HalfOpen;
                }
            }
        }
        return CircuitStatus.Healthy;
    }

    public CircuitStatus GetCircuitStatus(string providerId, string modelName)
        => GetCircuitStatusEnum(providerId, modelName);

    public string GetCircuitStatusDisplay(string providerId, string modelName)
    {
        var key = GetStateKey(providerId, modelName);
        if (_circuitStates.TryGetValue(key, out var state))
        {
            lock (state)
            {
                if (state.CooldownUntil != null)
                {
                    var now = DateTimeOffset.UtcNow;
                    if (now < state.CooldownUntil.Value)
                    {
                        var remaining = Math.Max(0, (int)(state.CooldownUntil.Value - now).TotalSeconds);
                        return $"open ({remaining}s)";
                    }
                    return "half-open";
                }
            }
        }
        return "healthy";
    }

    public void RecordSuccess(string providerId, string modelName)
    {
        var key = GetStateKey(providerId, modelName);
        if (_circuitStates.TryGetValue(key, out var state))
        {
            lock (state)
            {
                if (state.ConsecutiveFailures > 0 || state.CooldownUntil != null || state.BackoffLevel > 0)
                {
                    _logger?.LogInformation(
                        "[CircuitBreaker] Provider {ProviderId} model {ModelName} recovered.",
                        providerId, modelName);
                }
                state.ConsecutiveFailures = 0;
                state.CooldownUntil = null;
                state.BackoffLevel = 0;
                state.ProbeInFlight = false;
            }
        }
    }

    public void RecordFailure(string providerId, string modelName, int statusCode = 0, string? errorText = null, string? retryAfterHeader = null, string? errorBody = null)
    {
        var key = GetStateKey(providerId, modelName);
        var state = _circuitStates.GetOrAdd(key, _ => new CircuitState());
        lock (state)
        {
            if (state.CooldownUntil != null && DateTimeOffset.UtcNow < state.CooldownUntil.Value)
                return; // Already tripped and in cooldown

            state.ProbeInFlight = false;
            state.ConsecutiveFailures++;

            if (state.ConsecutiveFailures >= _circuitFailureThreshold)
            {
                var classification = ErrorClassifier.Classify(statusCode, errorText);
                TimeSpan cooldown;

                if (classification.Kind == ErrorKind.Backoff)
                {
                    state.BackoffLevel = ErrorClassifier.NextBackoffLevel(state.BackoffLevel);
                    cooldown = ErrorClassifier.ComputeBackoffCooldown(state.BackoffLevel);
                }
                else
                {
                    cooldown = classification.Cooldown;
                }

                // Provider-reported reset time overrides computed cooldown (capped)
                var reportedReset = ErrorClassifier.ResolveProviderReportedReset(retryAfterHeader, errorBody, DateTimeOffset.UtcNow);
                if (reportedReset != null && reportedReset.Value > cooldown)
                {
                    cooldown = reportedReset.Value;
                }

                state.CooldownUntil = DateTimeOffset.UtcNow + cooldown;
                _logger?.LogInformation(
                    "[CircuitBreaker] Provider {ProviderId} model {ModelName} tripped! Status {StatusCode}, cooldown {CooldownSeconds}s, backoff level {BackoffLevel}.",
                    providerId, modelName, statusCode, (int)cooldown.TotalSeconds, state.BackoffLevel);
            }
        }
    }

    public void Dispose()
    {
        _rwLock?.Dispose();
        _fileLock?.Dispose();
    }
}
