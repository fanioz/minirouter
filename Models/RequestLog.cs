using System.Text.Json.Serialization;

namespace MiniRouter.Models;

public class RequestLog
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? TokensIn { get; set; }
    public int? TokensOut { get; set; }
    public int LatencyMs { get; set; }
    public string? ApiKeyId { get; set; }
    public string? Model { get; set; }
    public bool Estimated { get; set; }
    
    // Cost tracking (Story 8.7)
    public decimal? Cost { get; set; }
    public bool CostEstimated { get; set; }
}

public class AnalyticsResponse
{
    public string ProviderId { get; set; } = string.Empty;
    public long TotalTokensIn { get; set; }
    public long TotalTokensOut { get; set; }
    public long TotalRequests { get; set; }
    public decimal TotalCost { get; set; }
    public decimal EstimatedCostPortion { get; set; }
}
