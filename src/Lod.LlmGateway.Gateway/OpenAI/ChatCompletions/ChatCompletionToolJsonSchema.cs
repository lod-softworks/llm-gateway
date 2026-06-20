using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

/// <summary>
/// Permissive JSON schema model used for tool parameters.
/// </summary>
/// <remarks>
/// Official reference: https://platform.openai.com/docs/guides/function-calling
/// </remarks>
public sealed class ChatCompletionToolJsonSchema
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; init; }

    [JsonPropertyName("properties")]
    public Dictionary<string, ChatCompletionToolJsonSchema> Properties { get; init; } = [];

    [JsonPropertyName("items")]
    public ChatCompletionToolJsonSchema? Items { get; init; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; init; }

    // Supports bool | object in provider schemas.
    [JsonPropertyName("additionalProperties")]
    public JsonElement? AdditionalProperties { get; init; }
}
