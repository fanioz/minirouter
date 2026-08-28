namespace MiniRouter.Services.Presets;

public enum PresetCategory
{
    Free,
    ApiKey
}

public record PresetDisplay(
    string ColorHex,
    string TextIcon,
    string? WebsiteUrl = null,
    string? ApiKeyUrl = null,
    string? Notice = null
);

/// <summary>
/// A curated provider template. Enabling a preset creates a regular provider
/// pre-filled from these values; multiple instances of the same preset are allowed.
/// </summary>
public record ProviderPreset(
    string Id,
    string Name,
    PresetCategory Category,
    string BaseUrl,
    bool ApiKeyRequired,
    string? DefaultApiKey,
    string? ModelsUrl,
    string? ModelsFilter,
    List<string> DefaultModels,
    PresetDisplay Display
);

/// <summary>
/// Preset plus the number of providers already created from it.
/// </summary>
public record PresetInfo(
    string Id,
    string Name,
    PresetCategory Category,
    string BaseUrl,
    bool ApiKeyRequired,
    string? ModelsUrl,
    List<string> DefaultModels,
    PresetDisplay Display,
    int ConnectedCount
);

public record EnablePresetRequest(string? ApiKey, string? Name);
