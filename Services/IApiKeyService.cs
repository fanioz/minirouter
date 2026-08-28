using MiniRouter.Models;

namespace MiniRouter.Services;

public interface IApiKeyService
{
    Task InitializeAsync();
    Task<CreateApiKeyResponse> CreateApiKeyAsync(CreateApiKeyDto dto);
    Task<IEnumerable<ApiKey>> ListApiKeysAsync();
    Task<ApiKey?> GetApiKeyByIdAsync(string id);
    Task<ApiKey> UpdateApiKeyAsync(string id, UpdateApiKeyDto dto);
    Task<bool> DeleteApiKeyAsync(string id);
    Task<ApiKey?> ValidateAndRecordUsageAsync(string plaintextKey);
}
