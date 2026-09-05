using System.Net;
using System.Net.Http.Headers;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MiniRouter.Models;
using MiniRouter.Services;
using MiniRouter.Services.Presets;
using System.IO;
using MiniRouter.Filters;
using Spectre.Console.Cli;
using MiniRouter.Cli.Infrastructure;
using MiniRouter.Cli.Commands;
using MiniRouter.Cli.ProcessManagement;
string? cliModel = null;
string? cliPrompt = null;
bool isCliMode = false;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "-m" && i + 1 < args.Length)
    {
        cliModel = args[i + 1];
        i++;
        isCliMode = true;
    }
    else if (args[i] == "-p" && i + 1 < args.Length)
    {
        cliPrompt = args[i + 1];
        i++;
        isCliMode = true;
    }
}

if (isCliMode)
{
    if (string.IsNullOrEmpty(cliModel))
    {
        Console.WriteLine("Error: -m <model> is required in CLI mode.");
        return 1;
    }
    
    if (cliPrompt == null)
    {
        if (Console.IsInputRedirected)
        {
            using var reader = new StreamReader(Console.OpenStandardInput(), Console.InputEncoding);
            cliPrompt = await reader.ReadToEndAsync();
        }
        else
        {
            Console.WriteLine("Error: -p <prompt> or stdin pipe is required.");
            return 1;
        }
    }
    
    var cliBuilder = WebApplication.CreateBuilder(args);
    cliBuilder.Logging.ClearProviders();
    
    cliBuilder.Services.AddSingleton<IProviderService, ProviderService>();
    cliBuilder.Services.AddSingleton<IModelChainService, ModelChainService>();
    cliBuilder.Services.AddSingleton<ILogService, LogService>();
    cliBuilder.Services.AddSingleton<IApiKeyService, ApiKeyService>();
    cliBuilder.Services.AddSingleton<IProxyService, ProxyService>();
    cliBuilder.Services.AddMemoryCache();
    cliBuilder.Services.AddHttpClient("upstream");
    cliBuilder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
    });
    
    var cliApp = cliBuilder.Build();
    
    var pService = cliApp.Services.GetRequiredService<IProviderService>();
    await pService.LoadProvidersAsync();
    
    var cService = cliApp.Services.GetRequiredService<IModelChainService>();
    await cService.LoadChainsAsync();
    
    var lService = cliApp.Services.GetRequiredService<ILogService>();
    await lService.InitializeAsync();
    
    var aService = cliApp.Services.GetRequiredService<IApiKeyService>();
    await aService.InitializeAsync();
    
    var proxy = cliApp.Services.GetRequiredService<IProxyService>();
    
    // JsonNode-based payload: quotes/newlines in the model name or prompt are escaped correctly
    var payloadObjString = CliPayloadBuilder.BuildChatPayloadJson(cliModel, cliPrompt);
    
    var proxyReq = new ProxyExecutionRequest
    {
        RequestedModel = cliModel,
        RequestBodyJson = payloadObjString,
        IsStream = false,
        ContentType = "application/json"
    };
    
    var result = await proxy.ExecuteCompletionAsync(proxyReq);
    if (result.Success && result.ResponseBytes != null)
    {
        var respStr = Encoding.UTF8.GetString(result.ResponseBytes);
        try
        {
            var node = JsonNode.Parse(respStr);
            var content = node?["choices"]?[0]?["message"]?["content"]?.ToString();
            if (content != null)
            {
                Console.WriteLine(content);
            }
            else
            {
                Console.WriteLine(respStr);
            }
        }
        catch
        {
            Console.WriteLine(respStr);
        }
        return 0;
    }
    else
    {
        Console.WriteLine($"Error: {result.StatusCode} - {result.ErrorMessage}");
        if (result.ResponseBytes != null)
        {
            Console.WriteLine(Encoding.UTF8.GetString(result.ResponseBytes));
        }
        return 1;
    }
}

