using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

/// <summary>
/// Function metadata for an OpenAI-compatible tool definition.
/// </summary>
/// <remarks>
/// Official reference: https://platform.openai.com/docs/guides/function-calling
/// </remarks>
public sealed class ChatCompletionToolFunction
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = default!;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("parameters")]
    public ChatCompletionToolJsonSchema Parameters { get; init; } = new();

    [JsonPropertyName("strict")]
    public bool? Strict { get; init; }
}
