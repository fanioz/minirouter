using MiniRouter.Models;

namespace MiniRouter.Services;

/// <summary>
/// Builds the OpenAI-format model list served by <c>GET /v1/models</c> (Story 10.1 AC 5).
/// Aggregates models from all enabled providers — prefixing <c>providerId/</c> when the
/// model id has no slash — and appends model chain names as virtual models owned by
/// "chain" (Story 13.1, issue #11). Single source of truth shared by the Program.cs
/// endpoint handler and ModelsEndpointTests.
/// </summary>
internal static class ModelsAggregator
{
    /// <summary>
    /// Aggregates provider models and chain names into an OpenAI model list,
    /// deduplicated case-insensitively by id (provider entries win over chains).
    /// </summary>
    public static OpenAiModelList Build(IEnumerable<Provider> providers, IEnumerable<ModelChain>? chains = null)
    {
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
        foreach (var c in chains ?? Enumerable.Empty<ModelChain>())
        {
            if (seen.Add(c.Name))
                data.Add(new OpenAiModelEntry(c.Name, "model", "chain"));
        }

        return new OpenAiModelList("list", data);
    }
}
