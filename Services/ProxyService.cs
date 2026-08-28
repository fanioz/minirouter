using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiniRouter.Models;
using MiniRouter.Services.Translation;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Linq;
using System;

namespace MiniRouter.Services;

public class ProxyService : IProxyService
{
    private readonly IProviderService _providerService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogService _logService;

    public ProxyService(IProviderService providerService, IHttpClientFactory httpClientFactory, ILogService logService)
    {
        _providerService = providerService;
        _httpClientFactory = httpClientFactory;
        _logService = logService;
    }

    public async Task HandleChatCompletionAsync(HttpContext ctx)
    {
        var ct = ctx.RequestAborted;
        
        string? apiKeyId = ctx.Items["ApiKeyId"] as string;

        // Read full request body
        var reqBodyBytes = await new StreamReader(ctx.Request.Body).ReadToEndAsync(ct);
        JsonNode? reqNode = null;
        try
        {
            reqNode = JsonNode.Parse(reqBodyBytes);
        }
        catch (JsonException) { }

        string? requestedModel = reqNode?["model"]?.ToString();
        
        bool isStream = false;
        if (reqNode?["stream"] is JsonValue streamVal && streamVal.TryGetValue(out bool sv))
        {
            isStream = sv;
        }

        var executionRequest = new ProxyExecutionRequest
        {
            RequestedModel = requestedModel ?? string.Empty,
            RequestBodyJson = reqBodyBytes,
            ApiKeyId = apiKeyId,
            AuthorizationHeader = ctx.Request.Headers.Authorization.ToString(),
            ContentType = ctx.Request.ContentType ?? "application/json",
            IsStream = isStream,
            OnStreamChunkAsync = async (chunk, token) =>
            {
                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.StatusCode = StatusCodes.Status200OK;
                    ctx.Response.ContentType = "text/event-stream";
                    ctx.Response.Headers.Append("Cache-Control", "no-cache");
                    ctx.Response.Headers.Append("Connection", "keep-alive");
                }
                await ctx.Response.BodyWriter.WriteAsync(chunk, token);
                await ctx.Response.Body.FlushAsync(token);
            }
        };

        foreach (var header in ctx.Request.Headers)
        {
            if (header.Key is "Host" or "Transfer-Encoding" or "Authorization" or "Content-Type" or "Content-Length")
                continue;
            executionRequest.Headers[header.Key] = header.Value.Where(v => v != null).Cast<string>().ToArray();
        }

        var result = await ExecuteCompletionAsync(executionRequest, ct);

