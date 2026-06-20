using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

public sealed record class OpenAiChatMessageApiContract
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "user";

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; init; }

    [JsonPropertyName("content")]
    public JsonElement? Content { get; init; }

    [JsonPropertyName("refusal")]
    public JsonElement? Refusal { get; init; }

    [JsonPropertyName("tool_calls")]
    public JsonElement? ToolCalls { get; init; }

    [JsonPropertyName("function_call")]
    public JsonElement? FunctionCall { get; init; }

    [JsonPropertyName("audio")]
    public JsonElement? Audio { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; init; } = [];
}
