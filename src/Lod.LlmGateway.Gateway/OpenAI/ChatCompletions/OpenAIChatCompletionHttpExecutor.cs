using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

/// <summary>
/// Performs OpenAI-compatible chat completion HTTP calls for one provider.
/// </summary>
public sealed class OpenAIChatCompletionHttpExecutor(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "OpenAI";

    static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ChatCompletionResponse> CreateChatCompletionAsync(
        OpenAIChatCompletionProvider provider,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        string model = !string.IsNullOrWhiteSpace(provider.Model) ? provider.Model : request.Model;
        JsonObject payload = ChatCompletionRequestPayloadBuilder.BuildOutboundPayload(
            request,
            model,
            stream: false,
            copyMaxCompletionTokensToMaxTokens: provider.CopyMaxCompletionTokensToMaxTokens);
        using HttpRequestMessage message = BuildRequest(provider, payload);
        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        using HttpResponseMessage response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body) ? "OpenAI fallback request failed." : body,
                null,
                response.StatusCode);
        }

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        ChatCompletionResponse? completionResponse = await JsonSerializer.DeserializeAsync<ChatCompletionResponse>(
            responseStream,
            SerializerOptions,
            cancellationToken);

        return completionResponse ?? throw new InvalidOperationException("OpenAI fallback returned an empty response.");
    }

    public async IAsyncEnumerable<string> CreateChatCompletionStreamAsync(
        OpenAIChatCompletionProvider provider,
        ChatCompletionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string model = !string.IsNullOrWhiteSpace(provider.Model) ? provider.Model : request.Model;
        JsonObject payload = ChatCompletionRequestPayloadBuilder.BuildOutboundPayload(
            request,
            model,
            stream: true,
            copyMaxCompletionTokensToMaxTokens: provider.CopyMaxCompletionTokensToMaxTokens);
        using HttpRequestMessage message = BuildRequest(provider, payload);
        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        using HttpResponseMessage response = await client.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body) ? "OpenAI fallback stream request failed." : body,
                null,
                response.StatusCode);
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream, Encoding.UTF8);

        while (true)
        {
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            string payloadLine = line[6..].Trim();
            yield return payloadLine;

            if (string.Equals(payloadLine, "[DONE]", StringComparison.Ordinal))
            {
                yield break;
            }
        }
    }

    static HttpRequestMessage BuildRequest(OpenAIChatCompletionProvider provider, JsonObject payload)
    {
        Uri requestUri = BuildChatCompletionsUri(provider.BaseUrl);
        string json = payload.ToJsonString(ChatCompletionRequestPayloadBuilder.OutboundSerializerOptions);
        HttpRequestMessage message = new(HttpMethod.Post, requestUri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        ApplyProviderHeaders(message, provider);
        return message;
    }

    internal static void ApplyProviderHeaders(HttpRequestMessage message, OpenAIChatCompletionProvider provider)
    {
        if (provider.Headers is not null)
        {
            foreach (KeyValuePair<string, string> pair in provider.Headers)
            {
                string name = pair.Key.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string value = pair.Value ?? "";

                if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    if (AuthenticationHeaderValue.TryParse(value, out AuthenticationHeaderValue? parsed))
                    {
                        message.Headers.Authorization = parsed;
                    }
                    else
                    {
                        message.Headers.TryAddWithoutValidation(name, value);
                    }
                }
                else if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    // JsonContent owns Content-Type on the body.
                }
                else
                {
                    message.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(provider.AuthToken) is false)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.AuthToken);
        }
    }

    internal static Uri BuildChatCompletionsUri(string baseUrl)
    {
        string trimmed = baseUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(trimmed, UriKind.Absolute), "v1/chat/completions");
    }
}
