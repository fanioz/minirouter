using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniRouter.Services.Translation;

/// <summary>
/// Translates an inbound Anthropic Messages request (JsonNode) into an
/// OpenAI Chat Completions request body (JsonObject). JsonNode-based so
/// wire keys stay explicit and AOT-safe (no reflection, no typed DTOs).
/// </summary>
public static class AnthropicRequestTranslator
{
    public static JsonObject ToOpenAI(JsonNode req)
    {
        var messages = new JsonArray();

        // system: string or array of {type:"text", text:"..."} blocks
        var system = req["system"];
        if (system != null)
        {
            var systemText = ExtractText(system);
            if (!string.IsNullOrEmpty(systemText))
            {
                messages.Add(new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = systemText
                });
            }
        }

        foreach (var msg in req["messages"]?.AsArray() ?? new JsonArray())
        {
            if (msg == null) continue;
            var role = msg["role"]?.ToString();
            var content = msg["content"];

            if (content == null || role == null) continue;

            if (content is JsonValue v && v.GetValueKind() == JsonValueKind.String)
            {
                messages.Add(new JsonObject
                {
                    ["role"] = role,
                    ["content"] = v.GetValue<string>()
                });
                continue;
            }

            var parts = new JsonArray();
            var toolCalls = new JsonArray();
            var toolMessages = new JsonArray();

            foreach (var block in content.AsArray())
            {
                if (block == null) continue;
                switch (block["type"]?.ToString())
                {
                    case "text":
                        parts.Add(new JsonObject
                        {
                            ["type"] = "text",
                            ["text"] = block["text"]?.ToString() ?? ""
                        });
                        break;

                    case "image":
                        var src = block["source"];
                        if (src?["type"]?.ToString() == "base64")
                        {
                            var mediaType = src?["media_type"]?.ToString() ?? "image/jpeg";
                            var data = src?["data"]?.ToString() ?? "";
                            parts.Add(new JsonObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JsonObject
                                {
                                    ["url"] = $"data:{mediaType};base64,{data}"
                                }
                            });
                        }
                        break;

                    case "tool_use":
                        toolCalls.Add(new JsonObject
                        {
                            ["id"] = block["id"]?.ToString() ?? "",
                            ["type"] = "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = block["name"]?.ToString() ?? "",
                                ["arguments"] = (block["input"] ?? new JsonObject()).ToJsonString()
                            }
                        });
                        break;

                    case "tool_result":
                        var resultContent = block["content"];
                        string resultText;
                        if (resultContent is JsonValue rv && rv.GetValueKind() == JsonValueKind.String)
                        {
                            resultText = rv.GetValue<string>();
                        }
                        else if (resultContent is JsonArray ra)
                        {
                            resultText = string.Join("\n", ra
                                .Where(b => b?["type"]?.ToString() == "text")
                                .Select(b => b?["text"]?.ToString() ?? ""));
                        }
                        else
                        {
                            resultText = resultContent?.ToJsonString() ?? "";
                        }
                        toolMessages.Add(new JsonObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = block["tool_use_id"]?.ToString() ?? "",
                            ["content"] = resultText
                        });
                        break;
                }
            }

            if (toolMessages.Count > 0)
            {
                foreach (var tm in toolMessages) messages.Add(tm);
                if (parts.Count > 0)
                {
                    messages.Add(new JsonObject
                    {
                        ["role"] = role == "assistant" ? "assistant" : "user",
                        ["content"] = parts.Count == 1 ? parts[0]?["text"]?.ToString() : parts
                    });
                }
            }
            else if (toolCalls.Count > 0)
            {
                var m = new JsonObject
                {
                    ["role"] = "assistant",
                    ["tool_calls"] = toolCalls
                };
                if (parts.Count > 0)
                {
                    m["content"] = parts.Count == 1 ? parts[0]?["text"]?.ToString() : parts;
                }
                messages.Add(m);
            }
            else if (parts.Count > 0)
            {
                messages.Add(new JsonObject
                {
                    ["role"] = role,
                    ["content"] = parts.Count == 1 && parts[0]?["type"]?.ToString() == "text"
                        ? parts[0]?["text"]?.ToString()
                        : parts
                });
            }
        }

        var openaiReq = new JsonObject
        {
            ["model"] = req["model"]?.ToString() ?? "",
            ["messages"] = messages,
            ["stream"] = req["stream"]?.GetValue<bool>() ?? false
        };

        // max_tokens is required in Anthropic; pass through, never invent a default
        if (req["max_tokens"] != null)
            openaiReq["max_tokens"] = req["max_tokens"].GetValue<int>();

        if (req["temperature"] != null)
            openaiReq["temperature"] = req["temperature"].GetValue<double>();

        if (req["stop_sequences"] is JsonArray stopSeqs && stopSeqs.Count > 0)
        {
            openaiReq["stop"] = new JsonArray(stopSeqs.Select(s => JsonValue.Create(s?.ToString())).ToArray());
        }

        if (req["tools"] is JsonArray tools && tools.Count > 0)
        {
            var openaiTools = new JsonArray();
            foreach (var tool in tools)
            {
                if (tool == null) continue;
                openaiTools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool["name"]?.ToString() ?? "",
                        ["description"] = tool["description"]?.ToString() ?? "",
                        ["parameters"] = tool["input_schema"]?.DeepClone() ?? new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject()
                        }
                    }
                });
            }
            openaiReq["tools"] = openaiTools;
        }

        var toolChoice = req["tool_choice"];
        if (toolChoice != null)
        {
            openaiReq["tool_choice"] = toolChoice["type"]?.ToString() switch
            {
                "auto" => "auto",
                "any" => "required",
                "tool" => new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject { ["name"] = toolChoice["name"]?.ToString() ?? "" }
                },
                _ => "auto"
            };
        }

        // Ask upstream for usage so terminal message_delta carries token counts
        if (openaiReq["stream"]?.GetValue<bool>() == true)
        {
            openaiReq["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        return openaiReq;
    }

    private static string ExtractText(JsonNode node)
    {
        if (node is JsonValue v && v.GetValueKind() == JsonValueKind.String)
            return v.GetValue<string>();

        if (node is JsonArray arr)
        {
            return string.Join("\n", arr
                .Where(b => b?["type"]?.ToString() == "text")
                .Select(b => b?["text"]?.ToString() ?? ""));
        }

        return "";
    }
}
