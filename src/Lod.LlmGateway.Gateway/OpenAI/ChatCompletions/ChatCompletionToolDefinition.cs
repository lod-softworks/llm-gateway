using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

/// <summary>
/// Tool definition payload for OpenAI-compatible tool calling.
/// </summary>
/// <remarks>
/// Official reference: https://platform.openai.com/docs/api-reference/chat/create
/// </remarks>
public sealed class ChatCompletionToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";

    [JsonPropertyName("function")]
    public ChatCompletionToolFunction? Function { get; init; }
}
