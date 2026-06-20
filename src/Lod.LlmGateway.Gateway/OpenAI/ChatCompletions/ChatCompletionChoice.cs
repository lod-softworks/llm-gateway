using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

public sealed record class ChatCompletionChoice
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; init; } = [];

    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("message")]
    public JsonElement? Message { get; init; }

    [JsonPropertyName("delta")]
    public JsonElement? Delta { get; init; }

    [JsonPropertyName("logprobs")]
    public JsonElement? Logprobs { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}
