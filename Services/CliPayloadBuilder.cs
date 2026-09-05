using System.Text.Json.Nodes;

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
        var payload = new JsonObject
        {
            ["model"] = model,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = prompt
                }
            }
        };
        return payload.ToJsonString();
    }
}
