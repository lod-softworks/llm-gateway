using Lod.LlmGateway.Gateway.Data;
using Microsoft.EntityFrameworkCore;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

public sealed class OpenAIChatCompletionDailyRollupWorker(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<OpenAIChatCompletionDailyRollupWorker> logger) : BackgroundService
{
    static readonly TimeSpan RollupInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RollupAsync(stoppingToken);

        using PeriodicTimer timer = new(RollupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RollupAsync(stoppingToken);
        }
    }

    async Task RollupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            GatewayDbContext dbContext = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

            List<OpenAIChatCompletionRequestRecord> requests = await dbContext.OpenAIChatCompletionRequest
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            List<OpenAIChatCompletionNonStreamRecord> nonStreamRecords = await dbContext.OpenAIChatCompletionNonStream
                .AsNoTracking()
                .Include(record => record.Request)
                .ToListAsync(cancellationToken);
            List<OpenAIChatCompletionStreamRecord> streamRecords = await dbContext.OpenAIChatCompletionStream
                .AsNoTracking()
                .Include(record => record.Request)
                .ToListAsync(cancellationToken);

            List<OpenAIChatCompletionDailyRollup> rollups = requests
                .GroupBy(record => new
                {
                    DayUtc = DateOnly.FromDateTime(record.RequestReceivedUtc.UtcDateTime.Date),
                    Client = record.Client ?? ""
                })
                .Select(group => BuildRollup(group.Key.DayUtc, group.Key.Client, group.ToArray(), nonStreamRecords, streamRecords))
                .ToList();

            await dbContext.ChatCompletionDailyRollup.ExecuteDeleteAsync(cancellationToken);
            dbContext.ChatCompletionDailyRollup.AddRange(rollups);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to update OpenAI chat completion daily rollup.");
        }
    }

    static OpenAIChatCompletionDailyRollup BuildRollup(
        DateOnly dayUtc,
        string client,
        IReadOnlyList<OpenAIChatCompletionRequestRecord> requests,
        IReadOnlyList<OpenAIChatCompletionNonStreamRecord> nonStreamRecords,
        IReadOnlyList<OpenAIChatCompletionStreamRecord> streamRecords)
    {
        HashSet<long> requestIds = requests.Select(record => record.Id).ToHashSet();
        OpenAIChatCompletionNonStreamRecord[] nonStreamMatches = nonStreamRecords
            .Where(record => requestIds.Contains(record.RequestId))
            .ToArray();
        OpenAIChatCompletionStreamRecord[] streamMatches = streamRecords
            .Where(record => requestIds.Contains(record.RequestId))
            .ToArray();
        double? nonStreamAverageTokensPerSecond = CalculateAverageTokensPerSecond(nonStreamMatches.Select(record => record.TokensPerSecond));
        double? streamAverageTokensPerSecond = CalculateAverageTokensPerSecond(streamMatches.Select(record => record.TokensPerSecond));

        return new OpenAIChatCompletionDailyRollup
        {
            DayUtc = dayUtc,
            Client = client,
            RequestCount = requests.Count,
            NonStreamRequestCount = requests.Count(record => !record.Streamed),
            StreamRequestCount = requests.Count(record => record.Streamed),
            InitialModelRequestCount = requests.Count(record => !record.LocalModelFallbackUsed && !record.CloudFallbackUsed),
            LocalFallbackModelRequestCount = requests.Count(record => record.LocalModelFallbackUsed),
            CloudFallbackModelRequestCount = requests.Count(record => record.CloudFallbackUsed),
            FailedRequestCount = requests.Count(record => record.HttpStatusCode >= 400 || !string.IsNullOrWhiteSpace(record.Error)),
            NonStreamPromptTokens = nonStreamMatches.Sum(record => record.PromptTokens ?? 0),
            NonStreamCompletionTokens = nonStreamMatches.Sum(record => record.CompletionTokens ?? 0),
            NonStreamTotalTokens = nonStreamMatches.Sum(record => record.TotalTokens ?? 0),
            StreamPromptTokens = streamMatches.Sum(record => record.PromptTokens ?? 0),
            StreamCompletionTokens = streamMatches.Sum(record => record.CompletionTokens ?? 0),
            StreamTotalTokens = streamMatches.Sum(record => record.TotalTokens ?? 0),
            NonStreamPromptCost = nonStreamMatches.Sum(record => record.PromptCost ?? 0),
            NonStreamCompletionCost = nonStreamMatches.Sum(record => record.CompletionCost ?? 0),
            NonStreamTotalCost = nonStreamMatches.Sum(record => record.TotalCost ?? 0),
            NonStreamAverageTokensPerSecond = nonStreamAverageTokensPerSecond,
            StreamAverageTokensPerSecond = streamAverageTokensPerSecond
        };
    }

    static double? CalculateAverageTokensPerSecond(IEnumerable<double?> tokensPerSecondValues)
    {
        double[] values = tokensPerSecondValues
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return values.Length == 0 ? null : values.Average();
    }
}
