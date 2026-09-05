using System.Text.Json;
using System.Text.Json.Nodes;
using MiniRouter.Models;
using MiniRouter.Services.Translation;

namespace MiniRouter.Tests;

/// <summary>
/// Round-trip coverage for the Anthropic &lt;-&gt; OpenAI translation layer,
/// guarding the AOT-safe JSON construction changes (issue #10).
/// </summary>
public class TranslationRoundTripTests
{
    private static JsonNode Parse(string json) => JsonNode.Parse(json)!;

    // ── Request: Anthropic -> OpenAI ─────────────────────────────────────────

    [Fact]
    public void ToOpenAI_SystemStringAndTextContent_MapToOpenAIShape()
    {
        var req = Parse("""
        {
            "model": "claude-x",
            "max_tokens": 128,
            "temperature": 0.5,
            "stream": false,
            "system": "be brief",
            "messages": [
                { "role": "user", "content": "hello" },
                { "role": "assistant", "content": [{ "type": "text", "text": "hi there" }] }
            ]
        }
        """);

        var openai = AnthropicRequestTranslator.ToOpenAI(req);

        Assert.Equal("claude-x", openai["model"]!.ToString());
        Assert.Equal(false, openai["stream"]!.GetValue<bool>());
        Assert.Equal(128, openai["max_tokens"]!.GetValue<int>());
        Assert.Equal(0.5, openai["temperature"]!.GetValue<double>());

        var messages = openai["messages"]!.AsArray();
        Assert.Equal(3, messages.Count);
        Assert.Equal("system", messages[0]!["role"]!.ToString());
        Assert.Equal("be brief", messages[0]!["content"]!.ToString());
        Assert.Equal("user", messages[1]!["role"]!.ToString());
        Assert.Equal("hello", messages[1]!["content"]!.ToString());
        // single text block collapses to a plain string content
        Assert.Equal("hi there", messages[2]!["content"]!.ToString());
    }

    [Fact]
    public void ToOpenAI_ImageBlock_MapsToDataUrl()
    {
        var req = Parse("""
        {
            "model": "claude-x",
            "messages": [
                { "role": "user", "content": [
                    { "type": "image", "source": { "type": "base64", "media_type": "image/png", "data": "QUJD" } }
                ]}
            ]
        }
        """);

        var openai = AnthropicRequestTranslator.ToOpenAI(req);
        var part = openai["messages"]![0]!["content"]![0]!;

        Assert.Equal("image_url", part["type"]!.ToString());
        Assert.Equal("data:image/png;base64,QUJD", part["image_url"]!["url"]!.ToString());
    }

    [Fact]
    public void ToOpenAI_ToolUseAndToolResult_MapToToolCallsAndToolMessages()
    {
        var req = Parse("""
        {
            "model": "claude-x",
            "messages": [
                { "role": "assistant", "content": [
                    { "type": "text", "text": "let me check" },
                    { "type": "tool_use", "id": "toolu_1", "name": "get_weather", "input": { "city": "Oslo" } }
                ]},
                { "role": "user", "content": [
                    { "type": "tool_result", "tool_use_id": "toolu_1", "content": [{ "type": "text", "text": "12 C" }] }
                ]}
            ]
        }
        """);

        var openai = AnthropicRequestTranslator.ToOpenAI(req);
        var messages = openai["messages"]!.AsArray();

        var assistant = messages[0]!;
        Assert.Equal("assistant", assistant["role"]!.ToString());
        Assert.Equal("let me check", assistant["content"]!.ToString());
        var toolCall = assistant["tool_calls"]![0]!;
        Assert.Equal("toolu_1", toolCall["id"]!.ToString());
        Assert.Equal("function", toolCall["type"]!.ToString());
        Assert.Equal("get_weather", toolCall["function"]!["name"]!.ToString());
        var args = JsonNode.Parse(toolCall["function"]!["arguments"]!.ToString())!;
        Assert.Equal("Oslo", args["city"]!.ToString());

        var tool = messages[1]!;
        Assert.Equal("tool", tool["role"]!.ToString());
        Assert.Equal("toolu_1", tool["tool_call_id"]!.ToString());
        Assert.Equal("12 C", tool["content"]!.ToString());
    }