if (args.Length > 0)
{
    if (args[0] == "restart" || args[0] == "providers")
    {
        var registrations = new ServiceCollection();
        registrations.AddSingleton<IProviderService, ProviderService>();
        registrations.AddSingleton<ITerminalFeedback, TerminalFeedback>();
        registrations.AddSingleton<IProcessLocator, ProcessLocator>();
        registrations.AddSingleton<IServerProcessManager, ServerProcessManager>();
        
        var registrar = new TypeRegistrar(registrations);
        var appCli = new CommandApp(registrar);
        
        appCli.Configure(config => 
        {
            config.AddCommand<RestartCommand>("restart");
            config.AddBranch("providers", p => 
            {
                p.AddCommand<ProvidersListCommand>("list");
                p.AddCommand<ProvidersAddCommand>("add");
                p.AddCommand<ProvidersEditCommand>("edit");
                p.AddCommand<ProvidersDeleteCommand>("delete");
            });
        });
        
        return await appCli.RunAsync(args);
    }

    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    bool isServerRunning = false;
    try 
    {
        var res = await client.GetAsync("http://localhost:8080/health");
        isServerRunning = res.IsSuccessStatusCode;
    }
    catch { }

    if (args.Length == 1 && args[0] == "status")
    {
        if (isServerRunning) Console.WriteLine("Status: RUNNING at http://localhost:8080");
        else Console.WriteLine("Status: STOPPED");
        return 0;
    }
    else if (args.Length == 2 && args[0] == "list" && args[1] == "providers")
    {
        if (!isServerRunning)
        {
            Console.WriteLine("Server is not running. Start it first.");
            return 1;
        }
        var res = await client.GetAsync("http://localhost:8080/api/providers");
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var providers = JsonSerializer.Deserialize<List<MaskedProvider>>(json, AppJsonContext.Default.MaskedProviderList);
        
        Console.WriteLine($"{"ID",-10} | {"Name",-20} | {"Enabled",-7}");
        Console.WriteLine(new string('-', 55));
        if (providers != null)
        {
            foreach (var p in providers)
            {
                Console.WriteLine($"{p.Id,-10} | {p.Name,-20} | {p.Enabled,-7}");
            }
        }
        return 0;
    }
    else if (args.Length == 2 && args[0] == "list" && args[1] == "models")
    {
        if (!isServerRunning)
        {
            Console.WriteLine("Server is not running. Start it first.");
            return 1;
        }
        var res = await client.GetAsync("http://localhost:8080/models");
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        var aggregatedModels = JsonSerializer.Deserialize<List<AggregatedModels>>(json, AppJsonContext.Default.ListAggregatedModels);
        if (aggregatedModels != null)
        {
            foreach (var group in aggregatedModels)
            {
                Console.WriteLine($"Provider: {group.ProviderName} ({group.ProviderId}){(group.Error != null ? " - ERROR: " + group.Error : "")}");
                if (group.Models != null)
                {
                    foreach (var m in group.Models)
                    {
                        Console.WriteLine($"  - {m.Id} [{m.CircuitStatus}]");
                    }
                }
                Console.WriteLine();
            }
        }
        return 0;
    }
    else
    {
        Console.WriteLine("Usage: minirouter [status|list providers|list models]");
        return 1;
    }
}
else
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
    try 
    {
        var res = await client.GetAsync("http://localhost:8080/health");
        if (res.IsSuccessStatusCode)
        {
            Console.WriteLine("Server running at http://localhost:8080");
            return 0;
        }
    }
    catch { /* Ignore, proceed to start server */ }
}

var builder = WebApplication.CreateBuilder(args);

    // Register provider service behind its interface (required for DI resolution in endpoints)
    builder.Services.AddSingleton<IProviderService, ProviderService>();
    builder.Services.AddSingleton<IModelChainService, ModelChainService>();
    builder.Services.AddSingleton<IPresetService, PresetService>();
builder.Services.AddSingleton<ILogService, LogService>();
builder.Services.AddSingleton<IApiKeyService, ApiKeyService>();
builder.Services.AddSingleton<IProxyService, ProxyService>();
builder.Services.AddHostedService<LogRetentionService>();
builder.Services.AddMemoryCache();

// Shared HttpClient for forwarding requests (no auth headers here; per-provider key set per request)
builder.Services.AddHttpClient("upstream");

// AOT-safe: route all minimal-API JSON through the source-generated context
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Serve the pre-built Svelte SPA from wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();

// Initialize providers from JSON config file
var providerService = app.Services.GetRequiredService<IProviderService>();
await providerService.LoadProvidersAsync();

var chainService = app.Services.GetRequiredService<IModelChainService>();
await chainService.LoadChainsAsync();

var logService = app.Services.GetRequiredService<ILogService>();
await logService.InitializeAsync();

var apiKeyService = app.Services.GetRequiredService<IApiKeyService>();
await apiKeyService.InitializeAsync();

