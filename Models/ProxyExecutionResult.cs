using System;
using System.Collections.Generic;

namespace MiniRouter.Models;

public class ProxyExecutionResult
{
    public bool Success { get; set; }
    
    public int StatusCode { get; set; }
    
    public string ContentType { get; set; } = "application/json";
    
    /// <summary>
    /// If not streaming, contains the full response body bytes.
    /// </summary>
    public byte[]? ResponseBytes { get; set; }
    
    /// <summary>
    /// Headers returned by the upstream provider.
    /// </summary>
    public Dictionary<string, string[]> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    
    public int? TokensIn { get; set; }
    
    public int? TokensOut { get; set; }
    
    public string? ErrorMessage { get; set; }
}
