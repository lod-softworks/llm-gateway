using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;

public sealed record class ChatCompletionUsage
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalProperties { get; init; } = [];

    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }

    [JsonPropertyName("prompt_tokens_details")]
    public JsonElement? PromptTokensDetails { get; init; }

    [JsonPropertyName("completion_tokens_details")]
    public JsonElement? CompletionTokensDetails { get; init; }
}