// Health check endpoint
app.MapGet("/health", () => Results.Ok());

// Provider CRUD endpoints
app.MapGet("/api/providers", async (IProviderService service) =>
{
    var providers = await service.ListProvidersAsync();
    return Results.Ok(providers);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapGet("/api/providers/{id}", async (string id, IProviderService service) =>
{
    var provider = await service.GetProviderByIdAsync(id);
    return provider is null
        ? Results.NotFound(new ErrorResponse($"Provider '{id}' not found"))
        : Results.Ok(provider);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPost("/api/providers", async (CreateProviderDto dto, IProviderService service, IMemoryCache cache) =>
{
    if (!Uri.TryCreate(dto.BaseUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        return Results.BadRequest(new ErrorResponse("Invalid baseUrl: must be a well-formed http/https URL"));
    }

    try
    {
        var provider = await service.CreateProviderAsync(dto);
        cache.Remove("aggregated_models");
        return Results.Created($"/api/providers/{provider.Id}", provider.Mask());
    }
    catch (ArgumentException ex)
    {
        return Results.Conflict(new ErrorResponse(ex.Message));
    }
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPut("/api/providers/{id}", async (string id, UpdateProviderDto dto, IProviderService service, IMemoryCache cache) =>
{
    if (!Uri.TryCreate(dto.BaseUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        return Results.BadRequest(new ErrorResponse("Invalid baseUrl: must be a well-formed http/https URL"));
    }

    try
    {
        var provider = await service.UpdateProviderAsync(id, dto);
        cache.Remove("aggregated_models");
        return Results.Ok(provider.Mask());
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapDelete("/api/providers/{id}", async (string id, IProviderService service, IMemoryCache cache) =>
{
    var deleted = await service.DeleteProviderAsync(id);
    if (deleted) cache.Remove("aggregated_models");
    return deleted ? Results.NoContent() : Results.NotFound(new ErrorResponse($"Provider '{id}' not found"));
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPost("/api/providers/test", async (TestConnectionDto dto, IHttpClientFactory httpClientFactory) =>
{
    if (!Uri.TryCreate(dto.BaseUrl, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "http" && uri.Scheme != "https"))
    {
        return Results.BadRequest(new ErrorResponse("Invalid baseUrl: must be a well-formed http/https URL"));
    }

    try
    {
        var client = httpClientFactory.CreateClient("upstream");
        var request = new HttpRequestMessage(HttpMethod.Get, $"{dto.BaseUrl.TrimEnd('/')}/v1/models");
        if (!string.IsNullOrEmpty(dto.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", dto.ApiKey);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var response = await client.SendAsync(request, cts.Token);
        
        if (response.IsSuccessStatusCode)
        {
            return Results.Ok();
        }
        else
        {
            var content = await response.Content.ReadAsStringAsync();
            return Results.BadRequest(new ErrorResponse($"Connection failed with status {response.StatusCode}. Response: {content}"));
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new ErrorResponse($"Connection failed: {ex.Message}"));
    }
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

// Preset Providers endpoints
app.MapGet("/api/presets", async (IPresetService service) =>
{
    var presets = await service.GetAllAsync();
    return Results.Ok(presets);
});

app.MapPost("/api/presets/{presetId}/enable", async (
    string presetId, 
    EnablePresetRequest request, // already populated from incoming JSON body
    IPresetService service,
    IMemoryCache cache) =>
{
    
    try
    {
        var provider = await service.EnablePresetAsync(presetId, request.ApiKey, request.Name);
        cache.Remove("aggregated_models");
        return Results.Created($"/api/providers/{provider.Id}", provider.Mask());
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse($"Missing required field: {ex.Message}"));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapGet("/api/presets/{presetId}/models", async (string presetId, IPresetService service) =>
{
    var models = await service.GetSuggestedModelsAsync(presetId);
    return Results.Ok(models);
});


// Logging and Analytics endpoints
app.MapGet("/api/logs", async (string? providerId, string? apiKeyId, string? model, bool? success, int? limit, ILogService service) =>
{
    var logs = await service.GetLogsAsync(providerId, apiKeyId, model, success, limit ?? 100);
    return Results.Ok(logs);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapGet("/api/analytics/tokens", async (string? providerId, ILogService service) =>
{
    var analytics = await service.GetAnalyticsAsync(providerId);
    return Results.Ok(analytics);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapGet("/api/analytics/keys", async (string? apiKeyId, ILogService service) =>
{
    var analytics = await service.GetKeyAnalyticsAsync(apiKeyId);
    return Results.Ok(analytics);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

// API Key Endpoints
app.MapPost("/api/keys", async (CreateApiKeyDto dto, IApiKeyService service) =>
{
    var apiKey = await service.CreateApiKeyAsync(dto);
    return Results.Created($"/api/keys/{apiKey.Id}", apiKey);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapGet("/api/keys", async (IApiKeyService service) =>
{
    var keys = await service.ListApiKeysAsync();
    return Results.Ok(keys);
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPut("/api/keys/{id}", async (string id, UpdateApiKeyDto dto, IApiKeyService service) =>
{
    try
    {
        var updated = await service.UpdateApiKeyAsync(id, dto);
        return Results.Ok(updated);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new ErrorResponse(ex.Message));
    }
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapDelete("/api/keys/{id}", async (string id, IApiKeyService service) =>
{
    var deleted = await service.DeleteApiKeyAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound(new ErrorResponse($"ApiKey '{id}' not found"));
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

// Model Chains CRUD endpoints
app.MapGet("/api/model-chains", (IModelChainService chains) =>
    Results.Json(chains.ListChains().ToList(), AppJsonContext.Default.ModelChainList))
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPost("/api/model-chains", async (CreateModelChainDto dto, IModelChainService chains) =>
{
    try
    {
        var chain = await chains.CreateChainAsync(dto);
        return Results.Created($"/api/model-chains/{chain.Name}", chain);
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(statusCode: 400, detail: ex.Message);
    }
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPut("/api/model-chains/{name}", async (string name, UpdateModelChainDto dto, IModelChainService chains, IMemoryCache cache) =>
{
    try
    {
        var chain = await chains.UpdateChainAsync(name, dto);
        cache.Remove("v1_models_openai");
        return Results.Ok(chain);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.Problem(statusCode: 404, detail: ex.Message);
    }
    catch (ArgumentException ex)
    {
        return Results.Problem(statusCode: 400, detail: ex.Message);
    }
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapDelete("/api/model-chains/{name}", async (string name, IModelChainService chains, IMemoryCache cache) =>
{
    var deleted = await chains.DeleteChainAsync(name);
    if (deleted) cache.Remove("v1_models_openai");
    return deleted ? Results.NoContent() : Results.Problem(statusCode: 404, detail: $"Chain '{name}' not found.");
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

// Round-robin proxy: POST /v1/chat/completions -> next enabled provider serving the requested model
app.MapPost("/v1/chat/completions", async (HttpContext ctx, IProxyService proxyService) =>
{
    await proxyService.HandleChatCompletionAsync(ctx);
}).AddEndpointFilter<ApiKeyEndpointFilter>();

// Anthropic Messages: POST /v1/messages -> OpenAI Chat Completions translation
app.MapPost("/v1/messages", async (HttpContext ctx, IProxyService proxyService) =>
{
    await proxyService.HandleAnthropicMessagesAsync(ctx);
}).AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapGet("/models", async (IProviderService providerService, IHttpClientFactory httpClientFactory, IMemoryCache cache, IConfiguration config) =>
{
    if (cache.TryGetValue("aggregated_models", out List<AggregatedModels>? cached) && cached != null)
    {
        return Results.Json(cached, AppJsonContext.Default.ListAggregatedModels);
    }

    var providers = await providerService.ListProvidersUnmaskedAsync();
    var enabledProviders = providers.Where(p => p.Enabled).ToList();
    
    var results = new List<AggregatedModels>();
    foreach (var provider in enabledProviders)
    {
        var agg = new AggregatedModels { ProviderId = provider.Id, ProviderName = provider.Name };
        try
        {
            var client = httpClientFactory.CreateClient("upstream");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{provider.BaseUrl.TrimEnd('/')}/v1/models");
            if (!string.IsNullOrEmpty(provider.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
            }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await client.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(json);
            var modelsArray = node?["data"] as JsonArray;
            agg.Models = new List<ModelInfo>();
            if (modelsArray != null)
            {
                foreach (var item in modelsArray)
                {
                    var id = item?["id"]?.ToString();
                    if (id != null) 
                    {
                        var status = providerService.GetCircuitStatusDisplay(provider.Id, id);
                        agg.Models.Add(new ModelInfo { Id = id, CircuitStatus = status });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            agg.Error = ex.Message;
        }
        results.Add(agg);
    }
    
    int ttlSeconds = 300;
    if (int.TryParse(config["MODELS_CACHE_TTL_SECONDS"] ?? Environment.GetEnvironmentVariable("MODELS_CACHE_TTL_SECONDS"), out int parsed))
    {
        ttlSeconds = parsed;
    }
    
    cache.Set("aggregated_models", results, TimeSpan.FromSeconds(ttlSeconds));
    
    return Results.Json(results, AppJsonContext.Default.ListAggregatedModels);
});

app.MapPost("/_shutdown", (Microsoft.Extensions.Hosting.IHostApplicationLifetime lifetime) => 
{ 
    lifetime.StopApplication(); 
    return Results.Ok(); 
})
.AddEndpointFilter<ApiKeyEndpointFilter>();

// Story 10.1: GET /v1/models — OpenAI-format flat model list for Codex CLI compatibility
// Story 13.1: Also includes model chain names as virtual models
app.MapGet("/v1/models", async (IProviderService providerService, IModelChainService chainService, IMemoryCache cache, IConfiguration config) =>
{
    const string cacheKey = "v1_models_openai";
    if (cache.TryGetValue(cacheKey, out OpenAiModelList? cachedList) && cachedList != null)
        return Results.Json(cachedList, AppJsonContext.Default.OpenAiModelList);

    var providers = await providerService.ListProvidersUnmaskedAsync();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var data = new List<OpenAiModelEntry>();

    foreach (var p in providers.Where(p => p.Enabled))
    {
        var models = p.Models ?? (p.Model != null ? new List<string> { p.Model } : new List<string>());
        foreach (var m in models)
        {
            // Prefix with providerId/ only if the model ID doesn't already contain a /
            var id = m.Contains('/') ? m : $"{p.Id}/{m}";
            if (seen.Add(id))
                data.Add(new OpenAiModelEntry(id, "model", p.Id));
        }
    }

    // Story 13.1: Append model chain names as discoverable virtual models
    foreach (var c in chainService.ListChains())
    {
        if (seen.Add(c.Name))
            data.Add(new OpenAiModelEntry(c.Name, "model", "chain"));
    }

    int ttl = 300;
    if (int.TryParse(config["MODELS_CACHE_TTL_SECONDS"] ?? Environment.GetEnvironmentVariable("MODELS_CACHE_TTL_SECONDS"), out int parsedTtl))
        ttl = parsedTtl;

    var result = new OpenAiModelList("list", data);
    cache.Set(cacheKey, result, TimeSpan.FromSeconds(ttl));
    return Results.Json(result, AppJsonContext.Default.OpenAiModelList);
});

// Story 10.1: GET /api/config/auth-passthrough — read-only, no auth filter
app.MapGet("/api/config/auth-passthrough", (IConfiguration config) =>
{
    var enabled = bool.TryParse(
        config["AUTH_PASSTHROUGH"] ?? Environment.GetEnvironmentVariable("AUTH_PASSTHROUGH"),
        out var v) && v;
    return Results.Json(new AuthPassthroughConfig(enabled), AppJsonContext.Default.AuthPassthroughConfig);
});

app.Lifetime.ApplicationStarted.Register(() => 
{
    var server = app.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
    var addressFeature = server.Features.Get<Microsoft.AspNetCore.Hosting.Server.Features.IServerAddressesFeature>();
    string address = addressFeature?.Addresses.FirstOrDefault() ?? "http://localhost:8080";
    File.WriteAllText(".minirouter.pid", $"{System.Diagnostics.Process.GetCurrentProcess().Id}|{address}");
});
app.Lifetime.ApplicationStopped.Register(() => 
{
    if (File.Exists(".minirouter.pid")) File.Delete(".minirouter.pid");
});

// Startup warning for AUTH_PASSTHROUGH mode
var authPassthroughEnabled = bool.TryParse(
    app.Configuration["AUTH_PASSTHROUGH"] ?? Environment.GetEnvironmentVariable("AUTH_PASSTHROUGH"),
    out var aptVal) && aptVal;
if (authPassthroughEnabled)
{
    Console.Error.WriteLine("[WARN] AUTH_PASSTHROUGH=true — API key validation disabled. Do not expose port publicly.");
}

app.Run();
return 0;
