using System.Net;
using Microsoft.Extensions.Configuration;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Filters;

public class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly IApiKeyService _apiKeyService;
    private readonly IConfiguration _config;

    public ApiKeyEndpointFilter(IApiKeyService apiKeyService, IConfiguration config)
    {
        _apiKeyService = apiKeyService;
        _config = config;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // AUTH_PASSTHROUGH=true: skip all key validation (local dev / tool integration escape hatch)
        var isPassthrough = bool.TryParse(
            _config["AUTH_PASSTHROUGH"] ?? Environment.GetEnvironmentVariable("AUTH_PASSTHROUGH"),
            out var pt) && pt;

        if (isPassthrough)
        {
            context.HttpContext.Items["ApiKeyId"] = "passthrough";
            return await next(context);
        }

        var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();
        // Claude Code sends x-api-key instead of Authorization: Bearer
        var xApiKey = context.HttpContext.Request.Headers["x-api-key"].ToString();

        // Keyless access for local connections (Playground) and Development mode.
        // Remote callers must always present a valid API key.
        if (string.IsNullOrEmpty(authHeader) && string.IsNullOrEmpty(xApiKey) && IsLocalRequest(context))
        {
            context.HttpContext.Items["ApiKeyId"] = "local";
            return await next(context);
        }

        // Resolve token: prefer Authorization: Bearer, fall back to x-api-key header
        string? token = null;
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader.Substring("Bearer ".Length).Trim();
        }
        else if (!string.IsNullOrEmpty(xApiKey))
        {
            token = xApiKey.Trim();
        }

        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        var apiKey = await _apiKeyService.ValidateAndRecordUsageAsync(token);

        if (apiKey == null || !apiKey.Enabled)
        {
            return Results.Unauthorized();
        }

        context.HttpContext.Items["ApiKeyId"] = apiKey.Id;

        return await next(context);
    }

    private static bool IsLocalRequest(EndpointFilterInvocationContext context)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            return true;

        var remoteIp = context.HttpContext.Connection.RemoteIpAddress;
        return remoteIp != null && IPAddress.IsLoopback(remoteIp);
    }
}
