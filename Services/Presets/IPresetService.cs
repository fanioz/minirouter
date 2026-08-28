using MiniRouter.Models;

namespace MiniRouter.Services.Presets;

public interface IPresetService
{
    Task<List<PresetInfo>> GetAllAsync();
    Task<Provider> EnablePresetAsync(string presetId, string? apiKey = null, string? name = null);
    Task<List<string>> GetSuggestedModelsAsync(string presetId);
}
