using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MiniRouter.Models;

public record Provider(
    string Id,
    string Name,
    string BaseUrl,
    string ApiKey,
    bool Enabled = true,
    string? Model = null,
    List<string>? Models = null,
    bool? SupportsStreamOptions = null,
    bool? ReportsStreamUsage = null,
    string? PresetId = null,
    double? InputPricePerMillion = null,
    double? OutputPricePerMillion = null
);

public record CreateProviderDto(
    string Id,
    string Name,
    string BaseUrl,
    string ApiKey,
    bool Enabled = true,
    string? Model = null,
    List<string>? Models = null,
    bool? SupportsStreamOptions = null,
    bool? ReportsStreamUsage = null,
    string? PresetId = null,
    double? InputPricePerMillion = null,
    double? OutputPricePerMillion = null
);

public record UpdateProviderDto(
    string Name,
    string BaseUrl,
    string ApiKey,
    bool Enabled,
    string? Model = null,
    List<string>? Models = null,
    bool? SupportsStreamOptions = null,
    bool? ReportsStreamUsage = null,
    double? InputPricePerMillion = null,
    double? OutputPricePerMillion = null
);

public record TestConnectionDto(
    string BaseUrl,
    string ApiKey
);

public record MaskedProvider(
    string Id,
    string Name,
    string BaseUrl,
    string? ApiKeyMasked,
    bool Enabled,
    string? Model,
    List<string>? Models,
    bool? SupportsStreamOptions,
    bool? ReportsStreamUsage,
    string? PresetId,
    double? InputPricePerMillion,
    double? OutputPricePerMillion
);

public enum CircuitStatus
{
    Healthy,
    HalfOpen,
    Open
}

public class ModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string CircuitStatus { get; set; } = "healthy";
}

public class AggregatedModels
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public List<ModelInfo>? Models { get; set; }
    public string? Error { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Provider))]
[JsonSerializable(typeof(CreateProviderDto))]
[JsonSerializable(typeof(UpdateProviderDto))]
[JsonSerializable(typeof(MaskedProvider))]
[JsonSerializable(typeof(List<Provider>), TypeInfoPropertyName = "ProviderList")]
[JsonSerializable(typeof(List<MaskedProvider>), TypeInfoPropertyName = "MaskedProviderList")]
[JsonSerializable(typeof(IEnumerable<MaskedProvider>), TypeInfoPropertyName = "MaskedProviderEnumerable")]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(RequestLog))]
[JsonSerializable(typeof(IEnumerable<RequestLog>))]
[JsonSerializable(typeof(AnalyticsResponse))]
[JsonSerializable(typeof(IEnumerable<AnalyticsResponse>))]
[JsonSerializable(typeof(TestConnectionDto))]
[JsonSerializable(typeof(ApiKey))]
[JsonSerializable(typeof(IEnumerable<ApiKey>))]
[JsonSerializable(typeof(CreateApiKeyDto))]
[JsonSerializable(typeof(CreateApiKeyResponse))]
[JsonSerializable(typeof(UpdateApiKeyDto))]
[JsonSerializable(typeof(KeyAnalyticsResponse))]
[JsonSerializable(typeof(IEnumerable<KeyAnalyticsResponse>))]
[JsonSerializable(typeof(AggregatedModels))]
[JsonSerializable(typeof(List<AggregatedModels>))]
[JsonSerializable(typeof(ModelInfo))]
[JsonSerializable(typeof(List<ModelInfo>))]
[JsonSerializable(typeof(MiniRouter.Services.Presets.PresetInfo))]
[JsonSerializable(typeof(List<MiniRouter.Services.Presets.PresetInfo>))]
[JsonSerializable(typeof(MiniRouter.Services.Presets.EnablePresetRequest))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(OpenAiModelEntry))]
[JsonSerializable(typeof(List<OpenAiModelEntry>))]
[JsonSerializable(typeof(OpenAiModelList))]
[JsonSerializable(typeof(AuthPassthroughConfig))]
[JsonSerializable(typeof(ModelChain))]
[JsonSerializable(typeof(List<ModelChain>), TypeInfoPropertyName = "ModelChainList")]
[JsonSerializable(typeof(CreateModelChainDto))]
[JsonSerializable(typeof(UpdateModelChainDto))]
public partial class AppJsonContext : JsonSerializerContext
{
}