    [Fact]
    public void ToOpenAI_ToolsAndChoice_MapSchemaAndChoice()
    {
        var req = Parse("""
        {
            "model": "claude-x",
            "stop_sequences": ["END"],
            "tools": [
                { "name": "get_weather", "description": "weather", "input_schema": { "type": "object", "properties": { "city": { "type": "string" } } } }
            ],
            "tool_choice": { "type": "tool", "name": "get_weather" },
            "messages": [{ "role": "user", "content": "w?" }]
        }
        """);

        var openai = AnthropicRequestTranslator.ToOpenAI(req);

        Assert.Equal("END", openai["stop"]![0]!.ToString());

        var fn = openai["tools"]![0]!["function"]!;
        Assert.Equal("get_weather", fn["name"]!.ToString());
        Assert.Equal("weather", fn["description"]!.ToString());
        Assert.Equal("string", fn["parameters"]!["properties"]!["city"]!["type"]!.ToString());

        var choice = openai["tool_choice"]!;
        Assert.Equal("function", choice["type"]!.ToString());
        Assert.Equal("get_weather", choice["function"]!["name"]!.ToString());
    }

    [Fact]
    public void ToOpenAI_StreamRequestsUsage()
    {
        var req = Parse("""
        { "model": "claude-x", "stream": true, "messages": [{ "role": "user", "content": "hi" }] }
        """);

        var openai = AnthropicRequestTranslator.ToOpenAI(req);

        Assert.Equal(true, openai["stream_options"]!["include_usage"]!.GetValue<bool>());
    }

    // ── Response: OpenAI -> Anthropic ────────────────────────────────────────

    [Fact]
    public void ToAnthropic_TextAndUsage_MapEnvelope()
    {
        var resp = Parse("""
        {
            "id": "chatcmpl-abc123",
            "model": "gpt-test",
            "choices": [ { "finish_reason": "stop", "message": { "role": "assistant", "content": "final answer" } } ],
            "usage": { "prompt_tokens": 11, "completion_tokens": 7 }
        }
        """);

        var anthropic = AnthropicResponseTranslator.ToAnthropic(resp, "fallback");

        Assert.Equal("abc123", anthropic["id"]!.ToString());
        Assert.Equal("message", anthropic["type"]!.ToString());
        Assert.Equal("assistant", anthropic["role"]!.ToString());
        Assert.Equal("gpt-test", anthropic["model"]!.ToString());
        Assert.Equal("text", anthropic["content"]![0]!["type"]!.ToString());
        Assert.Equal("final answer", anthropic["content"]![0]!["text"]!.ToString());
        Assert.Equal("end_turn", anthropic["stop_reason"]!.ToString());
        Assert.Null(anthropic["stop_sequence"]);
        Assert.Equal(11, anthropic["usage"]!["input_tokens"]!.GetValue<int>());
        Assert.Equal(7, anthropic["usage"]!["output_tokens"]!.GetValue<int>());
    }

    [Fact]
    public void ToAnthropic_ToolCalls_ParseArgumentsToInput()
    {
        var resp = Parse("""
        {
            "id": "chatcmpl-xyz",
            "model": "gpt-test",
            "choices": [ { "finish_reason": "tool_calls", "message": { "role": "assistant", "content": null,
                "tool_calls": [ { "id": "call_1", "type": "function",
                    "function": { "name": "get_weather", "arguments": "{\"city\":\"Oslo\"}" } } ] } } ],
            "usage": { "prompt_tokens": 3, "completion_tokens": 9 }
        }
        """);

        var anthropic = AnthropicResponseTranslator.ToAnthropic(resp, "fallback");
        var block = anthropic["content"]![0]!;

        Assert.Equal("tool_use", block["type"]!.ToString());
        Assert.Equal("call_1", block["id"]!.ToString());
        Assert.Equal("get_weather", block["name"]!.ToString());
        Assert.Equal("Oslo", block["input"]!["city"]!.ToString());
        Assert.Equal("tool_use", anthropic["stop_reason"]!.ToString());
    }

