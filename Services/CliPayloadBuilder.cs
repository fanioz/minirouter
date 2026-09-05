using System.Text.Json.Nodes;
using MiniRouter.Services.Translation;

namespace MiniRouter.Services;

/// <summary>
/// Builds the OpenAI chat-completions request payload used by CLI chat mode.
/// All values are inserted through JsonNode so characters that require JSON
/// escaping (quotes, backslashes, newlines) in the model name or prompt cannot
/// produce malformed JSON. AOT-safe: no reflection, no source-gen needed.
/// </summary>
public static class CliPayloadBuilder
{
    /// <summary>
    /// Builds the <c>{ "model": ..., "messages": [{ "role": "user", "content": ... }] }</c>
    /// request body for a single-turn CLI chat request.
    /// </summary>
    public static string BuildChatPayloadJson(string model, string prompt)
    {
        // JsonArray collection initializers bind to the trim-annotated generic
        // Add<T> (IL2026/IL3050), so inserts go through AddNode instead. See #10.
        var message = new JsonObject
        {
            ["role"] = "user",
            ["content"] = prompt
        };
        var messages = new JsonArray();
        messages.AddNode(message);

        var payload = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messages
        };
        return payload.ToJsonString();
    }
}
