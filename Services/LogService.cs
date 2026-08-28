using Microsoft.Data.Sqlite;
using MiniRouter.Models;
using System.Text.Json;

namespace MiniRouter.Services;

public class LogService : ILogService
{
    private readonly string _connectionString;

    public LogService(IConfiguration config)
    {
        var dbPath = config["DbPath"] ?? "minirouter.db";
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS request_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                provider_id TEXT NOT NULL,
                success INTEGER NOT NULL,
                error_message TEXT,
                tokens_in INTEGER,
                tokens_out INTEGER,
                latency_ms INTEGER NOT NULL,
                api_key_id TEXT,
                model TEXT,
                estimated INTEGER NOT NULL DEFAULT 0,
                cost REAL,
                cost_estimated INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_request_log_timestamp ON request_log (timestamp);
            CREATE INDEX IF NOT EXISTS idx_request_log_provider ON request_log (provider_id);
            CREATE INDEX IF NOT EXISTS idx_request_log_api_key ON request_log (api_key_id);
        ";
        await command.ExecuteNonQueryAsync();

        // Migration for existing databases
        try
        {
            var alterCmd = connection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE request_log ADD COLUMN api_key_id TEXT;";
            await alterCmd.ExecuteNonQueryAsync();
            
            var idxCmd = connection.CreateCommand();
            idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_request_log_api_key ON request_log (api_key_id);";
            await idxCmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) { /* Ignore */ }

        try
        {
            var alterModelCmd = connection.CreateCommand();
            alterModelCmd.CommandText = "ALTER TABLE request_log ADD COLUMN model TEXT;";
            await alterModelCmd.ExecuteNonQueryAsync();
            
            var idxModelCmd = connection.CreateCommand();
            idxModelCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_request_log_model ON request_log (model);";
            await idxModelCmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) { /* Ignore */ }

        try
        {
            var alterEstimatedCmd = connection.CreateCommand();
            alterEstimatedCmd.CommandText = "ALTER TABLE request_log ADD COLUMN estimated INTEGER NOT NULL DEFAULT 0;";
            await alterEstimatedCmd.ExecuteNonQueryAsync();
            
            var idxEstimatedCmd = connection.CreateCommand();
            idxEstimatedCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_request_log_estimated ON request_log (estimated);";
            await idxEstimatedCmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) { /* Ignore */ }

        // Cost columns migration (Story 8.7)
        try
        {
            var alterCostCmd = connection.CreateCommand();
            alterCostCmd.CommandText = "ALTER TABLE request_log ADD COLUMN cost REAL;";
            await alterCostCmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) { /* Ignore */ }

        try
        {
            var alterCostEstimatedCmd = connection.CreateCommand();
            alterCostEstimatedCmd.CommandText = "ALTER TABLE request_log ADD COLUMN cost_estimated INTEGER NOT NULL DEFAULT 0;";
            await alterCostEstimatedCmd.ExecuteNonQueryAsync();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1) { /* Ignore */ }
    }

