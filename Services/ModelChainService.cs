using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MiniRouter.Models;

namespace MiniRouter.Services;

/// <summary>
/// Stores model chains in a JSON file (read-modify-write on every CRUD op).
/// Thread-safe via ReaderWriterLockSlim for reads and SemaphoreSlim for file I/O.
/// Mirrors the ProviderService pattern.
///
/// Mutation contract: mutators mutate _chains under the write lock, then persist
/// while still holding _fileLock (all mutators serialize on it); a failed persist
/// rolls the in-memory mutation back so memory never outruns disk. If the process
/// dies between mutate and persist, the disk copy wins on the next load.
/// </summary>
public class ModelChainService : IModelChainService, IDisposable
{
    private readonly ILogger<ModelChainService>? _logger;
    private readonly string _configPath;
    private readonly List<ModelChain> _chains = new();
    private readonly ReaderWriterLockSlim _rwLock = new();
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly IMemoryCache? _cache;

    public ModelChainService(ILogger<ModelChainService>? logger = null, IMemoryCache? cache = null)
    {
        _logger = logger;
        _cache = cache;
        _configPath = Environment.GetEnvironmentVariable("MODEL_CHAINS_CONFIG_PATH") ?? "./model_chains.json";
    }

    public async Task LoadChainsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            var tmpPath = GetTempPath();

