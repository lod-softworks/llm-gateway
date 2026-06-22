using Lod.LlmGateway.Gateway.Api;
using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;
using Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;
using System.Text;
using System.Text.Json;

namespace Lod.LlmGateway.Gateway.Handlers.OpenAI.ChatCompletions;

public sealed class OpenAIChatCompletionHandler(
    ILogger<OpenAIChatCompletionHandler> logger,
    OpenAIChatCompletionProviderChainService providerChain,
    ApiKeyAuthorizer apiKeyAuthorizer,
    OpenAIChatCompletionTelemetryWriter telemetryWriter)
{
    public async Task<IResult> HandleAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        Guid requestId = Guid.NewGuid();
        DateTimeOffset requestReceivedUtc = DateTimeOffset.UtcNow;

        if (!apiKeyAuthorizer.IsClientAuthorized(httpContext))
        {
            logger.LogWarning("Client request connection rejected: missing or invalid API key header. Client provided key '{Provided}' does not match configured key '{Configured}'.",
                ApiKeyAuthorizer.ObfuscateKey(ApiKeyAuthorizer.GetApiKey(httpContext) ?? ""),
                string.Join(", ", apiKeyAuthorizer.ClientObfuscatedKeys));

            return GatewayResults.OpenAIError(
                StatusCodes.Status401Unauthorized,
                "Missing or invalid API key.",
                type: "authentication_error");
        }

        string? clientName = apiKeyAuthorizer.GetAuthorizedClientName(httpContext);

        (ChatCompletionRequest? request, IResult? validationError) = await ValidateRequestAsync(httpContext, requestId, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        IReadOnlyList<OpenAIChatCompletionProvider> chain = providerChain.BuildMatchingChain(request!.Model);
        if (chain.Count == 0)
        {
            const string noMatchMessage = "No OpenAI chat completion provider matches the requested model.";
            await telemetryWriter.WriteRequest(
                new OpenAIChatCompletionRequestTelemetry(
                    requestId.ToString("n"),
                    clientName,
                    requestReceivedUtc,
                    null,
                    DateTimeOffset.UtcNow,
                    ConfiguredModel: null,
                    RequestModel: request.Model,
                    ResponseFallbackModel: null,
                    ResponseModel: null,
                    Streamed: request.Stream,
                    FailoverUsed: false,
                    HttpStatusCode: StatusCodes.Status400BadRequest,
                    Error: noMatchMessage,
                    ProviderChainTelemetry: null),
                cancellationToken);

            return GatewayResults.OpenAIError(
                StatusCodes.Status400BadRequest,
                noMatchMessage,
                type: "invalid_request_error");
        }

        return request.Stream
            ? HandleStreamResponse(requestId, clientName, request, chain, requestReceivedUtc, cancellationToken)
            : await HandleNonStreamResponseAsync(requestId, clientName, request, chain, requestReceivedUtc, cancellationToken);
    }

    private async Task<(ChatCompletionRequest? Request, IResult? Error)> ValidateRequestAsync(
        HttpContext httpContext, Guid requestId, CancellationToken cancellationToken)
    {
        using StreamReader requestReader = new(httpContext.Request.Body);
        string requestText = await requestReader.ReadToEndAsync(cancellationToken);

        ChatCompletionRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<ChatCompletionRequest>(requestText);
        }
        catch (JsonException)
        {
            return (null, GatewayResults.OpenAIError(
                StatusCodes.Status400BadRequest,
                "Invalid chat completion request.",
                type: "invalid_request_error"));
        }

        if (request is null)
        {
            return (null, GatewayResults.OpenAIError(
                StatusCodes.Status400BadRequest,
                "Invalid chat completion request.",
                type: "invalid_request_error"));
        }
        else if (request.Messages.ValueKind != JsonValueKind.Array || request.Messages.GetArrayLength() == 0)
        {
            return (null, GatewayResults.OpenAIError(
                StatusCodes.Status400BadRequest,
                "No messages received in the request.",
                type: "invalid_request_error"));
        }
        else if (logger.IsEnabled(LogLevel.Trace))
        {
            logger.LogTrace("Received chat completion request ({Id}). Request: {Request}",
                requestId, requestText);
        }

        return (request, null);
    }

    private IResult HandleStreamResponse(
        Guid requestId, string? clientName, ChatCompletionRequest request,
        IReadOnlyList<OpenAIChatCompletionProvider> chain, DateTimeOffset requestReceivedUtc,
        CancellationToken cancellationToken)
    {
        return Results.Stream(
            async (outputStream) =>
            {
                DateTimeOffset requestSentUtc = DateTimeOffset.UtcNow;
                string? streamError = null;
                DateTimeOffset? firstChunkSentUtc = null;
                DateTimeOffset? finalChunkSentUtc = null;
                int streamChunkCount = 0;
                ChatCompletionUsage? streamUsage = null;
                string? rawStreamUsageJson = null;
                OpenAIChatCompletionChainStreamTelemetryCapture capture = new();
                try
                {
                    await foreach (string chunkData in providerChain.StreamWithChainAsync(
                        chain,
                        request,
                        capture,
                        cancellationToken))
                    {
                        capture.ResponseModel = ResolveStreamResponseModel(chunkData, capture.ResponseModel);
                        StreamUsageCapture usageCapture = ResolveStreamUsage(chunkData, streamUsage, rawStreamUsageJson);
                        streamUsage = usageCapture.Usage;
                        rawStreamUsageJson = usageCapture.RawUsageJson;
                        if (!string.Equals(chunkData, "[DONE]", StringComparison.Ordinal))
                        {
                            streamChunkCount++;
                            firstChunkSentUtc ??= DateTimeOffset.UtcNow;
                        }

                        string line = $"data: {chunkData}\n\n";
                        await outputStream.WriteAsync(Encoding.UTF8.GetBytes(line), cancellationToken);
                        await outputStream.FlushAsync(cancellationToken);
                        finalChunkSentUtc = DateTimeOffset.UtcNow;

                        if (logger.IsEnabled(LogLevel.Trace))
                        {
                            logger.LogTrace("Response chunk for completion request ({Id}): {Chunk}", requestId, line);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "An exception occurred while streaming chat completions for request ({Id}).", requestId);
                    streamError = ex.Message;
                }

                if (capture.Telemetry.Succeeded is false || streamError is not null)
                {
                    string errorMessage = streamError ?? (capture.TerminalHttpStatusCode == StatusCodes.Status400BadRequest
                        ? capture.TerminalError ?? "Provider rejected the request."
                        : "All OpenAI provider steps failed for this request.");

                    string errorPayload = JsonSerializer.Serialize(new
                    {
                        error = new
                        {
                            message = errorMessage,
                            type = capture.TerminalHttpStatusCode == StatusCodes.Status400BadRequest
                                ? "invalid_request_error"
                                : "server_error",
                            code = (string?)null
                        }
                    });

                    try
                    {
                        await outputStream.WriteAsync(Encoding.UTF8.GetBytes($"data: {errorPayload}\n\n"), cancellationToken);
                        await outputStream.FlushAsync(cancellationToken);
                    }
                    catch (Exception writeEx)
                    {
                        logger.LogDebug(writeEx, "Failed to write error payload to stream for request ({Id}). The client may have disconnected.", requestId);
                    }

                    finalChunkSentUtc = DateTimeOffset.UtcNow;
                    streamError = errorMessage;
                }

                bool failoverUsed = capture.Telemetry.WinnerIndex is > 0;
                bool streamSucceeded = streamError is null;
                string? configuredModel = streamSucceeded
                    ? ResolveConfiguredModel(chain, request.Model, capture.Telemetry.WinnerIndex)
                    : null;
                string? responseFallbackModel = streamSucceeded ? request.Model : null;

                DateTimeOffset responseSentUtc = DateTimeOffset.UtcNow;
                await telemetryWriter.WriteStream(
                    new OpenAIChatCompletionRequestTelemetry(
                        requestId.ToString("n"),
                        clientName,
                        requestReceivedUtc,
                        requestSentUtc,
                        responseSentUtc,
                        ConfiguredModel: configuredModel,
                        RequestModel: request.Model,
                        ResponseFallbackModel: responseFallbackModel,
                        ResponseModel: streamSucceeded ? capture.ResponseModel : null,
                        Streamed: true,
                        FailoverUsed: failoverUsed,
                        HttpStatusCode: streamSucceeded ? StatusCodes.Status200OK : StatusCodes.Status500InternalServerError,
                        Error: streamError,
                        ProviderChainTelemetry: capture.Telemetry),
                    new OpenAIChatCompletionStreamTelemetry(
                        firstChunkSentUtc,
                        finalChunkSentUtc,
                        streamChunkCount,
                        streamUsage,
                        rawStreamUsageJson),
                    cancellationToken);
            },
            "text/event-stream");
    }

    private async Task<IResult> HandleNonStreamResponseAsync(
        Guid requestId, string? clientName, ChatCompletionRequest request,
        IReadOnlyList<OpenAIChatCompletionProvider> chain, DateTimeOffset requestReceivedUtc,
        CancellationToken cancellationToken)
    {
        DateTimeOffset requestSentNonStreamUtc = DateTimeOffset.UtcNow;
        OpenAIChatCompletionNonStreamResult result = await providerChain.TryRunChainAsync(
            request,
            chain,
            cancellationToken);

        if (result is { Succeeded: true, Response: { } r })
        {
            bool failoverUsed = result.ChainTelemetry.WinnerIndex is > 0;
            string json = JsonSerializer.Serialize(r);
            if (logger.IsEnabled(LogLevel.Trace))
            {
                logger.LogTrace("Finished completion request ({Id}). Response: {Response}", requestId, json);
            }

            await telemetryWriter.WriteNonStream(
                new OpenAIChatCompletionRequestTelemetry(
                    requestId.ToString("n"),
                    clientName,
                    requestReceivedUtc,
                    requestSentNonStreamUtc,
                    DateTimeOffset.UtcNow,
                    ConfiguredModel: ResolveConfiguredModel(chain, request.Model, result.ChainTelemetry.WinnerIndex),
                    RequestModel: request.Model,
                    ResponseFallbackModel: request.Model,
                    ResponseModel: null,
                    Streamed: false,
                    FailoverUsed: failoverUsed,
                    HttpStatusCode: StatusCodes.Status200OK,
                    Error: null,
                    ProviderChainTelemetry: result.ChainTelemetry),
                new OpenAIChatCompletionNonStreamTelemetry(r),
                cancellationToken);

            return Results.Content(json, "application/json", Encoding.UTF8);
        }

        int statusCode = result.TerminalHttpStatusCode ?? StatusCodes.Status502BadGateway;
        string errorMessage = string.IsNullOrWhiteSpace(result.TerminalError)
            ? "All OpenAI provider steps failed for this request."
            : result.TerminalError!;

        logger.LogWarning("OpenAI provider chain did not return a success for request ({Id}).", requestId);

        await telemetryWriter.WriteRequest(
            new OpenAIChatCompletionRequestTelemetry(
                requestId.ToString("n"),
                clientName,
                requestReceivedUtc,
                requestSentNonStreamUtc,
                DateTimeOffset.UtcNow,
                ConfiguredModel: null,
                RequestModel: request.Model,
                ResponseFallbackModel: null,
                ResponseModel: null,
                Streamed: false,
                FailoverUsed: false,
                HttpStatusCode: statusCode,
                Error: errorMessage,
                ProviderChainTelemetry: result.ChainTelemetry),
            cancellationToken);

        if (result.TerminalHttpStatusCode is StatusCodes.Status400BadRequest)
        {
            return GatewayResults.OpenAIError(
                StatusCodes.Status400BadRequest,
                result.TerminalError ?? "Provider rejected the request.",
                type: "invalid_request_error");
        }

        return GatewayResults.OpenAIError(
            statusCode,
            errorMessage,
            type: statusCode >= 500 ? "server_error" : "gateway_error");
    }

    static string? ResolveConfiguredModel(IReadOnlyList<OpenAIChatCompletionProvider> chain, string? requestModel, int? winnerIndex)
    {
        if (winnerIndex.HasValue && winnerIndex.Value >= 0 && winnerIndex.Value < chain.Count)
        {
            return ResolveConfiguredModel(chain[winnerIndex.Value], requestModel);
        }

        OpenAIChatCompletionProvider? firstProvider = chain.Count > 0 ? chain[0] : null;
        return firstProvider is null ? null : ResolveConfiguredModel(firstProvider, requestModel);
    }

    static string? ResolveConfiguredModel(OpenAIChatCompletionProvider provider, string? requestModel)
    {
        if (string.IsNullOrWhiteSpace(provider.Model) is false)
        {
            return provider.Model.Trim();
        }

        return OpenAIChatCompletionProviderMatcher.ResolveMatchedConfiguredModel(provider, requestModel);
    }

    static string? ResolveStreamResponseModel(string? chunkData, string? fallbackModel)
    {
        if (string.IsNullOrWhiteSpace(chunkData) || string.Equals(chunkData, "[DONE]", StringComparison.Ordinal))
        {
            return fallbackModel;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(chunkData);
            if (document.RootElement.TryGetProperty("model", out JsonElement modelElement)
                && modelElement.ValueKind == JsonValueKind.String)
            {
                string? streamModel = modelElement.GetString();
                if (string.IsNullOrWhiteSpace(streamModel) is false)
                {
                    return streamModel;
                }
            }
        }
        catch (JsonException)
        {
            return fallbackModel;
        }

        return fallbackModel;
    }

    static StreamUsageCapture ResolveStreamUsage(string? chunkData, ChatCompletionUsage? fallbackUsage, string? fallbackRawUsageJson)
    {
        if (string.IsNullOrWhiteSpace(chunkData) || string.Equals(chunkData, "[DONE]", StringComparison.Ordinal))
        {
            return new StreamUsageCapture(fallbackUsage, fallbackRawUsageJson);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(chunkData);
            if (!document.RootElement.TryGetProperty("usage", out JsonElement usageElement)
                || usageElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return new StreamUsageCapture(fallbackUsage, fallbackRawUsageJson);
            }

            ChatCompletionUsage? usage = usageElement.Deserialize<ChatCompletionUsage>();
            return new StreamUsageCapture(usage ?? fallbackUsage, usageElement.GetRawText());
        }
        catch (JsonException)
        {
            return new StreamUsageCapture(fallbackUsage, fallbackRawUsageJson);
        }
    }

    sealed record class StreamUsageCapture(ChatCompletionUsage? Usage, string? RawUsageJson);
}