    [Theory]
    [InlineData("stop", "end_turn")]
    [InlineData("length", "max_tokens")]
    [InlineData("tool_calls", "tool_use")]
    [InlineData("content_filter", "end_turn")]
    [InlineData(null, "end_turn")]
    public void MapStopReason_MapsAllCases(string? finishReason, string expected)
    {
        Assert.Equal(expected, AnthropicResponseTranslator.MapStopReason(finishReason));
    }

    // ── Stream: OpenAI chunks -> Anthropic events ────────────────────────────

    [Fact]
    public void Stream_TextAndFinish_EmitsWellFormedEventSequence()
    {
        var state = new AnthropicStreamState();
        var events = new List<AnthropicStreamEvent>();

        foreach (var chunkJson in new[]
        {
            """{ "id": "chatcmpl-s1", "model": "gpt-test", "choices": [ { "delta": { "role": "assistant" } } ] }""",
            """{ "choices": [ { "delta": { "content": "Hel" } } ] }""",
            """{ "choices": [ { "delta": { "content": "lo" } } ] }""",
            """{ "choices": [ { "delta": {}, "finish_reason": "stop" } ], "usage": { "prompt_tokens": 4, "completion_tokens": 2 } }"""
        })
        {
            events.AddRange(AnthropicStreamTranslator.Translate(Parse(chunkJson), ref state));
        }

        var types = events.Select(e => e.Type).ToArray();
        Assert.Equal(
        [
            AnthropicStreamEventType.MessageStart,
            AnthropicStreamEventType.ContentBlockStart,
            AnthropicStreamEventType.ContentBlockDelta,
            AnthropicStreamEventType.ContentBlockDelta,
            AnthropicStreamEventType.ContentBlockStop,
            AnthropicStreamEventType.MessageDelta,
            AnthropicStreamEventType.MessageStop
        ], types);

        var start = events[0].Data;
        Assert.Equal("message_start", start["type"]!.ToString());
        Assert.Equal("msg_s1", start["message"]!["id"]!.ToString());
        Assert.Equal("gpt-test", start["message"]!["model"]!.ToString());
        Assert.Null(start["message"]!["stop_reason"]);
        Assert.Null(start["message"]!["stop_sequence"]);

        Assert.Equal("Hello", string.Concat(
            events.Where(e => e.Type == AnthropicStreamEventType.ContentBlockDelta)
                 .Select(e => e.Data["delta"]!["text"]!.ToString())));

        var delta = events.First(e => e.Type == AnthropicStreamEventType.MessageDelta).Data;
        Assert.Equal("end_turn", delta["delta"]!["stop_reason"]!.ToString());
        Assert.Equal(4, delta["usage"]!["input_tokens"]!.GetValue<int>());
        Assert.Equal(2, delta["usage"]!["output_tokens"]!.GetValue<int>());

        Assert.Equal("message_stop", events.Last().Data["type"]!.ToString());
    }

