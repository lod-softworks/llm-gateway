using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;

namespace Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;

public static class ChatCompletionRequestPayloadBuilder
{
    public static readonly JsonSerializerOptions OutboundSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonObject BuildOutboundPayload(
        ChatCompletionRequest request,
        string resolvedModel,
        bool stream,
        bool copyMaxCompletionTokensToMaxTokens = false)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(request, OutboundSerializerOptions);
        if (node is not JsonObject root)
        {
            throw new InvalidOperationException("Chat completion request did not serialize to a JSON object.");
        }

        root["model"] = JsonValue.Create(resolvedModel);
        root["stream"] = JsonValue.Create(stream);

        if (copyMaxCompletionTokensToMaxTokens
            && root.TryGetPropertyValue("max_completion_tokens", out JsonNode? maxCompletion)
            && maxCompletion is not null)
        {
            if (!HasNonNullMaxTokens(root))
            {
                root["max_tokens"] = maxCompletion.DeepClone();
            }
        }

        return root;
    }

    static bool HasNonNullMaxTokens(JsonObject root)
    {
        if (!root.TryGetPropertyValue("max_tokens", out JsonNode? maxTokens) || maxTokens is null)
        {
            return false;
        }

        if (maxTokens is JsonValue jv)
        {
            return jv.GetValueKind() is not System.Text.Json.JsonValueKind.Null
                and not System.Text.Json.JsonValueKind.Undefined;
        }

        return true;
    }
}
