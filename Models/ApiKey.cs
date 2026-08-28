using System.Text.Json.Serialization;

namespace MiniRouter.Models;

public class ApiKey
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [JsonIgnore]
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
}

public class CreateApiKeyDto
{
    public string Name { get; set; } = string.Empty;
}

public class CreateApiKeyResponse : ApiKey
{
    public string PlaintextKey { get; set; } = string.Empty;
}

public class UpdateApiKeyDto
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class KeyAnalyticsResponse
{
    public string ApiKeyId { get; set; } = string.Empty;
    public long TotalTokensIn { get; set; }
    public long TotalTokensOut { get; set; }
    public long TotalRequests { get; set; }
}
