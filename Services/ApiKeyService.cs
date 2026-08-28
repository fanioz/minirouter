using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MiniRouter.Models;

namespace MiniRouter.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly string _connectionString;
    private readonly IMemoryCache _cache;

    public ApiKeyService(IConfiguration config, IMemoryCache cache)
    {
        var dbPath = config["DbPath"] ?? "minirouter.db";
        _connectionString = $"Data Source={dbPath}";
        _cache = cache;
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS api_keys (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                key_hash TEXT NOT NULL,
                key_prefix TEXT NOT NULL,
                created_at TEXT NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1,
                last_used_at TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_api_keys_hash ON api_keys (key_hash);
        ";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<CreateApiKeyResponse> CreateApiKeyAsync(CreateApiKeyDto dto)
    {
        var id = "ak_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        
        // Generate plaintext key: sk- + 32 bytes hex
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        var plaintextKey = "sk-" + Convert.ToHexString(keyBytes).ToLowerInvariant();
        
        var keyHash = HashKey(plaintextKey);
        var keyPrefix = plaintextKey.Substring(0, 7); // e.g. "sk-abcd"

        var apiKey = new CreateApiKeyResponse
        {
            Id = id,
            Name = dto.Name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            CreatedAt = DateTime.UtcNow,
            Enabled = true,
            PlaintextKey = plaintextKey
        };

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO api_keys (id, name, key_hash, key_prefix, created_at, enabled)
            VALUES ($id, $name, $key_hash, $key_prefix, $created_at, $enabled)
        ";
        command.Parameters.AddWithValue("$id", apiKey.Id);
        command.Parameters.AddWithValue("$name", apiKey.Name);
        command.Parameters.AddWithValue("$key_hash", apiKey.KeyHash);
        command.Parameters.AddWithValue("$key_prefix", apiKey.KeyPrefix);
        command.Parameters.AddWithValue("$created_at", apiKey.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$enabled", apiKey.Enabled ? 1 : 0);

        await command.ExecuteNonQueryAsync();
        
        return apiKey;
    }

    public async Task<IEnumerable<ApiKey>> ListApiKeysAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, key_hash, key_prefix, created_at, enabled, last_used_at FROM api_keys ORDER BY created_at DESC";
        
        var keys = new List<ApiKey>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            keys.Add(new ApiKey
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                KeyHash = reader.GetString(2),
                KeyPrefix = reader.GetString(3),
                CreatedAt = ParseDate(reader.GetString(4)),
                Enabled = reader.GetInt32(5) == 1,
                LastUsedAt = reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6))
            });
        }
        return keys;
    }

    public async Task<ApiKey?> GetApiKeyByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, key_hash, key_prefix, created_at, enabled, last_used_at FROM api_keys WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ApiKey
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                KeyHash = reader.GetString(2),
                KeyPrefix = reader.GetString(3),
                CreatedAt = ParseDate(reader.GetString(4)),
                Enabled = reader.GetInt32(5) == 1,
                LastUsedAt = reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6))
            };
        }
        return null;
    }

    private DateTime ParseDate(string dateStr)
    {
        if (DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
        {
            return dt;
        }
        if (double.TryParse(dateStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double unixTime))
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)unixTime).UtcDateTime;
        }
        return DateTime.UtcNow;
    }

    public async Task<ApiKey> UpdateApiKeyAsync(string id, UpdateApiKeyDto dto)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "UPDATE api_keys SET name = $name, enabled = $enabled WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", dto.Name);
        command.Parameters.AddWithValue("$enabled", dto.Enabled ? 1 : 0);
        
        var rows = await command.ExecuteNonQueryAsync();
        if (rows == 0) throw new KeyNotFoundException($"ApiKey '{id}' not found.");
        
        await InvalidateCacheAsync(connection, id);
        
        return await GetApiKeyByIdAsync(id) ?? throw new InvalidOperationException();
    }

    public async Task<bool> DeleteApiKeyAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        await InvalidateCacheAsync(connection, id);
        
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM api_keys WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private async Task InvalidateCacheAsync(SqliteConnection connection, string id)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT key_hash FROM api_keys WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        var hash = (await command.ExecuteScalarAsync()) as string;
        if (hash != null)
        {
            _cache.Remove($"apikey_hash:{hash}");
        }
    }

    public async Task<ApiKey?> ValidateAndRecordUsageAsync(string plaintextKey)
    {
        var hash = HashKey(plaintextKey);
        var cacheKey = $"apikey_hash:{hash}";

        if (_cache.TryGetValue(cacheKey, out ApiKey? cachedKey) && cachedKey != null)
        {
            if (cachedKey.Enabled)
            {
                UpdateLastUsedAsync(cachedKey.Id);
            }
            return cachedKey;
        }
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, enabled FROM api_keys WHERE key_hash = $hash";
        command.Parameters.AddWithValue("$hash", hash);
        
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var enabled = reader.GetInt32(1) == 1;
            
            var apiKey = new ApiKey { Id = id, Enabled = enabled };
            _cache.Set(cacheKey, apiKey, TimeSpan.FromMinutes(5));
            
            if (enabled)
            {
                UpdateLastUsedAsync(id);
            }
            return apiKey;
        }
        
        return null;
    }

    private void UpdateLastUsedAsync(string id)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var updateConn = new SqliteConnection(_connectionString);
                await updateConn.OpenAsync();
                var updateCmd = updateConn.CreateCommand();
                updateCmd.CommandText = "UPDATE api_keys SET last_used_at = $now WHERE id = $id";
                updateCmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
                updateCmd.Parameters.AddWithValue("$id", id);
                await updateCmd.ExecuteNonQueryAsync();
            }
            catch (Exception) { }
        });
    }

    private string HashKey(string key)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(key);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
