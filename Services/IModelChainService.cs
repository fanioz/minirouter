using MiniRouter.Models;

namespace MiniRouter.Services;

/// <summary>
/// Service for managing model chains (named fallback sequences).
/// </summary>
public interface IModelChainService
{
    /// <summary>
    /// Loads chains from the JSON config file. Called once at startup.
    /// </summary>
    Task LoadChainsAsync();

    /// <summary>
    /// Returns all chains as an enumerable.
    /// </summary>
    IEnumerable<ModelChain> ListChains();

    /// <summary>
    /// Gets a chain by name (case-insensitive).
    /// </summary>
    ModelChain? GetChain(string name);

    /// <summary>
    /// Checks if a name exists as a chain (case-insensitive).
    /// </summary>
    bool IsChain(string name);

    /// <summary>
    /// Creates a new chain. Throws ArgumentException if name is invalid or already exists.
    /// </summary>
    Task<ModelChain> CreateChainAsync(CreateModelChainDto dto);

    /// <summary>
    /// Updates an existing chain. Throws KeyNotFoundException if not found.
    /// </summary>
    Task<ModelChain> UpdateChainAsync(string name, UpdateModelChainDto dto);

    /// <summary>
    /// Deletes a chain. Returns true if deleted, false if not found.
    /// </summary>
    Task<bool> DeleteChainAsync(string name);
}