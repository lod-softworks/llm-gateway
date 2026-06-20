using Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;
using Lod.LlmGateway.Gateway.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lod.LlmGateway.Gateway.Tests;

public sealed class OpenAIChatCompletionTelemetryWriterTests
{
    [Fact]
    public async Task WriteNonStream_UsesConfiguredModel_WhenApiOmitsResponseModel()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<GatewayDbContext> options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseSqlite(connection)
            .Options;

        await using GatewayDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();
        OpenAIChatCompletionTelemetryWriter writer = new(dbContext, NullLogger<OpenAIChatCompletionTelemetryWriter>.Instance);

        await writer.WriteNonStream(
            new OpenAIChatCompletionRequestTelemetry(
                GatewayRequestId: "request-id",
                Client: null,
                RequestReceivedUtc: DateTimeOffset.UtcNow,
                RequestSentUtc: DateTimeOffset.UtcNow,
                ResponseSentUtc: DateTimeOffset.UtcNow,
                ConfiguredModel: "qwen3.6",
                RequestModel: "gpt-5-nano",
                ResponseFallbackModel: "gpt-5-nano",
                ResponseModel: null,
                Streamed: false,
                FailoverUsed: false,
                HttpStatusCode: 200,
                Error: null,
                ProviderChainTelemetry: OpenAIChatCompletionChainTelemetry.ForWinner(
                    "Api",
                    0,
                    [new OpenAIChatCompletionAttempt("Api", 0, true, 200, null)])),
            new OpenAIChatCompletionNonStreamTelemetry(new ChatCompletionResponse
            {
                Id = "response-id",
                Model = ""
            }),
            CancellationToken.None);

        OpenAIChatCompletionRequestRecord record = await dbContext.OpenAIChatCompletionRequest.SingleAsync();
        Assert.Equal("qwen3.6", record.ResponseModel);
    }

    [Fact]
    public async Task WriteStream_UsesConfiguredModel_WhenApiStreamOmitsResponseModel()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<GatewayDbContext> options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseSqlite(connection)
            .Options;

        await using GatewayDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();
        OpenAIChatCompletionTelemetryWriter writer = new(dbContext, NullLogger<OpenAIChatCompletionTelemetryWriter>.Instance);

        await writer.WriteStream(
            new OpenAIChatCompletionRequestTelemetry(
                GatewayRequestId: "request-id",
                Client: null,
                RequestReceivedUtc: DateTimeOffset.UtcNow,
                RequestSentUtc: DateTimeOffset.UtcNow,
                ResponseSentUtc: DateTimeOffset.UtcNow,
                ConfiguredModel: "qwen3.6",
                RequestModel: "gpt-5-nano",
                ResponseFallbackModel: "gpt-5-nano",
                ResponseModel: null,
                Streamed: true,
                FailoverUsed: false,
                HttpStatusCode: 200,
                Error: null,
                ProviderChainTelemetry: OpenAIChatCompletionChainTelemetry.ForWinner(
                    "Api",
                    0,
                    [new OpenAIChatCompletionAttempt("Api", 0, true, 200, null)])),
            new OpenAIChatCompletionStreamTelemetry(
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                1,
                Usage: null,
                RawUsageJson: null),
            CancellationToken.None);

        OpenAIChatCompletionRequestRecord record = await dbContext.OpenAIChatCompletionRequest.SingleAsync();
        Assert.Equal("qwen3.6", record.ResponseModel);
    }

    [Fact]
    public async Task WriteNonStream_UsesResponseModel_WhenApiReturnsAuthoritativeModel()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        DbContextOptions<GatewayDbContext> options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseSqlite(connection)
            .Options;

        await using GatewayDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();
        OpenAIChatCompletionTelemetryWriter writer = new(dbContext, NullLogger<OpenAIChatCompletionTelemetryWriter>.Instance);

        await writer.WriteNonStream(
            new OpenAIChatCompletionRequestTelemetry(
                GatewayRequestId: "request-id",
                Client: null,
                RequestReceivedUtc: DateTimeOffset.UtcNow,
                RequestSentUtc: DateTimeOffset.UtcNow,
                ResponseSentUtc: DateTimeOffset.UtcNow,
                ConfiguredModel: "configured-model",
                RequestModel: "gpt-5-nano",
                ResponseFallbackModel: "gpt-5-nano",
                ResponseModel: null,
                Streamed: false,
                FailoverUsed: true,
                HttpStatusCode: 200,
                Error: null,
                ProviderChainTelemetry: OpenAIChatCompletionChainTelemetry.ForWinner(
                    "OpenAI",
                    2,
                    [new OpenAIChatCompletionAttempt("OpenAI", 2, true, 200, null)])),
            new OpenAIChatCompletionNonStreamTelemetry(new ChatCompletionResponse
            {
                Id = "response-id",
                Model = "api-returned-model"
            }),
            CancellationToken.None);

        OpenAIChatCompletionRequestRecord record = await dbContext.OpenAIChatCompletionRequest.SingleAsync();
        Assert.Equal("api-returned-model", record.ResponseModel);
    }
}