// Anthropic translation types (JSON wire keys are snake_case via manual JsonNode construction)
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AnthropicMessageRequest))]
[JsonSerializable(typeof(AnthropicMessageResponse))]
[JsonSerializable(typeof(ErrorData))]
[JsonSerializable(typeof(AnthropicResponseError))]
public partial class AnthropicAppJsonContext : JsonSerializerContext
{
}

public record ErrorData(string type, string message);
public record AnthropicResponseError(string type, ErrorData error);

public record AnthropicStreamEvent(
    AnthropicStreamEventType Type,
    JsonObject Data
);

public record ErrorResponse(string Error);

public static class ProviderExtensions
{
    public static MaskedProvider Mask(this Provider p) => new(
        p.Id,
        p.Name,
        p.BaseUrl,
        p.ApiKey.Length >= 4 ? $"***{p.ApiKey[^4..]}" : "***",
        p.Enabled,
        p.Model,
        p.Models,
        p.SupportsStreamOptions,
        p.ReportsStreamUsage,
        p.PresetId,
        p.InputPricePerMillion,
        p.OutputPricePerMillion
    );
}

// Anthropic translation DTOs (kept for potential future typed use; translators are JsonNode-based)
// Note: Wire keys are snake_case via manual JSON construction in translators.
public record AnthropicMessageRequest(
    string Model,
    List<AnthropicMessage> Messages,
    string? System = null,
    int? MaxTokens = null,
    double? Temperature = null,
    List<string>? StopSequences = null,
    List<Tool>? Tools = null,
    ToolChoice? ToolChoice = null,
    bool Stream = false
);

public record AnthropicMessage(string Role, object Content);
public record Tool(string Name, string Description, JsonElement InputSchema);
public record ToolChoice(string Type, string? Name = null);

public record AnthropicMessageResponse(
    string Id,
    string Type,
    string Role,
    string Model,
    List<ContentBlock> Content,
    string? StopReason,
    string? StopSequence,
    Usage Usage
);

public record ContentBlock(
    string Type,
    string? Text = null,
    string? Id = null,
    string? Name = null,
    object? Input = null
);

public record Usage(
    int InputTokens,
    int OutputTokens,
    int? CacheReadInputTokens = null,
    int? CacheCreationInputTokens = null
);

// Anthropic stream types defined later


public enum AnthropicStreamEventType
{
    MessageStart,
    ContentBlockStart,
    ContentBlockDelta,
    ContentBlockStop,
    MessageDelta,
    MessageStop,
    Ping,
    Error
}

// Anthropic stream types

public class AnthropicStreamState
{
    public bool MessageStartSent { get; set; }
    public string? MessageId { get; set; }
    public string? Model { get; set; }
    public int NextContentBlockIndex { get; set; }
    public bool TextBlockStarted { get; set; }
    public int TextBlockIndex { get; set; }
    public bool ThinkingBlockStarted { get; set; }
    public int ThinkingBlockIndex { get; set; }
    public Dictionary<int, ToolCallInfo> ToolCalls { get; set; } = new();
    public int? UsagePromptTokens { get; set; }
    public int? UsageCompletionTokens { get; set; }
    public string? FinishReason { get; set; }
}

public record ToolCallInfo(string Id, string Name, int BlockIndex, string ArgBuffer);


// OpenAI Chat Completion request DTOs (for translation)
// Note: These are used internally by the JsonNode-based translators.
// Wire keys are explicit snake_case via manual JSON construction.
public record Message(string Role, object Content, List<ToolCallParam>? ToolCalls = null);
public record ToolCallParam(string Id, string Type, FunctionParam Function);
public record FunctionParam(string Name, string Description, JsonElement Parameters);
public record StreamOptions(bool IncludeUsage);

public record ChatCompletionCreateRequest(
    string Model,
    List<Message> Messages,
    int? MaxTokens = null,
    double? Temperature = null,
    List<string>? Stop = null,
    List<ToolParam>? Tools = null,
    bool? Stream = null,
    StreamOptions? StreamOptions = null
);

public record ToolParam(string Type, FunctionParam Function);


// Story 10.1: OpenAI-format /v1/models response types (AOT-safe named records)
public record OpenAiModelEntry(string Id, string Object, string OwnedBy);
public record OpenAiModelList(string Object, List<OpenAiModelEntry> Data);

// Story 10.1: Auth passthrough config response
public record AuthPassthroughConfig(bool Enabled);
