using System.Text.Json.Nodes;
using MiniRouter.Services;

namespace MiniRouter.Tests;

public class CliPayloadBuilderTests
{
    [Fact]
    public void BuildChatPayloadJson_OrdinaryValues_ProduceWellFormedPayload()
    {
        var json = CliPayloadBuilder.BuildChatPayloadJson("openai/gpt-4o-mini", "hello world");

        var node = JsonNode.Parse(json);
        Assert.NotNull(node);
        Assert.Equal("openai/gpt-4o-mini", node["model"]?.ToString());
        var message = Assert.Single(node["messages"]!.AsArray());
        Assert.Equal("user", message!["role"]?.ToString());
        Assert.Equal("hello world", message["content"]?.ToString());
    }

    [Fact]
    public void BuildChatPayloadJson_QuotesInModelAndPrompt_RoundTripExactly()
    {
        // Issue #13: a '"' in the -m model argument used to break the request JSON
        var model = "provider/model-with-\"quotes\"";
        var prompt = "say \"hi\"\nnewline\ttab \\ backslash";

        var json = CliPayloadBuilder.BuildChatPayloadJson(model, prompt);

        // Throws if the payload is malformed JSON
        var node = JsonNode.Parse(json);
        Assert.NotNull(node);
        Assert.Equal(model, node["model"]?.ToString());
        Assert.Equal(prompt, node["messages"]?[0]?["content"]?.ToString());
    }

    [Fact]
    public void BuildChatPayloadJson_QuotesInModel_ModelIsNotInterpolatedRaw()
    {
        var json = CliPayloadBuilder.BuildChatPayloadJson("provider/\"quoted\"-model", "p");

        // The old bug interpolated the model into the JSON verbatim; the escaped
        // form (default encoder emits \u0022 for '"') must appear instead.
        Assert.DoesNotContain("provider/\"quoted\"-model", json);
        Assert.Contains("\\u0022quoted", json);
    }
}