            // Clean up any leftover temp file from previous failed operation
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }

            // If file doesn't exist, start with empty list
            if (!File.Exists(_configPath))
            {
                _chains.Clear();
                return;
            }

            string json;
            try
            {
                json = await File.ReadAllTextAsync(_configPath);
                if (string.IsNullOrWhiteSpace(json))
                    json = "[]";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"[ModelChainService] Failed to read {_configPath}. Starting with empty chains.");
                json = "[]";
            }

            try
            {
                var loaded = JsonSerializer.Deserialize<List<ModelChain>>(json, AppJsonContext.Default.ModelChainList) ?? new List<ModelChain>();
                _rwLock.EnterWriteLock();
                try
                {
                    _chains.Clear();
                    _chains.AddRange(loaded);
                    _logger?.LogInformation("[ModelChainService] Loaded {Count} chains from {Path}", _chains.Count, _configPath);
                }
                finally
                {
                    _rwLock.ExitWriteLock();
                }
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "[ModelChainService] Failed to parse {Path}. Starting with empty chains.", _configPath);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public IEnumerable<ModelChain> ListChains()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _chains.ToList();
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public ModelChain? GetChain(string name)
    {
        _rwLock.EnterReadLock();
        try
        {
            return _chains.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public bool IsChain(string name)
    {
        return GetChain(name) != null;
    }

    public async Task<ModelChain> CreateChainAsync(CreateModelChainDto dto)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Chain name must not be empty");

        if (dto.Name.Contains('/'))
            throw new ArgumentException("Chain name must not contain '/' character");

        var validationErrors = ModelChain.ValidateTargets(dto.Models).ToList();

        if (validationErrors.Count > 0)
            throw new ArgumentException($"Invalid chain: {string.Join("; ", validationErrors)}");

        await _fileLock.WaitAsync();
        try
        {
            ModelChain chain;
            _rwLock.EnterWriteLock();
            try
            {
                // Check for duplicate (case-insensitive)
                if (_chains.Any(c => c.Name.Equals(dto.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException($"Chain with name '{dto.Name}' already exists");

                chain = new ModelChain(
                    dto.Name.Trim(),
                    dto.Description?.Trim(),
                    dto.Models?.Select(m => m.Trim()).Where(m => !string.IsNullOrWhiteSpace(m)).ToList() ?? new List<string>()
                );

                _chains.Add(chain);
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            // Persist outside the write lock (await is not safe inside ReaderWriterLockSlim)
            await PersistOrRollbackAsync(() =>
            {
                _rwLock.EnterWriteLock();
                try { _chains.RemoveAll(c => ReferenceEquals(c, chain)); }
                finally { _rwLock.ExitWriteLock(); }
            });
            _logger?.LogInformation("[ModelChainService] Created chain '{Name}' with {Count} targets", chain.Name, chain.Models.Count);
            return chain;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<ModelChain> UpdateChainAsync(string name, UpdateModelChainDto dto)
    {
        await _fileLock.WaitAsync();
        try
        {
            ModelChain updated;
            int index;
            ModelChain existing;

            // Single write-lock window: find, validate, mutate, and produce the return
            // value together, so a concurrent delete cannot shift indices between lock
            // acquisitions (Issue #11; mirrors ProviderService.UpdateProviderAsync).
            _rwLock.EnterWriteLock();
            try
            {
                index = _chains.FindIndex(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                    throw new KeyNotFoundException($"Chain '{name}' not found");

                existing = _chains[index];

                var validationErrors = ModelChain.ValidateTargets(dto.Models).ToList();

                if (validationErrors.Count > 0)
                    throw new ArgumentException($"Invalid chain: {string.Join("; ", validationErrors)}");

                // Preserve original name, update description and models
                updated = new ModelChain(
                    existing.Name,
                    dto.Description?.Trim(),
                    dto.Models?.Select(m => m.Trim()).Where(m => !string.IsNullOrWhiteSpace(m)).ToList() ?? new List<string>()
                );

                _chains[index] = updated;
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            // Persist outside the write lock (await is not safe inside ReaderWriterLockSlim)
            await PersistOrRollbackAsync(() =>
            {
                _rwLock.EnterWriteLock();
                try { _chains[index] = existing; }
                finally { _rwLock.ExitWriteLock(); }
            });
            _logger?.LogInformation("[ModelChainService] Updated chain '{Name}'", updated.Name);
            return updated;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> DeleteChainAsync(string name)
    {
        await _fileLock.WaitAsync();
        try
        {
            List<ModelChain> removedChains;
            _rwLock.EnterWriteLock();
            try
            {
                removedChains = _chains.Where(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                _chains.RemoveAll(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                _rwLock.ExitWriteLock();
            }

            // Persist outside the write lock (await is not safe inside ReaderWriterLockSlim)
            if (removedChains.Count > 0)
            {
                await PersistOrRollbackAsync(() =>
                {
                    _rwLock.EnterWriteLock();
                    try { _chains.AddRange(removedChains); }
                    finally { _rwLock.ExitWriteLock(); }
                });
                _logger?.LogInformation("[ModelChainService] Deleted chain '{Name}'", name);
            }

            return removedChains.Count > 0;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private string GetTempPath() => _configPath + ".tmp";

    /// <summary>
    /// Persists the current chain list, rolling back the caller's in-memory
    /// mutation when the write fails so memory never outruns disk (a later
    /// successful persist would otherwise silently write the failed change).
    /// The rollback restores the pre-mutation state; the caller must still
    /// hold _fileLock (indices/order are stable under it). Residual tradeoff:
    /// if the process dies between mutate and persist, the disk copy is
    /// authoritative on the next load.
    /// </summary>
    private async Task PersistOrRollbackAsync(Action rollback)
    {
        try
        {
            await PersistAsync();
        }
        catch
        {
            rollback();
            throw;
        }
    }

    private async Task PersistAsync(CancellationToken ct = default)
    {
        var tmpPath = GetTempPath();

        // Snapshot under the read lock — never hold ReaderWriterLockSlim across
        // an await: continuations may resume on another thread, and ExitReadLock
        // is thread-affine (a failed exit deadlocks the rollback's write lock).
        // Legal here (no lock recursion): every caller releases the write lock
        // before calling PersistAsync, holding only _fileLock.
        string json;
        _rwLock.EnterReadLock();
        try
        {
            json = JsonSerializer.Serialize(_chains, AppJsonContext.Default.ModelChainList);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }

        await using var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(fs);
        await writer.WriteAsync(json.AsMemory(), ct);
        await writer.FlushAsync(ct);
        fs.Flush(true);

        // Backup existing valid config before overwriting
        if (File.Exists(_configPath))
        {
            var backupPath = _configPath + ".bak";
            File.Copy(_configPath, backupPath, overwrite: true);
        }

        File.Move(tmpPath, _configPath, overwrite: true);

        // Invalidate the /v1/models cache so new chains appear immediately
        _cache?.Remove("v1_models_openai");
    }

    public void Dispose()
    {
        _rwLock?.Dispose();
        _fileLock?.Dispose();
    }
}
