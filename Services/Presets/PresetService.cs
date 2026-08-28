using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MiniRouter.Models;

namespace MiniRouter.Services.Presets;

public class PresetService : IPresetService
{
    private readonly IProviderService _providerService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PresetService>? _logger;

    private static readonly HashSet<string> KnownFreeOpenCodeModels = new() { "big-pickle" };

    public PresetService(
        IProviderService providerService,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<PresetService>? logger = null)
    {
        _providerService = providerService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<PresetInfo>> GetAllAsync()
    {
        var providers = await _providerService.ListProvidersUnmaskedAsync();
        var presetCounts = providers
            .Where(p => p.PresetId != null)
            .GroupBy(p => p.PresetId)
            .ToDictionary(g => g.Key!, g => g.Count());

        return PresetCatalog.All.Select(p => new PresetInfo(
            p.Id,
            p.Name,
            p.Category,
            p.BaseUrl,
            p.ApiKeyRequired,
            p.ModelsUrl,
            p.DefaultModels,
            p.Display,
            presetCounts.TryGetValue(p.Id, out var count) ? count : 0
        )).ToList();
    }

    public async Task<Provider> EnablePresetAsync(string presetId, string? apiKey = null, string? name = null)
    {
        var preset = PresetCatalog.All.FirstOrDefault(p => p.Id == presetId)
            ?? throw new KeyNotFoundException($"Preset '{presetId}' not found");

        string effectiveKey;
        if (preset.ApiKeyRequired)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException($"Preset '{presetId}' requires an API key");
            effectiveKey = apiKey.Trim();
        }
        else
        {
            effectiveKey = preset.DefaultApiKey ?? string.Empty;
        }

        // Validate before creating: GET {baseUrl}/v1/models with Bearer key (5s timeout)
        var validationError = await ValidateConnectionAsync(preset.BaseUrl, effectiveKey);
        if (validationError != null)
            throw new InvalidOperationException(validationError);

        var id = await GenerateUniqueIdAsync(preset.Id);

        var dto = new CreateProviderDto(
            Id: id,
            Name: name?.Trim() is { Length: > 0 } n ? n : preset.Name,
            BaseUrl: preset.BaseUrl,
            ApiKey: effectiveKey,
            Enabled: true,
            Model: null,
            Models: preset.DefaultModels.Count > 0 ? preset.DefaultModels : null,
            SupportsStreamOptions: null,
            ReportsStreamUsage: null,
            PresetId: preset.Id
        );

        return await _providerService.CreateProviderAsync(dto);
    }

    public async Task<List<string>> GetSuggestedModelsAsync(string presetId)
    {
        var preset = PresetCatalog.All.FirstOrDefault(p => p.Id == presetId)
            ?? throw new KeyNotFoundException($"Preset '{presetId}' not found");

        if (preset.ModelsFilter == null)
            return preset.DefaultModels;

        var modelsUrl = preset.ModelsUrl ?? $"{preset.BaseUrl.TrimEnd('/')}/v1/models";
        var cacheKey = $"preset_models_{presetId}";
        if (_cache.TryGetValue(cacheKey, out List<string>? cached) && cached != null)
            return cached;

        var client = _httpClientFactory.CreateClient("upstream");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
        if (!string.IsNullOrEmpty(preset.DefaultApiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", preset.DefaultApiKey);
        }

        var response = await client.SendAsync(request, cts.Token);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cts.Token);

        var models = ApplyFilter(json, preset.ModelsFilter);
        _cache.Set(cacheKey, models, TimeSpan.FromMinutes(10));
        return models;
    }

    public static List<string> ApplyFilter(string modelsJson, string filterType)
    {
        var result = new List<string>();
        var node = JsonNode.Parse(modelsJson);
        if (node?["data"] is not JsonArray data)
            return result;

        foreach (var item in data)
        {
            if (item is not JsonObject obj) continue;
            var id = obj["id"]?.ToString();
            if (string.IsNullOrEmpty(id)) continue;

            switch (filterType)
            {
                case "opencode-free":
                    if (id.EndsWith("-free") || KnownFreeOpenCodeModels.Contains(id))
                        result.Add(id);
                    break;
                case "openrouter-free":
                    var prompt = obj["pricing"]?["prompt"]?.ToString();
                    var completion = obj["pricing"]?["completion"]?.ToString();
                    if (IsZeroPrice(prompt) && IsZeroPrice(completion))
                        result.Add(id);
                    break;
                default:
                    result.Add(id);
                    break;
            }
        }

        return result;
    }

    private async Task<string?> ValidateConnectionAsync(string baseUrl, string apiKey)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("upstream");
            var url = $"{baseUrl.TrimEnd('/')}/v1/models";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await client.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            return $"Connection failed with status {(int)response.StatusCode}. {Truncate(content, 200)}";
        }
        catch (TaskCanceledException)
        {
            return "Connection timed out (5s)";
        }
        catch (Exception ex)
        {
            return $"Connection failed: {ex.Message}";
        }
    }

    private async Task<string> GenerateUniqueIdAsync(string baseId)
    {
        var providers = await _providerService.ListProvidersUnmaskedAsync();
        var existingIds = providers.Select(p => p.Id).ToHashSet();

        if (!existingIds.Contains(baseId))
            return baseId;

        int suffix = 2;
        while (existingIds.Contains($"{baseId}-{suffix}"))
            suffix++;

        return $"{baseId}-{suffix}";
    }

    private static bool IsZeroPrice(string? price) =>
        decimal.TryParse(price, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            && value == 0m;

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
