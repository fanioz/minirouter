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
    /// Validates that each model entry contains exactly one '/' separator.
    /// </summary>
    public IEnumerable<string> ValidateTargets()
    {
        if (Models == null)
            yield break;

        foreach (var model in Models)
        {
            if (string.IsNullOrWhiteSpace(model))
                yield return $"Empty model target";
            else if (!model.Contains('/'))
                yield return $"Model target '{model}' must contain '/' (providerId/modelName)";
            else if (model.Split('/').Length != 2)
                yield return $"Model target '{model}' must contain exactly one '/' separator";
        }
    }
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