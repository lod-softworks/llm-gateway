using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

/// <summary>
/// Runtime model-list payload from OpenAI-compatible providers.
/// </summary>
/// <remarks>
/// Official reference: https://platform.openai.com/docs/api-reference/models/list
/// </remarks>
public sealed class OpenAIModelListResponse
{
    [JsonPropertyName("data")]
    public List<ModelInfo> Data { get; init; } = [];

    [JsonPropertyName("object")]
    public string Object { get; init; } = "";
}