    public async Task LogRequestAsync(RequestLog log)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO request_log (timestamp, provider_id, success, error_message, tokens_in, tokens_out, latency_ms, api_key_id, model, estimated)
            VALUES ($timestamp, $provider_id, $success, $error_message, $tokens_in, $tokens_out, $latency_ms, $api_key_id, $model, $estimated)
        ";
        command.Parameters.AddWithValue("$timestamp", log.Timestamp.ToString("O"));
        command.Parameters.AddWithValue("$provider_id", log.ProviderId);
        command.Parameters.AddWithValue("$success", log.Success ? 1 : 0);
        command.Parameters.AddWithValue("$error_message", log.ErrorMessage ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$tokens_in", log.TokensIn.HasValue ? log.TokensIn.Value : DBNull.Value);
        command.Parameters.AddWithValue("$tokens_out", log.TokensOut.HasValue ? log.TokensOut.Value : DBNull.Value);
        command.Parameters.AddWithValue("$latency_ms", log.LatencyMs);
        command.Parameters.AddWithValue("$api_key_id", log.ApiKeyId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$model", log.Model ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$estimated", log.Estimated ? 1 : 0);
        command.Parameters.AddWithValue("$cost", log.Cost.HasValue ? (object)log.Cost.Value : DBNull.Value);
        command.Parameters.AddWithValue("$cost_estimated", log.CostEstimated ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<RequestLog>> GetLogsAsync(string? providerId, string? apiKeyId, string? model, bool? success, int limit)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
            var query = "SELECT id, timestamp, provider_id, success, error_message, tokens_in, tokens_out, latency_ms, api_key_id, model, estimated, cost, cost_estimated FROM request_log WHERE 1=1";
        
        if (!string.IsNullOrEmpty(providerId))
        {
            query += " AND provider_id = $provider_id";
            command.Parameters.AddWithValue("$provider_id", providerId);
        }

        if (!string.IsNullOrEmpty(apiKeyId))
        {
            query += " AND api_key_id = $api_key_id";
            command.Parameters.AddWithValue("$api_key_id", apiKeyId);
        }

        if (!string.IsNullOrEmpty(model))
        {
            query += " AND model = $model";
            command.Parameters.AddWithValue("$model", model);
        }

        if (success.HasValue)
        {
            query += " AND success = $success";
            command.Parameters.AddWithValue("$success", success.Value ? 1 : 0);
        }

        query += " ORDER BY timestamp DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit", limit);
        command.CommandText = query;

        var logs = new List<RequestLog>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            logs.Add(new RequestLog
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                ProviderId = reader.GetString(2),
                Success = reader.GetInt32(3) == 1,
                ErrorMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
                TokensIn = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                TokensOut = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                LatencyMs = reader.GetInt32(7),
                ApiKeyId = reader.IsDBNull(8) ? null : reader.GetString(8),
                Model = reader.IsDBNull(9) ? null : reader.GetString(9),
                Estimated = reader.GetInt32(10) == 1,
                Cost = reader.IsDBNull(11) ? null : reader.GetDecimal(11),
                CostEstimated = reader.GetInt32(12) == 1
            });
        }

        return logs;
    }

    public async Task<IEnumerable<AnalyticsResponse>> GetAnalyticsAsync(string? providerId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        var query = @"
            SELECT provider_id, SUM(tokens_in) as total_in, SUM(tokens_out) as total_out, COUNT(id) as total_reqs, 
                   COALESCE(SUM(cost), 0.0) as total_cost,
                   COALESCE(SUM(CASE WHEN cost_estimated = 1 THEN cost ELSE 0 END), 0.0) as estimated_cost_portion
            FROM request_log 
            WHERE 1=1";

        if (!string.IsNullOrEmpty(providerId))
        {
            query += " AND provider_id = $provider_id";
            command.Parameters.AddWithValue("$provider_id", providerId);
        }

        query += " GROUP BY provider_id";
        command.CommandText = query;

        var results = new List<AnalyticsResponse>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new AnalyticsResponse
            {
                ProviderId = reader.GetString(0),
                TotalTokensIn = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                TotalTokensOut = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                TotalRequests = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                TotalCost = reader.IsDBNull(4) ? 0.0m : (decimal)reader.GetDouble(4),
                EstimatedCostPortion = reader.IsDBNull(5) ? 0.0m : (decimal)reader.GetDouble(5)
            });
        }

        return results;
    }

    public async Task<IEnumerable<KeyAnalyticsResponse>> GetKeyAnalyticsAsync(string? apiKeyId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        var query = @"
            SELECT api_key_id, SUM(tokens_in) as total_in, SUM(tokens_out) as total_out, COUNT(id) as total_reqs 
            FROM request_log 
            WHERE api_key_id IS NOT NULL";

        if (!string.IsNullOrEmpty(apiKeyId))
        {
            query += " AND api_key_id = $api_key_id";
            command.Parameters.AddWithValue("$api_key_id", apiKeyId);
        }

        query += " GROUP BY api_key_id";
        command.CommandText = query;

        var results = new List<KeyAnalyticsResponse>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new KeyAnalyticsResponse
            {
                ApiKeyId = reader.GetString(0),
                TotalTokensIn = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                TotalTokensOut = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                TotalRequests = reader.GetInt64(3)
            });
        }

        return results;
    }

    public async Task PruneLogsAsync(int retentionDays)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM request_log WHERE timestamp < $cutoff";
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        
        await command.ExecuteNonQueryAsync();
    }
}
