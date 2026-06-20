using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;
using Xunit;

namespace Lod.LlmGateway.Gateway.Tests;

public sealed class ChatCompletionRequestPayloadBuilderTests
{
    [Fact]
    public void BuildOutboundPayload_IncludesToolChoiceAndResponseFormat()
    {
        JsonElement toolChoice = JsonDocument.Parse("\"auto\"").RootElement;
        JsonElement responseFormat = JsonDocument.Parse("""{"type":"json_object"}""").RootElement;
        ChatCompletionRequest request = new()
        {
            Model = "any",
            Messages = JsonDocument.Parse("""[{"role":"user","content":"hi"}]""").RootElement,
            Stream = true,
            ToolChoice = toolChoice,
            ResponseFormat = responseFormat
        };

        JsonObject payload = ChatCompletionRequestPayloadBuilder.BuildOutboundPayload(request, "resolved-model", stream: false);

        Assert.Equal("resolved-model", payload["model"]!.GetValue<string>());
        Assert.False(payload["stream"]!.GetValue<bool>());
        Assert.Equal("auto", payload["tool_choice"]!.GetValue<string>());
        Assert.Equal("json_object", payload["response_format"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void BuildOutboundPayload_ExtensionMetadataPreserved()
    {
        JsonElement custom = JsonDocument.Parse("42").RootElement;
        ChatCompletionRequest request = new()
        {
            Model = "m",
            Messages = JsonDocument.Parse("[]").RootElement,
            Stream = false,
            AdditionalProperties = new Dictionary<string, JsonElement> { ["custom_field"] = custom }
        };

        JsonObject payload = ChatCompletionRequestPayloadBuilder.BuildOutboundPayload(request, "m", stream: true);

        Assert.Equal(42, payload["custom_field"]!.GetValue<int>());
        Assert.True(payload["stream"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildOutboundPayload_MirrorsMaxCompletionToMaxTokensWhenEnabled()
    {
        ChatCompletionRequest request = new()
        {
            Model = "m",
            Messages = JsonDocument.Parse("[]").RootElement,
            Stream = false,
            MaxCompletionTokens = 128
        };

        JsonObject payload = ChatCompletionRequestPayloadBuilder.BuildOutboundPayload(
            request,
            "m",
            stream: false,
            copyMaxCompletionTokensToMaxTokens: true);

        Assert.Equal(128, payload["max_tokens"]?.GetValue<int>());
        Assert.Equal(128, payload["max_completion_tokens"]?.GetValue<int>());
    }

    [Fact]
    public void BuildOutboundPayload_DoesNotOverwriteExplicitMaxTokensWhenEnabled()
    {
        ChatCompletionRequest request = new()
        {
            Model = "m",
            Messages = JsonDocument.Parse("[]").RootElement,
            Stream = false,
            MaxTokens = 10,
            MaxCompletionTokens = 200
        };

        JsonObject payload = ChatCompletionRequestPayloadBuilder.BuildOutboundPayload(
            request,
            "m",
            stream: false,
            copyMaxCompletionTokensToMaxTokens: true);

        Assert.Equal(10, payload["max_tokens"]?.GetValue<int>());
        Assert.Equal(200, payload["max_completion_tokens"]?.GetValue<int>());
    }

    [Fact]
    public void BuildOutboundPayload_RoundTripsRawClientJson_PreservesTopLevelAndExtensionKeys()
    {
        string body = """
        {
            "model": "client-model",
            "messages": [{"role":"user","content":"ping"}],
            "stream": true,
            "max_completion_tokens": 99,
            "response_format": {"type":"json_object"},
            "frequency_penalty": 0.5,
            "custom_vendor_flag": true
        }
        """;

        JsonSerializerOptions deserializeOptions = new() { PropertyNameCaseInsensitive = false };
        ChatCompletionRequest? request = JsonSerializer.Deserialize<ChatCompletionRequest>(body, deserializeOptions);
        Assert.NotNull(request);
        Assert.True(request.AdditionalProperties.ContainsKey("custom_vendor_flag"));
        Assert.Equal(0.5, request.FrequencyPenalty);

        JsonObject payload = ChatCompletionRequestPayloadBuilder.BuildOutboundPayload(
            request,
            "up-model",
            stream: true,
            copyMaxCompletionTokensToMaxTokens: true);

        Assert.Equal("up-model", payload["model"]!.GetValue<string>());
        Assert.True(payload["stream"]!.GetValue<bool>());
        Assert.Equal(99, payload["max_completion_tokens"]?.GetValue<int>());
        Assert.Equal(99, payload["max_tokens"]?.GetValue<int>());
        Assert.Equal("json_object", payload["response_format"]!["type"]!.GetValue<string>());
        Assert.Equal(0.5, payload["frequency_penalty"]!.GetValue<double>());
        Assert.True(payload["custom_vendor_flag"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildOutboundPayload_DoesNotMirrorMaxCompletionToMaxTokensWhenDisabled()
    {
        ChatCompletionRequest request = new()
        {
            Model = "m",
            Messages = JsonDocument.Parse("[]").RootElement,
            MaxCompletionTokens = 64
        };

        JsonObject payload = ChatCompletionRequestPayloadBuilder.BuildOutboundPayload(
            request,
            "m",
            stream: false,
            copyMaxCompletionTokensToMaxTokens: false);

        Assert.Equal(64, payload["max_completion_tokens"]?.GetValue<int>());
        Assert.False(payload.ContainsKey("max_tokens"));
    }
}
