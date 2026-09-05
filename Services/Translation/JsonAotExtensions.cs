using System.Text.Json.Nodes;

namespace MiniRouter.Services.Translation;

/// <summary>
/// AOT/trimming-safe helpers for building JSON trees. See issue #10.
/// </summary>
public static class JsonAotExtensions
{
    /// <summary>
    /// Adds a pre-built node to a <see cref="JsonArray"/> through the non-generic
    /// collection API. Calling the generic <c>JsonArray.Add&lt;T&gt;(T)</c> with a
    /// non-primitive T (e.g. JsonObject) is annotated RequiresUnreferencedCode /
    /// RequiresDynamicCode (IL2026/IL3050) because it may construct a JsonValue via
    /// reflection. Passing the node through a <see cref="JsonNode"/>-typed parameter
    /// selects the plain collection Add, which stores the node as-is and needs no
    /// reflection or runtime code generation.
    /// </summary>
    public static void AddNode(this JsonArray array, JsonNode? node) => array.Add(node);
}
