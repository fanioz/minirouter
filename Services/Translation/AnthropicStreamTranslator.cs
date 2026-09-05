using System.Text.Json;
using System.Text.Json.Nodes;
using MiniRouter.Models;

namespace MiniRouter.Services.Translation;

public static class AnthropicStreamTranslator
{
    /// <summary>
    /// Synthesize Anthropic SSE events from OpenAI Chat Completions chunks
    /// </summary>
    public static List<AnthropicStreamEvent> Translate(JsonNode chunk, ref AnthropicStreamState state)
    {
        var results = new List<AnthropicStreamEvent>();

        if (chunk is not JsonObject obj || !TryGetArray(obj, "choices", out var choices) || choices.Count == 0)
            return results;

        var choice = choices[0];
        if (choice is null) return results;
        var deltaNode = choice["delta"];

        // First chunk: emit message_start before any content
        if (!state.MessageStartSent)
        {
            state.MessageStartSent = true;
            state.MessageId ??= ReplaceChatCmplId(obj["id"]?.ToString());
            state.Model = obj["model"]?.ToString() ?? "unknown";

            var msgData = new JsonObject
            {
                ["type"] = "message_start",
                ["message"] = new JsonObject
                {
                    ["id"] = state.MessageId,
                    ["type"] = "message",
                    ["role"] = "assistant",
                    ["model"] = state.Model,
                    ["content"] = new JsonArray(),
                    ["stop_reason"] = null,
                    ["stop_sequence"] = null,
                    ["usage"] = new JsonObject { ["input_tokens"] = 0, ["output_tokens"] = 0 }
                }
            };
            results.Add(new(AnthropicStreamEventType.MessageStart, msgData));
        }

        // Handle text content
        if (deltaNode != null && TryGetObject(deltaNode, "content") is var contentObj)
        {
            var contentStr = contentObj?.ToString();
            if (!string.IsNullOrEmpty(contentStr))
            {
                if (!state.TextBlockStarted)
                {
                    state.TextBlockStarted = true;
                    state.TextBlockIndex = state.NextContentBlockIndex++;

                    var blockStart = new JsonObject
                    {
                        ["type"] = "content_block_start",
                        ["index"] = state.TextBlockIndex,
                        ["content_block"] = new JsonObject { ["type"] = "text", ["text"] = "" }
                    };
                    results.Add(new(AnthropicStreamEventType.ContentBlockStart, blockStart));
                }

                var deltaData = new JsonObject
                {
                    ["type"] = "content_block_delta",
                    ["index"] = state.TextBlockIndex,
                    ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = contentStr }
                };
                results.Add(new(AnthropicStreamEventType.ContentBlockDelta, deltaData));
            }
        }

        // Handle tool calls
        if (deltaNode is JsonObject deltaObj && TryGetArray(deltaObj, "tool_calls", out var tcArray))
        {
            foreach (var tc in tcArray)
            {
                if (tc is not JsonObject tcObj) continue;

                var idx = GetValue<int>(tcObj, "index") ?? 0;
                var callId = tcObj["id"]?.ToString();

                if (callId != null && !state.ToolCalls.ContainsKey(idx))
                {
                    if (state.TextBlockStarted)
                    {
                        state.TextBlockStarted = false;
                        results.Add(new(AnthropicStreamEventType.ContentBlockStop, new JsonObject { ["type"] = "content_block_stop", ["index"] = state.TextBlockIndex }));
                    }

                    var toolBlockIdx = state.NextContentBlockIndex++;
                    var toolName = tcObj["function"]?["name"]?.ToString() ?? "";
                    state.ToolCalls[idx] = new ToolCallInfo(callId, toolName, toolBlockIdx, "");

                    var toolStart = new JsonObject
                    {
                        ["type"] = "content_block_start",
                        ["index"] = toolBlockIdx,
                        ["content_block"] = new JsonObject
                        {
                            ["type"] = "tool_use",
                            ["id"] = callId,
                            ["name"] = toolName,
                            ["input"] = new JsonObject()
                        }
                    };
                    results.Add(new(AnthropicStreamEventType.ContentBlockStart, toolStart));
                }

                if (tcObj["function"]?["arguments"] is JsonValue argsVal)
                {
                    var args = argsVal.ToString();
                    if (!string.IsNullOrEmpty(args) && state.ToolCalls.TryGetValue(idx, out var tcInfo))
                    {
                        tcInfo = tcInfo with { ArgBuffer = tcInfo.ArgBuffer + args };
                        state.ToolCalls[idx] = tcInfo;

                        var jsonDelta = new JsonObject
                        {
                            ["type"] = "content_block_delta",
                            ["index"] = tcInfo.BlockIndex,
                            ["delta"] = new JsonObject { ["type"] = "input_json_delta", ["partial_json"] = args }
                        };
                        results.Add(new(AnthropicStreamEventType.ContentBlockDelta, jsonDelta));
                    }
                }
            }
        }

