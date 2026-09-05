using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniRouter.Services.Translation;

/// <summary>
/// Translates a non-streaming OpenAI Chat Completions response (JsonNode)
/// into an Anthropic Messages response envelope (JsonObject) with snake_case keys.
/// </summary>
public static class AnthropicResponseTranslator
{
    public static JsonObject ToAnthropic(JsonNode resp, string model)
    {
        var content = new JsonArray();

        var message = resp["choices"]?[0]?["message"];
        if (message != null)
        {
            var contentNode = message["content"];
            if (contentNode is JsonValue cv && cv.GetValueKind() == JsonValueKind.String)
            {
                var text = cv.GetValue<string>();
                if (!string.IsNullOrEmpty(text))
                {
                    content.AddNode(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = text
                    });
                }
            }
            else if (contentNode is JsonArray parts)
            {
                foreach (var part in parts)
                {
                    if (part?["type"]?.ToString() == "text")
                    {
                        content.AddNode(new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = part["text"]?.ToString() ?? ""
                        });
                    }
                }
            }

            if (message["tool_calls"] is JsonArray toolCalls)
            {
                foreach (var tc in toolCalls)
                {
                    if (tc == null) continue;
                    JsonNode? input = null;
                    var args = tc["function"]?["arguments"];
                    if (args is JsonValue av && av.GetValueKind() == JsonValueKind.String)
                    {
                        try { input = JsonNode.Parse(av.GetValue<string>()); }
                        catch { input = new JsonObject(); }
                    }
                    else if (args != null)
                    {
                        input = args.DeepClone();
                    }

                    content.AddNode(new JsonObject
                    {
                        ["type"] = "tool_use",
                        ["id"] = tc["id"]?.ToString() ?? "",
                        ["name"] = tc["function"]?["name"]?.ToString() ?? "",
                        ["input"] = input ?? new JsonObject()
                    });
                }
            }
        }

        var usage = resp["usage"];
        var finishReason = resp["choices"]?[0]?["finish_reason"]?.ToString();

        var id = resp["id"]?.ToString() ?? "";
        if (id.StartsWith("chatcmpl-")) id = id.Substring("chatcmpl-".Length);
        if (string.IsNullOrEmpty(id)) id = $"msg_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

        return new JsonObject
        {
            ["id"] = id,
            ["type"] = "message",
            ["role"] = "assistant",
            ["model"] = resp["model"]?.ToString() ?? model,
            ["content"] = content,
            ["stop_reason"] = MapStopReason(finishReason),
            ["stop_sequence"] = null,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = usage?["prompt_tokens"]?.GetValue<int>() ?? 0,
                ["output_tokens"] = usage?["completion_tokens"]?.GetValue<int>() ?? 0
            }
        };
    }

    public static string MapStopReason(string? finishReason) => finishReason switch
    {
        "stop" => "end_turn",
        "length" => "max_tokens",
        "tool_calls" => "tool_use",
        "content_filter" => "end_turn",
        _ => "end_turn"
    };
}
