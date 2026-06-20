using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

/// <summary>
/// Runtime model metadata entry from model-list responses.
/// </summary>
/// <remarks>
/// Official reference: https://platform.openai.com/docs/api-reference/models/object
/// </remarks>
public sealed class ModelInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("object")]
    public string Object { get; init; } = "";

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; init; } = "";
}
