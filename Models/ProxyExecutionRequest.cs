using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace MiniRouter.Models;

public class ProxyExecutionRequest
{
    /// <summary>
    /// The provider/model as requested (e.g. "openai/gpt-4").
    /// </summary>
    public string RequestedModel { get; set; } = string.Empty;
    
    /// <summary>
    /// The raw request body in JSON format.
    /// </summary>
    public string RequestBodyJson { get; set; } = string.Empty;
    
    /// <summary>
    /// The parsed ApiKeyId (if any) to use for logging.
    /// </summary>
    public string? ApiKeyId { get; set; }
    
    /// <summary>
    /// The Authorization header value (e.g., "Bearer sk-...")
    /// Can be null if bypassing auth (like in CLI mode).
    /// </summary>
    public string? AuthorizationHeader { get; set; }
    
    /// <summary>
    /// Headers to forward to the upstream provider.
    /// </summary>
    public Dictionary<string, string[]> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Request content type.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Indicates if the client requested streaming.
    /// </summary>
    public bool IsStream { get; set; }

    /// <summary>
    /// Stream output directly using this callback if provided.
    /// </summary>
    public Func<byte[], CancellationToken, Task>? OnStreamChunkAsync { get; set; }

    /// <summary>
    /// Per-request streaming state (used for Anthropic SSE translation).
    /// </summary>
    public object? StreamingState { get; set; }
}