        // Map status/content-type/headers BEFORE any body write.
        // For streamed responses the first chunk has already committed the response,
        // so skip re-setting anything once it has started.
        if (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = result.StatusCode;
            if (result.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
            {
                ctx.Response.Headers.Append("Cache-Control", "no-cache");
                ctx.Response.Headers.Append("Connection", "keep-alive");
                ctx.Response.ContentType = result.ContentType;
            }

            foreach (var header in result.Headers)
            {
                ctx.Response.Headers[header.Key] = header.Value;
            }

            if (result.ResponseBytes != null)
            {
                await ctx.Response.BodyWriter.WriteAsync(result.ResponseBytes, ct);
            }
            else if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(new ErrorResponse(result.ErrorMessage), AppJsonContext.Default.ErrorResponse), ct);
            }
        }
    }

    public async Task HandleAnthropicMessagesAsync(HttpContext ctx)
    {
        var ct = ctx.RequestAborted;
        
        string? apiKeyId = ctx.Items["ApiKeyId"] as string;

        // Read full request body
        var reqBodyBytes = await new StreamReader(ctx.Request.Body).ReadToEndAsync(ct);

        // Parse as JsonNode (snake_case wire keys stay explicit, AOT-safe)
        JsonNode? reqNode = null;
        try
        {
            reqNode = JsonNode.Parse(reqBodyBytes);
        }
        catch (JsonException)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\",\"message\":\"Invalid JSON\"}}", ct);
            return;
        }

        if (reqNode == null || string.IsNullOrEmpty(reqNode["model"]?.ToString()))
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\",\"message\":\"Model must be specified\"}}", ct);
            return;
        }

        bool isStream = reqNode["stream"]?.GetValue<bool>() ?? false;

        // Translate Anthropic → OpenAI (JsonNode-based, explicit snake_case keys)
        var openaiReq = AnthropicRequestTranslator.ToOpenAI(reqNode);
        var openaiBody = openaiReq.ToJsonString();

        // Per-request streaming state for Anthropic translation
        var anthropicState = new AnthropicStreamState();

        var executionRequest = new ProxyExecutionRequest
        {
            RequestedModel = openaiReq["model"]?.ToString() ?? string.Empty,
            RequestBodyJson = openaiBody,
            ApiKeyId = apiKeyId,
            AuthorizationHeader = ctx.Request.Headers.Authorization.ToString(),
            ContentType = "application/json",
            IsStream = isStream,
            StreamingState = anthropicState,
            OnStreamChunkAsync = async (chunk, token) =>
            {
                if (!isStream)
                {
                    await ctx.Response.BodyWriter.WriteAsync(chunk, token);
                    await ctx.Response.Body.FlushAsync(token);
                    return;
                }

                if (!ctx.Response.HasStarted)
                {
                    ctx.Response.StatusCode = StatusCodes.Status200OK;
                    ctx.Response.ContentType = "text/event-stream";
                    ctx.Response.Headers.Append("Cache-Control", "no-cache");
                    ctx.Response.Headers.Append("Connection", "keep-alive");
                }

                try
                {
                    var chunkStr = Encoding.UTF8.GetString(chunk);
                    
                    // Parse OpenAI chunk JSON (strip "data: " prefix if present)
                    var jsonStr = chunkStr.StartsWith("data: ") ? chunkStr.Substring(6) : chunkStr;
                    
                    if (jsonStr == "[DONE]" || string.IsNullOrWhiteSpace(jsonStr))
                        return;
                    
                    var chunkNode = JsonNode.Parse(jsonStr);
                    if (chunkNode != null)
                    {
                        var events = AnthropicStreamTranslator.Translate(chunkNode, ref anthropicState);
                        
                        foreach (var evt in events)
                        {
                            var eventData = evt.Data.ToJsonString();
                            var eventLine = $"event: {AnthropicStreamTranslator.MapEventType(evt.Type)}\ndata: {eventData}\n\n";
                            var eventBytes = Encoding.UTF8.GetBytes(eventLine);
                            await ctx.Response.BodyWriter.WriteAsync(eventBytes, token);
                        }
                        await ctx.Response.Body.FlushAsync(token);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Anthropic Stream] Translation error: {ex.Message}");
                }
                
                // Fallback: passthrough
                await ctx.Response.BodyWriter.WriteAsync(chunk, token);
                await ctx.Response.Body.FlushAsync(token);
            }
        };

        foreach (var header in ctx.Request.Headers)
        {
            if (header.Key is "Host" or "Transfer-Encoding" or "Authorization" or "Content-Type" or "Content-Length")
                continue;
            executionRequest.Headers[header.Key] = header.Value.Where(v => v != null).Cast<string>().ToArray();
        }

        var result = await ExecuteCompletionAsync(executionRequest, ct);

        // Set status/content-type/headers BEFORE any body write (skip if already streamed)
        if (!ctx.Response.HasStarted)
            ctx.Response.StatusCode = result.StatusCode;
            
        if (result.ContentType?.StartsWith("text/event-stream", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (!ctx.Response.Headers.ContainsKey("Cache-Control"))
                ctx.Response.Headers.Append("Cache-Control", "no-cache");
            if (!ctx.Response.Headers.ContainsKey("Connection"))
                ctx.Response.Headers.Append("Connection", "keep-alive");
            if (!ctx.Response.Headers.ContainsKey("Content-Type"))
                ctx.Response.ContentType = "text/event-stream";
        }

        foreach (var header in result.Headers)
        {
            ctx.Response.Headers[header.Key] = header.Value;
        }

        if (!isStream && result.Success && result.ResponseBytes != null)
        {
            var respJson = System.Text.Encoding.UTF8.GetString(result.ResponseBytes);
            var respNode = JsonNode.Parse(respJson);
            
            try
            {
                var anthropicResp = AnthropicResponseTranslator.ToAnthropic(respNode, openaiReq["model"]?.ToString() ?? "");
                await ctx.Response.BodyWriter.WriteAsync(System.Text.Encoding.UTF8.GetBytes(anthropicResp.ToJsonString()), ct);
            }
            catch
            {
                await ctx.Response.BodyWriter.WriteAsync(result.ResponseBytes, ct);
            }
        }
        else if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
        {
            await ctx.Response.WriteAsync(TranslateErrorToAnthropic(result.ErrorMessage), ct);
        }
    }

    private string TranslateErrorToAnthropic(string message)
    {
        return new JsonObject
        {
            ["type"] = "error",
            ["error"] = new JsonObject
            {
                ["type"] = "invalid_request_error",
                ["message"] = message
            }
        }.ToJsonString();
    }

    public async Task<ProxyExecutionResult> ExecuteCompletionAsync(ProxyExecutionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.RequestedModel))
        {
            return new ProxyExecutionResult 
            { 
                Success = false, 
                StatusCode = StatusCodes.Status400BadRequest, 
                ErrorMessage = "Model must be specified." 
            };
        }

        if (request.RequestedModel.Contains('/'))
        {
            var targetModels = request.RequestedModel.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            ProxyExecutionResult? lastResult = null;

            foreach (var target in targetModels)
            {
                var parts = target.Split('/', 2);
                if (parts.Length != 2)
                {
                    lastResult = new ProxyExecutionResult { Success = false, StatusCode = StatusCodes.Status400BadRequest, ErrorMessage = $"Model '{target}' must be specified as 'providerId/modelName'." };
                    continue;
                }

                var targetProviderId = parts[0];
                var targetModelName = parts[1];

                var maskedProvider = await _providerService.GetProviderByIdAsync(targetProviderId);
                if (maskedProvider == null)
                {
                    lastResult = new ProxyExecutionResult { Success = false, StatusCode = StatusCodes.Status404NotFound, ErrorMessage = $"Provider '{targetProviderId}' not found" };
                    continue;
                }

                if (!maskedProvider.Enabled)
                {
                    lastResult = new ProxyExecutionResult { Success = false, StatusCode = StatusCodes.Status400BadRequest, ErrorMessage = $"Provider '{targetProviderId}' is disabled" };
                    continue;
                }

                var circuitStatus = _providerService.GetCircuitStatus(targetProviderId, targetModelName);
                if (circuitStatus == CircuitStatus.Open)
                {
                    lastResult = new ProxyExecutionResult { Success = false, StatusCode = StatusCodes.Status503ServiceUnavailable, ErrorMessage = $"Provider '{targetProviderId}' model '{targetModelName}' is temporarily unavailable (circuit open)" };
                    continue;
                }
                
                var provider = await _providerService.GetProviderByIdUnmaskedAsync(targetProviderId);
                if (provider == null)
                {
                    lastResult = new ProxyExecutionResult { Success = false, StatusCode = StatusCodes.Status404NotFound, ErrorMessage = $"Provider '{targetProviderId}' not found" };
                    continue;
                }
                
                var result = await ExecuteSingleProviderAsync(provider, targetModelName, request, ct);
                
                // If success or a 4xx client error (except 429 Too Many Requests), don't fallback. Return immediately.
                if (result.Success || (result.StatusCode >= 400 && result.StatusCode < 500 && result.StatusCode != StatusCodes.Status429TooManyRequests))
                {
                    return result;
                }

                // If 5xx or 429, save as lastResult and fallback to next model
                lastResult = result;
            }

            return lastResult ?? new ProxyExecutionResult { Success = false, StatusCode = StatusCodes.Status502BadGateway, ErrorMessage = "All fallback models exhausted" };
        }
        else
        {
            // Round-robin execution for virtual model aliases
            return await HandleRoundRobinExecutionAsync(request.RequestedModel, request, ct);
        }
    }

    private async Task<ProxyExecutionResult> HandleRoundRobinExecutionAsync(string? requestedModel, ProxyExecutionRequest request, CancellationToken ct)
    {
        int maxRetries = 2;
        if (int.TryParse(Environment.GetEnvironmentVariable("MAX_RETRIES"), out int parsedRetries))
        {
            maxRetries = parsedRetries;
        }
        
        int attempts = 0;
        HashSet<string> triedProviderIds = new();
        ProxyExecutionResult? lastResult = null;

        while (attempts <= maxRetries)
        {
            var provider = await _providerService.GetNextProviderAsync(requestedModel, triedProviderIds);

            if (provider is null)
            {
                if (attempts == 0)
                {
                    var msg = requestedModel is null
                        ? "No enabled providers available"
                        : $"No enabled providers available for model '{requestedModel}'";
                    return new ProxyExecutionResult { Success = false, StatusCode = StatusCodes.Status503ServiceUnavailable, ErrorMessage = msg };
                }
                break; // Exhausted available providers
            }

            triedProviderIds.Add(provider.Id);
            attempts++;

            Console.WriteLine($"[RoundRobin] Selected provider: {provider.Name} ({provider.Id}) for model '{requestedModel ?? "(none)"}' (Attempt {attempts}/{maxRetries + 1})");

            var result = await ExecuteSingleProviderAsync(provider, provider.Model ?? requestedModel, request, ct);
            if (result.Success || result.StatusCode == 400 || result.StatusCode == 401 || result.StatusCode == 403 || result.StatusCode == 404 || result.StatusCode == 422)
            {
                // Unrecoverable errors or success, return immediately
                return result;
            }

            lastResult = result;
        }

        return lastResult ?? new ProxyExecutionResult { Success = false, StatusCode = StatusCodes.Status502BadGateway, ErrorMessage = "All retries exhausted" };
    }

    private async Task<ProxyExecutionResult> ExecuteSingleProviderAsync(Provider provider, string? targetModelName, ProxyExecutionRequest request, CancellationToken ct)
    {
        JsonNode? reqNode = null;
        string? actualModel = targetModelName;
        try
        {
            reqNode = JsonNode.Parse(request.RequestBodyJson);
            if (string.IsNullOrEmpty(actualModel) && reqNode is JsonObject obj && obj.TryGetPropertyValue("model", out var modelNode))
            {
                actualModel = modelNode?.ToString();
            }
        }
        catch (JsonException) { }

        bool injectedIncludeUsage = false;
        if (request.IsStream && provider.SupportsStreamOptions != false && reqNode is JsonObject reqObj)
        {
            var streamOptions = reqObj["stream_options"] as JsonObject;
            if (streamOptions == null)
            {
                streamOptions = new JsonObject();
                reqObj["stream_options"] = streamOptions;
            }

            if (streamOptions["include_usage"] == null || streamOptions["include_usage"]?.GetValue<bool>() == false)
            {
                streamOptions["include_usage"] = true;
                injectedIncludeUsage = true;
            }
        }

        var finalReqNode = reqNode != null ? JsonNode.Parse(reqNode.ToJsonString()) : null;
        if (finalReqNode is JsonObject rootObj && !string.IsNullOrEmpty(targetModelName))
        {
            rootObj["model"] = targetModelName;
        }

        var finalReqBodyString = finalReqNode != null ? finalReqNode.ToJsonString() : request.RequestBodyJson;
        var upstreamUrl = $"{provider.BaseUrl.TrimEnd('/')}/v1/chat/completions";
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, upstreamUrl);

        foreach (var header in request.Headers)
        {
            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(provider.ApiKey))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        }
        
        requestMessage.Content = new StringContent(finalReqBodyString, Encoding.UTF8, request.ContentType);

        var httpClient = _httpClientFactory.CreateClient("upstream");
        var sw = Stopwatch.StartNew();

        HttpResponseMessage? upstreamResponse = null;
        try
        {
            upstreamResponse = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            Console.WriteLine($"[Proxy] Request to {provider.Name} failed: {ex.Message}");
            _providerService.RecordFailure(provider.Id, actualModel ?? string.Empty, 0, ex.Message);
            await _logService.LogRequestAsync(new RequestLog
            {
                Timestamp = DateTime.UtcNow,
                ProviderId = provider.Id,
                ApiKeyId = request.ApiKeyId,
                Success = false,
                ErrorMessage = "Provider connection failed",
                LatencyMs = (int)sw.ElapsedMilliseconds,
                Model = actualModel
            });

            return new ProxyExecutionResult
            {
                Success = false,
                StatusCode = StatusCodes.Status502BadGateway,
                ErrorMessage = "Provider connection failed"
            };
        }

        bool success = upstreamResponse.IsSuccessStatusCode;
        var result = new ProxyExecutionResult
        {
            Success = success,
            StatusCode = (int)upstreamResponse.StatusCode,
            ContentType = upstreamResponse.Content.Headers.ContentType?.ToString() ?? "application/json"
        };

        foreach (var header in upstreamResponse.Headers)
        {
            if (header.Key is "Transfer-Encoding" or "Connection" or "Server")
                continue;
            result.Headers[header.Key] = header.Value.ToArray();
        }

        if (!success)
        {
            using (upstreamResponse)
            {
                var errorBytes = await upstreamResponse.Content.ReadAsByteArrayAsync(ct);
                var errorBody = System.Text.Encoding.UTF8.GetString(errorBytes);
                var retryAfter = upstreamResponse.Headers.RetryAfter?.ToString();
                _providerService.RecordFailure(provider.Id, actualModel ?? string.Empty, (int)upstreamResponse.StatusCode, errorBody, retryAfter, errorBody);
                var errorMessage = $"Upstream returned {(int)upstreamResponse.StatusCode}";
                
                sw.Stop();
                await _logService.LogRequestAsync(new RequestLog
                {
                    Timestamp = DateTime.UtcNow,
                    ProviderId = provider.Id,
                    ApiKeyId = request.ApiKeyId,
                    Success = false,
                    ErrorMessage = errorMessage,
                    LatencyMs = (int)sw.ElapsedMilliseconds,
                    Model = actualModel
                });

                result.ResponseBytes = errorBytes;
                result.ErrorMessage = errorMessage;
                return result;
            }
        }

        // SUCCESS PATH
        int? tokensIn = null;
        int? tokensOut = null;
        bool estimated = false;
        var responseTextBuilder = new System.Text.StringBuilder();
        var clientRequestedUsage = request.IsStream && !injectedIncludeUsage;
        
        using (upstreamResponse)
        {
            _providerService.RecordSuccess(provider.Id, actualModel ?? string.Empty);
            
            if (!request.IsStream)
            {
                var respBytes = await upstreamResponse.Content.ReadAsByteArrayAsync(ct);
                sw.Stop();
                
                try
                {
                    var respNode = JsonNode.Parse(respBytes);
                    var usage = respNode?["usage"];
                    if (usage != null)
                    {
                        tokensIn = usage["prompt_tokens"]?.GetValue<int>();
                        tokensOut = usage["completion_tokens"]?.GetValue<int>();
                    }
                }
                catch (JsonException) { }

                result.ResponseBytes = respBytes;
                result.TokensIn = tokensIn;
                result.TokensOut = tokensOut;
            }
            else
            {
                try
                {
                    await using var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(ct);
                    using var reader = new StreamReader(upstreamStream);
                    
                    // Don't use OnStreamChunkAsync - collect locally instead
                    while (true)
                    {
                        var line = await reader.ReadLineAsync(ct);
                        if (line == null) break;

                        if (line.StartsWith("data: ") && line != "data: [DONE]")
                        {
                            var jsonStr = line.Substring("data: ".Length);
                            try
                            {
                                var chunkNode = JsonNode.Parse(jsonStr);
                                var usage = chunkNode?["usage"];
                                if (usage != null)
                                {
                                    tokensIn = usage["prompt_tokens"]?.GetValue<int>();
                                    tokensOut = usage["completion_tokens"]?.GetValue<int>();
                                    
                                    if (injectedIncludeUsage)
                                    {
                                        var choices = chunkNode?["choices"] as JsonArray;
                                        if (choices == null || choices.Count == 0)
                                        {
                                            continue; // skip writing this chunk to client
                                        }
                                        else
                                        {
                                            if (chunkNode is JsonObject obj)
                                            {
                                                obj.Remove("usage");
                                                line = "data: " + obj.ToJsonString();
                                            }
                                        }
                                    }
                                }

                                // Accumulate text for estimation
                                var delta = chunkNode?["choices"]?[0]?["delta"];
                                if (delta != null)
                                {
                                    var content = delta["content"]?.ToString();
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        responseTextBuilder.Append(content);
                                    }
                                }
                            }
                            catch (JsonException) { }
                        }

                        if (request.OnStreamChunkAsync != null)
                        {
                            var chunkBytes = Encoding.UTF8.GetBytes(line + "\n\n");
                            await request.OnStreamChunkAsync(chunkBytes, ct);
                        }
                    }
                    sw.Stop();
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _providerService.RecordFailure(provider.Id, actualModel ?? string.Empty, 0, ex.Message);
                    await _logService.LogRequestAsync(new RequestLog
                    {
                        Timestamp = DateTime.UtcNow,
                        ProviderId = provider.Id,
                        ApiKeyId = request.ApiKeyId,
                        Success = false,
                        ErrorMessage = $"Streaming interrupted: {ex.Message}",
                        TokensIn = tokensIn,
                        TokensOut = tokensOut,
                        LatencyMs = (int)sw.ElapsedMilliseconds,
                        Model = actualModel
                    });
                    
                    result.ErrorMessage = $"Streaming interrupted: {ex.Message}";
                    return result;
                }
            }
        } // end using upstreamResponse

        // Token estimation fallback: stream completed with no usage data
        if (request.IsStream && tokensIn == null && tokensOut == null && !clientRequestedUsage)
        {
            var requestChars = finalReqBodyString.Length;
            var responseChars = responseTextBuilder.Length;
            tokensIn = Math.Max(1, requestChars / 4);
            tokensOut = Math.Max(0, responseChars / 4);
            estimated = true;
        }
        
        await _logService.LogRequestAsync(new RequestLog
        {
            Timestamp = DateTime.UtcNow,
            ProviderId = provider.Id,
            ApiKeyId = request.ApiKeyId,
            Success = true,
            ErrorMessage = null,
            TokensIn = tokensIn,
            TokensOut = tokensOut,
            LatencyMs = (int)sw.ElapsedMilliseconds,
            Model = actualModel,
            Estimated = estimated
        });

        return result;
    }
}
