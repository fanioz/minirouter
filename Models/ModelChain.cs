using System.Text.Json.Serialization;

namespace MiniRouter.Models;

/// <summary>
/// Represents a named model chain that maps a virtual model name to an ordered sequence
/// of real "providerId/modelId" targets. When a request uses the chain name as the model,
/// MiniRouter executes the targets as a waterfall fallback.
/// </summary>
public record ModelChain(
    string Name,
    string? Description,
    List<string> Models
)
{
    /// <summary>
    /// Single source of truth for chain-target validation: each model entry
    /// must contain exactly one '/' separator (providerId/modelName). The
    /// service and any ModelChain instance both delegate here.
    /// </summary>
    public static IEnumerable<string> ValidateTargets(IEnumerable<string>? models)
    {
        if (models == null)
            yield break;

        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model))
                yield return "Empty model target";
            else if (model.Count(c => c == '/') != 1)
                yield return $"Model target '{model}' must contain exactly one '/' (providerId/modelName)";
        }
    }

    /// <summary>
    /// Validates that each model entry contains exactly one '/' separator.
    /// </summary>
    public IEnumerable<string> ValidateTargets() => ValidateTargets(Models);
}

/// <summary>
/// DTO for creating a new model chain.
/// </summary>
public record CreateModelChainDto(
    string Name,
    string? Description,
    List<string> Models
);

/// <summary>
/// DTO for updating an existing model chain (name is read-only).
/// </summary>
public record UpdateModelChainDto(
    string? Description,
    List<string> Models
);