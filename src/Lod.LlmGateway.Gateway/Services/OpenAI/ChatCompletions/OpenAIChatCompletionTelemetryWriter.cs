using Lod.LlmGateway.Gateway.Data;
using Lod.LlmGateway.Gateway.Data.OpenAI.ChatCompletions;
using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;

public sealed record class OpenAIChatCompletionRequestTelemetry(
    string GatewayRequestId,
    string? Client,
    DateTimeOffset RequestReceivedUtc,
    DateTimeOffset? RequestSentUtc,
    DateTimeOffset ResponseSentUtc,
    string? ConfiguredModel,
    string? RequestModel,
    string? ResponseFallbackModel,
    string? ResponseModel,
    bool Streamed,
    bool FailoverUsed,
    int HttpStatusCode,
    string? Error,
    OpenAIChatCompletionChainTelemetry? ProviderChainTelemetry);

public sealed record class OpenAIChatCompletionNonStreamTelemetry(ChatCompletionResponse Response);

public sealed record class OpenAIChatCompletionStreamTelemetry(
    DateTimeOffset? FirstChunkSentUtc,
    DateTimeOffset? FinalChunkSentUtc,
    int ChunkCount,
    ChatCompletionUsage? Usage,
    string? RawUsageJson);

public sealed class OpenAIChatCompletionTelemetryWriter(
    GatewayDbContext dbContext,
    ILogger<OpenAIChatCompletionTelemetryWriter> logger)
{
    public async Task WriteRequest(
        OpenAIChatCompletionRequestTelemetry requestTelemetry,
        CancellationToken cancellationToken)
    {
        OpenAIChatCompletionRequestRecord request = BuildRequestRecord(requestTelemetry, responseModel: null);
        dbContext.OpenAIChatCompletionRequest.Add(request);
        await SaveChanges(requestTelemetry.GatewayRequestId, cancellationToken);
    }

    public async Task WriteNonStream(
        OpenAIChatCompletionRequestTelemetry requestTelemetry,
        OpenAIChatCompletionNonStreamTelemetry nonStreamTelemetry,
        CancellationToken cancellationToken)
    {
        ChatCompletionResponse response = nonStreamTelemetry.Response;
        OpenAIChatCompletionRequestRecord request = BuildRequestRecord(requestTelemetry, response.Model);
        double? durationSeconds = GetElapsedSeconds(requestTelemetry.RequestSentUtc, requestTelemetry.ResponseSentUtc);
        dbContext.OpenAIChatCompletionRequest.Add(request);
        dbContext.OpenAIChatCompletionNonStream.Add(new OpenAIChatCompletionNonStreamRecord
        {
            Request = request,
            UpstreamResponseId = response.Id,
            PromptTokens = response.Usage?.PromptTokens,
            CompletionTokens = response.Usage?.CompletionTokens,
            TotalTokens = response.Usage?.TotalTokens,
            DurationSeconds = durationSeconds,
            TokensPerSecond = CalculateTokensPerSecond(response.Usage?.CompletionTokens, durationSeconds),
            PromptCost = TryGetDecimal(response.Usage?.AdditionalProperties, "prompt_cost"),
            CompletionCost = TryGetDecimal(response.Usage?.AdditionalProperties, "completion_cost"),
            TotalCost = TryGetDecimal(response.Usage?.AdditionalProperties, "total_cost")
                ?? TryGetDecimal(response.AdditionalProperties, "cost")
                ?? TryGetDecimal(response.AdditionalProperties, "total_cost"),
            RawUsageJson = response.Usage is null ? null : JsonSerializer.Serialize(response.Usage)
        });

        await SaveChanges(requestTelemetry.GatewayRequestId, cancellationToken);
    }

    public async Task WriteStream(
        OpenAIChatCompletionRequestTelemetry requestTelemetry,
        OpenAIChatCompletionStreamTelemetry streamTelemetry,
        CancellationToken cancellationToken)
    {
        OpenAIChatCompletionRequestRecord request = BuildRequestRecord(requestTelemetry, responseModel: null);
        double? durationSeconds = GetElapsedSeconds(requestTelemetry.RequestSentUtc, requestTelemetry.ResponseSentUtc);
        double? timeToFirstChunkSeconds = GetElapsedSeconds(requestTelemetry.RequestSentUtc, streamTelemetry.FirstChunkSentUtc);
        dbContext.OpenAIChatCompletionRequest.Add(request);
        dbContext.OpenAIChatCompletionStream.Add(new OpenAIChatCompletionStreamRecord
        {
            Request = request,
            FirstChunkSentUtc = streamTelemetry.FirstChunkSentUtc,
            FinalChunkSentUtc = streamTelemetry.FinalChunkSentUtc,
            ChunkCount = streamTelemetry.ChunkCount,
            PromptTokens = streamTelemetry.Usage?.PromptTokens,
            CompletionTokens = streamTelemetry.Usage?.CompletionTokens,
            TotalTokens = streamTelemetry.Usage?.TotalTokens,
            DurationSeconds = durationSeconds,
            TimeToFirstChunkSeconds = timeToFirstChunkSeconds,
            TokensPerSecond = CalculateTokensPerSecond(streamTelemetry.Usage?.CompletionTokens, durationSeconds),
            RawUsageJson = streamTelemetry.RawUsageJson
        });

        await SaveChanges(requestTelemetry.GatewayRequestId, cancellationToken);
    }

    OpenAIChatCompletionRequestRecord BuildRequestRecord(
        OpenAIChatCompletionRequestTelemetry telemetry,
        string? responseModel)
    {
        string? resolvedResponseModel = OpenAIChatCompletionModelResolution.ResolveForTelemetry(
            responseModel,
            telemetry.ResponseModel,
            telemetry.ConfiguredModel,
            telemetry.ResponseFallbackModel);
        string requestedModel = string.IsNullOrWhiteSpace(telemetry.RequestModel)
            ? resolvedResponseModel ?? ""
            : telemetry.RequestModel.Trim();

        return new OpenAIChatCompletionRequestRecord
        {
            GatewayRequestId = telemetry.GatewayRequestId,
            Client = telemetry.Client,
            Streamed = telemetry.Streamed,
            RequestReceivedUtc = telemetry.RequestReceivedUtc,
            RequestSentUtc = telemetry.RequestSentUtc,
            ResponseSentUtc = telemetry.ResponseSentUtc,
            RequestedModel = requestedModel,
            ResponseModel = resolvedResponseModel,
            Provider = telemetry.ProviderChainTelemetry?.ProviderName,
            ProviderIndex = telemetry.ProviderChainTelemetry?.WinnerIndex,
            ProviderAttemptsJson = telemetry.ProviderChainTelemetry?.AttemptsJson,
            FailoverUsed = telemetry.FailoverUsed,
            HttpStatusCode = telemetry.HttpStatusCode,
            Error = telemetry.Error
        };
    }

    async Task SaveChanges(string gatewayRequestId, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Failed to persist chat completion telemetry for request {RequestId}.", gatewayRequestId);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Failed to persist chat completion telemetry for request {RequestId}.", gatewayRequestId);
        }
    }

    static double? GetElapsedSeconds(DateTimeOffset? start, DateTimeOffset? end)
    {
        if (!start.HasValue || !end.HasValue || end.Value <= start.Value)
        {
            return null;
        }

        return (end.Value - start.Value).TotalSeconds;
    }

    static double? CalculateTokensPerSecond(int? completionTokens, double? durationSeconds)
    {
        if (!completionTokens.HasValue || completionTokens.Value <= 0 || !durationSeconds.HasValue || durationSeconds.Value <= 0)
        {
            return null;
        }

        return completionTokens.Value / durationSeconds.Value;
    }

    static decimal? TryGetDecimal(IReadOnlyDictionary<string, JsonElement>? metadata, string key)
    {
        if (metadata is null || metadata.TryGetValue(key, out JsonElement value) is false)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out decimal number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out decimal number) => number,
            _ => null
        };
    }
}