    [Fact]
    public void Stream_ToolCall_ClosesTextBlockAndEmitsInputJsonDelta()
    {
        var state = new AnthropicStreamState();
        var events = new List<AnthropicStreamEvent>();

        foreach (var chunkJson in new[]
        {
            """{ "id": "chatcmpl-t1", "model": "gpt-test", "choices": [ { "delta": { "content": "one moment" } } ] }""",
            """{ "choices": [ { "delta": { "tool_calls": [ { "index": 0, "id": "call_9", "function": { "name": "get_weather", "arguments": "" } } ] } } ] }""",
            """{ "choices": [ { "delta": { "tool_calls": [ { "index": 0, "function": { "arguments": "{\"ci" } } ] } } ] }""",
            """{ "choices": [ { "delta": { "tool_calls": [ { "index": 0, "function": { "arguments": "ty\":\"Oslo\"}" } } ] } } ] }""",
            """{ "choices": [ { "delta": {}, "finish_reason": "tool_calls" } ], "usage": { "prompt_tokens": 5, "completion_tokens": 6 } }"""
        })
        {
            events.AddRange(AnthropicStreamTranslator.Translate(Parse(chunkJson), ref state));
        }

        var types = events.Select(e => e.Type).ToArray();
        Assert.Equal(
        [
            AnthropicStreamEventType.MessageStart,
            AnthropicStreamEventType.ContentBlockStart,  // text block
            AnthropicStreamEventType.ContentBlockDelta,  // text delta
            AnthropicStreamEventType.ContentBlockStop,   // close text before tool
            AnthropicStreamEventType.ContentBlockStart,  // tool_use block
            AnthropicStreamEventType.ContentBlockDelta,  // input_json_delta "{"ci"
            AnthropicStreamEventType.ContentBlockDelta,  // input_json_delta "ty":"Oslo"}"
            AnthropicStreamEventType.ContentBlockStop,   // close tool block
            AnthropicStreamEventType.MessageDelta,
            AnthropicStreamEventType.MessageStop
        ], types);

        var toolStart = events.First(e => e.Type == AnthropicStreamEventType.ContentBlockStart && e.Data["content_block"]!["type"]!.ToString() == "tool_use").Data;
        Assert.Equal(1, toolStart["index"]!.GetValue<int>());
        Assert.Equal("call_9", toolStart["content_block"]!["id"]!.ToString());

        var bufferedArgs = string.Concat(
            events.Where(e => e.Type == AnthropicStreamEventType.ContentBlockDelta
                           && e.Data["delta"]!["type"]!.ToString() == "input_json_delta")
                 .Select(e => e.Data["delta"]!["partial_json"]!.ToString()));
        Assert.Equal("""{"city":"Oslo"}""", bufferedArgs);

        var delta = events.First(e => e.Type == AnthropicStreamEventType.MessageDelta).Data;
        Assert.Equal("tool_use", delta["delta"]!["stop_reason"]!.ToString());
        Assert.Equal(5, delta["usage"]!["input_tokens"]!.GetValue<int>());
        Assert.Equal(6, delta["usage"]!["output_tokens"]!.GetValue<int>());
    }

    [Fact]
    public void Stream_EmptyChoicesChunk_IsIgnored()
    {
        var state = new AnthropicStreamState();
        var events = AnthropicStreamTranslator.Translate(Parse("""{ "choices": [] }"""), ref state);
        Assert.Empty(events);
    }

    // ── Full round-trip ──────────────────────────────────────────────────────

    [Fact]
    public void FullRoundTrip_AnthropicRequestViaOpenAIBackToAnthropic()
    {
        var req = Parse("""
        { "model": "claude-x", "max_tokens": 64, "system": "be brief",
          "messages": [{ "role": "user", "content": "hi" }] }
        """);

        var openai = AnthropicRequestTranslator.ToOpenAI(req);
        Assert.Equal("claude-x", openai["model"]!.ToString());
        Assert.Equal("hi", openai["messages"]![1]!["content"]!.ToString());

        // A minimal OpenAI-compliant upstream response, as produced from the translated request.
        var upstreamResp = Parse($$"""
        { "id": "chatcmpl-rt1", "model": "{{openai["model"]}}",
          "choices": [ { "finish_reason": "stop",
            "message": { "role": "assistant", "content": "hello!" } } ],
          "usage": { "prompt_tokens": 1, "completion_tokens": 2 } }
        """);

        var anthropic = AnthropicResponseTranslator.ToAnthropic(upstreamResp, "claude-x");

        Assert.Equal("rt1", anthropic["id"]!.ToString());
        Assert.Equal("message", anthropic["type"]!.ToString());
        Assert.Equal("hello!", anthropic["content"]![0]!["text"]!.ToString());
        Assert.Equal("end_turn", anthropic["stop_reason"]!.ToString());
        Assert.Equal(1, anthropic["usage"]!["input_tokens"]!.GetValue<int>());
        Assert.Equal(2, anthropic["usage"]!["output_tokens"]!.GetValue<int>());
    }
}
