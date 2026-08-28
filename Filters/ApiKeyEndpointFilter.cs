using System.Net;
using MiniRouter.Models;
using MiniRouter.Services;

namespace MiniRouter.Filters;

public class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly IApiKeyService _apiKeyService;

    public ApiKeyEndpointFilter(IApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();

        // Keyless access for local connections (Playground) and Development mode.
        // Remote callers must always present a valid API key.
        if (string.IsNullOrEmpty(authHeader) && IsLocalRequest(context))
        {
            context.HttpContext.Items["ApiKeyId"] = "local";
            return await next(context);
        }

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Unauthorized();
        }

        var token = authHeader.Substring("Bearer ".Length).Trim();
        
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