        // Track usage
        if (obj["usage"] is JsonObject usageObj)
        {
            state.UsagePromptTokens = GetValue<int>(usageObj, "prompt_tokens");
            state.UsageCompletionTokens = GetValue<int>(usageObj, "completion_tokens");
        }

        // Handle finish reason - close all blocks and emit terminal events
        var finishReasonNode = choice["finish_reason"];
        if (finishReasonNode is not null)
        {
            if (state.TextBlockStarted)
            {
                state.TextBlockStarted = false;
                results.Add(new(AnthropicStreamEventType.ContentBlockStop, new JsonObject { ["type"] = "content_block_stop", ["index"] = state.TextBlockIndex }));
            }

            foreach (var tc in state.ToolCalls.Values)
            {
                // Note: every arguments fragment is already emitted as an
                // input_json_delta when it arrives; re-sending the accumulated
                // buffer here would duplicate partial_json on the client and
                // corrupt the assembled tool input. Just close the block.
                results.Add(new(AnthropicStreamEventType.ContentBlockStop, new JsonObject { ["type"] = "content_block_stop", ["index"] = tc.BlockIndex }));
            }

            var stopReason = MapFinishReason(finishReasonNode.ToString());
            var finalUsage = new JsonObject { ["input_tokens"] = state.UsagePromptTokens ?? 0, ["output_tokens"] = state.UsageCompletionTokens ?? 0 };

            var messageDelta = new JsonObject
            {
                ["type"] = "message_delta",
                ["delta"] = new JsonObject { ["stop_reason"] = stopReason },
                ["usage"] = finalUsage
            };
            results.Add(new(AnthropicStreamEventType.MessageDelta, messageDelta));

            results.Add(new(AnthropicStreamEventType.MessageStop, new JsonObject { ["type"] = "message_stop" }));
        }

        return results;
    }

    private static string MapFinishReason(string reason) => reason switch
    {
        "stop" => "end_turn",
        "length" => "max_tokens",
        "tool_calls" => "tool_use",
        _ => "end_turn"
    };

    private static bool TryGetArray(JsonObject o, string key, out JsonArray arr)
    {
        if (o[key] is JsonArray jarr)
        {
            arr = jarr;
            return true;
        }
        arr = null!;
        return false;
    }

    private static JsonNode? TryGetObject(JsonNode node, string key) => node[key];

    private static T? GetValue<T>(JsonObject o, string key) where T : struct => o[key]?.GetValue<T?>();

    private static string ReplaceChatCmplId(string? id) => id?.Replace("chatcmpl-", "msg_") ?? $"msg_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

    public static string MapEventType(AnthropicStreamEventType type) => type switch
    {
        AnthropicStreamEventType.MessageStart => "message_start",
        AnthropicStreamEventType.ContentBlockStart => "content_block_start",
        AnthropicStreamEventType.ContentBlockDelta => "content_block_delta",
        AnthropicStreamEventType.ContentBlockStop => "content_block_stop",
        AnthropicStreamEventType.MessageDelta => "message_delta",
        AnthropicStreamEventType.MessageStop => "message_stop",
        AnthropicStreamEventType.Ping => "ping",
        AnthropicStreamEventType.Error => "error",
        _ => "unknown"
    };

}
