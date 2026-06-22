using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;

namespace Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;

public sealed class OpenAIModelListHttpExecutor(IHttpClientFactory httpClientFactory)
{
    static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<OpenAIModelListResponse> ListModelsAsync(
        OpenAIChatCompletionProvider provider,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage message = BuildRequest(provider);
        HttpClient client = httpClientFactory.CreateClient(OpenAIChatCompletionHttpExecutor.HttpClientName);

        using HttpResponseMessage response = await client.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body) ? "OpenAI model-list request failed." : body,
                null,
                response.StatusCode);
        }

        await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        OpenAIModelListResponse? listResponse = await JsonSerializer.DeserializeAsync<OpenAIModelListResponse>(
            responseStream,
            SerializerOptions,
            cancellationToken);

        return listResponse ?? throw new InvalidOperationException("OpenAI model-list returned an empty response.");
    }

    static HttpRequestMessage BuildRequest(OpenAIChatCompletionProvider provider)
    {
        Uri requestUri = BuildModelsUri(provider.BaseUrl);
        HttpRequestMessage message = new(HttpMethod.Get, requestUri);
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
                    // No body for GET /v1/models.
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

    internal static Uri BuildModelsUri(string baseUrl)
    {
        string trimmed = baseUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(trimmed, UriKind.Absolute), "v1/models");
    }
}
