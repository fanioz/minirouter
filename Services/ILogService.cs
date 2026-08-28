using MiniRouter.Models;

namespace MiniRouter.Services;

public interface ILogService
{
    Task InitializeAsync();
    Task LogRequestAsync(RequestLog log);
    Task<IEnumerable<RequestLog>> GetLogsAsync(string? providerId, string? apiKeyId, string? model, bool? success, int limit);
    Task<IEnumerable<AnalyticsResponse>> GetAnalyticsAsync(string? providerId);
    Task<IEnumerable<KeyAnalyticsResponse>> GetKeyAnalyticsAsync(string? apiKeyId);
    Task PruneLogsAsync(int retentionDays);
}
