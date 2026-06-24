using Lod.LlmGateway.Gateway.Data.OpenAI.ChatCompletions;
using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;
using Lod.LlmGateway.Gateway.Api;
using Lod.LlmGateway.Gateway.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;

namespace Lod.LlmGateway.Gateway.Pages;

public class IndexModel(
    IWebHostEnvironment environment,
    GatewayDbContext dbContext,
    IOptionsMonitor<OpenAIChatCompletionOptions> openAiChatCompletionOptions) : PageModel
{
    public bool IsDevelopment { get; } = environment.IsDevelopment();
    public int ConfiguredProviderCount { get; } = openAiChatCompletionOptions.CurrentValue.Providers.Count;
    public BranchMetricWindow AllTimeMetrics { get; private set; } = BranchMetricWindow.Empty("All time");
    public BranchMetricWindow Last24HoursMetrics { get; private set; } = BranchMetricWindow.Empty("Last 24");

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        DateTimeOffset trailing24HoursStart = utcNow.AddHours(-24);

        List<OpenAIChatCompletionRequestRecord> requestRecords = await dbContext.OpenAIChatCompletionRequest
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        List<OpenAIChatCompletionNonStreamRecord> nonStreamCompletionRecords = await dbContext.OpenAIChatCompletionNonStream
            .AsNoTracking()
            .Include(nameof(OpenAIChatCompletionNonStreamRecord.Request))
            .ToListAsync(cancellationToken);
        List<OpenAIChatCompletionStreamRecord> streamCompletionRecords = await dbContext.OpenAIChatCompletionStream
            .AsNoTracking()
            .Include(nameof(OpenAIChatCompletionStreamRecord.Request))
            .ToListAsync(cancellationToken);

        List<ChatCompletionRequestSnapshot> requests = BuildRequestSnapshots(requestRecords);
        List<NonStreamCompletionSnapshot> nonStreamCompletions = BuildNonStreamCompletionSnapshots(nonStreamCompletionRecords);
        List<StreamCompletionSnapshot> streamCompletions = BuildStreamCompletionSnapshots(streamCompletionRecords);

        AllTimeMetrics = BuildMetricWindow("All time", requests, nonStreamCompletions, streamCompletions);
        Last24HoursMetrics = BuildMetricWindow(
            "Last 24",
            FilterRequestsByResponseSent(requests, trailing24HoursStart),
            FilterNonStreamCompletionsByResponseSent(nonStreamCompletions, trailing24HoursStart),
            FilterStreamCompletionsByResponseSent(streamCompletions, trailing24HoursStart));
    }

    static List<ChatCompletionRequestSnapshot> BuildRequestSnapshots(IReadOnlyList<OpenAIChatCompletionRequestRecord> records)
    {
        List<ChatCompletionRequestSnapshot> snapshots = [];
        foreach (OpenAIChatCompletionRequestRecord record in records)
        {
            snapshots.Add(new ChatCompletionRequestSnapshot(
                record.Id,
                record.Streamed,
                record.ResponseSentUtc,
                record.RequestSentUtc,
                record.ResponseModel,
                record.ProviderIndex,
                record.ProviderAttemptsJson,
                record.HttpStatusCode,
                record.Error));
        }

        return snapshots;
    }

    static List<NonStreamCompletionSnapshot> BuildNonStreamCompletionSnapshots(IReadOnlyList<OpenAIChatCompletionNonStreamRecord> records)
    {
        List<NonStreamCompletionSnapshot> snapshots = [];
        foreach (OpenAIChatCompletionNonStreamRecord record in records)
        {
            snapshots.Add(new NonStreamCompletionSnapshot(
                record.RequestId,
                record.Request.ResponseSentUtc,
                record.Request.ResponseModel,
                record.TokensPerSecond));
        }

        return snapshots;
    }

    static List<StreamCompletionSnapshot> BuildStreamCompletionSnapshots(IReadOnlyList<OpenAIChatCompletionStreamRecord> records)
    {
        List<StreamCompletionSnapshot> snapshots = [];
        foreach (OpenAIChatCompletionStreamRecord record in records)
        {
            snapshots.Add(new StreamCompletionSnapshot(
                record.RequestId,
                record.Request.ResponseSentUtc,
                record.Request.ResponseModel,
                record.CompletionTokens,
                record.TokensPerSecond));
        }

        return snapshots;
    }

    static List<ChatCompletionRequestSnapshot> FilterRequestsByResponseSent(
        IReadOnlyList<ChatCompletionRequestSnapshot> snapshots,
        DateTimeOffset threshold)
    {
        List<ChatCompletionRequestSnapshot> filtered = [];
        foreach (ChatCompletionRequestSnapshot snapshot in snapshots)
        {
            if (snapshot.ResponseSentUtc >= threshold)
            {
                filtered.Add(snapshot);
            }
        }

        return filtered;
    }

    static List<NonStreamCompletionSnapshot> FilterNonStreamCompletionsByResponseSent(
        IReadOnlyList<NonStreamCompletionSnapshot> snapshots,
        DateTimeOffset threshold)
    {
        List<NonStreamCompletionSnapshot> filtered = [];
        foreach (NonStreamCompletionSnapshot snapshot in snapshots)
        {
            if (snapshot.ResponseSentUtc >= threshold)
            {
                filtered.Add(snapshot);
            }
        }

        return filtered;
    }

    static List<StreamCompletionSnapshot> FilterStreamCompletionsByResponseSent(
        IReadOnlyList<StreamCompletionSnapshot> snapshots,
        DateTimeOffset threshold)
    {
        List<StreamCompletionSnapshot> filtered = [];
        foreach (StreamCompletionSnapshot snapshot in snapshots)
        {
            if (snapshot.ResponseSentUtc >= threshold)
            {
                filtered.Add(snapshot);
            }
        }

        return filtered;
    }

    static BranchMetricWindow BuildMetricWindow(
        string label,
        IReadOnlyList<ChatCompletionRequestSnapshot> requests,
        IReadOnlyList<NonStreamCompletionSnapshot> nonStreamCompletions,
        IReadOnlyList<StreamCompletionSnapshot> streamCompletions)
    {
        List<ChatCompletionRequestSnapshot> nonStreamRequests = [];
        List<ChatCompletionRequestSnapshot> streamRequests = [];
        foreach (ChatCompletionRequestSnapshot request in requests)
        {
            if (request.Streamed)
            {
                streamRequests.Add(request);
            }
            else
            {
                nonStreamRequests.Add(request);
            }
        }

        return new BranchMetricWindow(
            label,
            BuildBranchMetrics("Non-streamed", "bi bi-file-earmark-text", nonStreamRequests, BuildNonStreamAverageTokensPerSecond(nonStreamCompletions)),
            BuildBranchMetrics("Streamed", "bi bi-broadcast", streamRequests, BuildStreamAverageTokensPerSecond(streamCompletions)));
    }

    static BranchMetrics BuildBranchMetrics(
        string label,
        string icon,
        IReadOnlyList<ChatCompletionRequestSnapshot> requests,
        IReadOnlyList<ModelTokenRate> tokenRates)
    {
        List<string> responseModels = [];
        foreach (ChatCompletionRequestSnapshot request in requests)
        {
            if (!string.IsNullOrWhiteSpace(request.ResponseModel))
            {
                responseModels.Add(request.ResponseModel);
            }
        }

        return new BranchMetrics(
            label,
            icon,
            BuildPieChartData(responseModels),
            BuildFailoverRatePieChart(requests),
            tokenRates,
            BuildFailedRequestRate(requests));
    }

    static PieChartData BuildFailoverRatePieChart(IReadOnlyList<ChatCompletionRequestSnapshot> snapshots)
    {
        List<string> winningAttemptLabels = [];
        foreach (ChatCompletionRequestSnapshot snapshot in snapshots)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.ProviderAttemptsJson) && snapshot.ProviderIndex.HasValue)
            {
                winningAttemptLabels.Add(GetAttemptLabel(snapshot.ProviderIndex.Value + 1));
            }
        }

        return BuildPieChartData(winningAttemptLabels);
    }

    static IReadOnlyList<ModelTokenRate> BuildNonStreamAverageTokensPerSecond(IReadOnlyList<NonStreamCompletionSnapshot> snapshots)
    {
        List<TokenRateSnapshot> tokenRateSnapshots = [];
        foreach (NonStreamCompletionSnapshot snapshot in snapshots)
        {
            tokenRateSnapshots.Add(new TokenRateSnapshot(snapshot.ResponseModel, snapshot.TokensPerSecond));
        }

        return BuildAverageTokensPerSecond(tokenRateSnapshots);
    }

    static IReadOnlyList<ModelTokenRate> BuildStreamAverageTokensPerSecond(IReadOnlyList<StreamCompletionSnapshot> snapshots)
    {
        List<TokenRateSnapshot> tokenRateSnapshots = [];
        foreach (StreamCompletionSnapshot snapshot in snapshots)
        {
            tokenRateSnapshots.Add(new TokenRateSnapshot(snapshot.ResponseModel, snapshot.TokensPerSecond));
        }

        return BuildAverageTokensPerSecond(tokenRateSnapshots);
    }

    static IReadOnlyList<ModelTokenRate> BuildAverageTokensPerSecond(IReadOnlyList<TokenRateSnapshot> snapshots)
    {
        Dictionary<string, decimal> totalTokensPerSecondByModel = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> countByModel = new(StringComparer.OrdinalIgnoreCase);
        foreach (TokenRateSnapshot snapshot in snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.ResponseModel) || !snapshot.TokensPerSecond.HasValue || snapshot.TokensPerSecond.Value <= 0)
            {
                continue;
            }

            string model = snapshot.ResponseModel.Trim();
            decimal tokensPerSecond = Convert.ToDecimal(snapshot.TokensPerSecond.Value);
            if (totalTokensPerSecondByModel.TryGetValue(model, out decimal currentTotal))
            {
                totalTokensPerSecondByModel[model] = currentTotal + tokensPerSecond;
                countByModel[model]++;
            }
            else
            {
                totalTokensPerSecondByModel[model] = tokensPerSecond;
                countByModel[model] = 1;
            }
        }

        List<ModelTokenRate> rates = [];
        foreach (KeyValuePair<string, decimal> totalTokensPerSecond in totalTokensPerSecondByModel)
        {
            int count = countByModel[totalTokensPerSecond.Key];
            rates.Add(new ModelTokenRate(totalTokensPerSecond.Key, decimal.Round(totalTokensPerSecond.Value / count, 2)));
        }

        return rates
            .OrderByDescending(rate => rate.AverageTokensPerSecond)
            .ThenBy(rate => rate.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static PercentageStat BuildFailedRequestRate(IReadOnlyList<ChatCompletionRequestSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return PercentageStat.Empty();
        }

        int failedRequestCount = 0;
        foreach (ChatCompletionRequestSnapshot snapshot in snapshots)
        {
            if (snapshot.HttpStatusCode >= 400 || !string.IsNullOrWhiteSpace(snapshot.Error))
            {
                failedRequestCount++;
            }
        }

        decimal failedRate = decimal.Round(failedRequestCount * 100m / snapshots.Count, 2);
        return new PercentageStat(failedRequestCount, snapshots.Count, failedRate);
    }

    static string GetAttemptLabel(int attemptNumber)
    {
        return attemptNumber switch
        {
            1 => "1st attempt",
            2 => "2nd attempt",
            3 => "3rd attempt",
            _ => $"{attemptNumber}th attempt"
        };
    }

    static PieChartData BuildPieChartData(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return PieChartData.Empty();
        }

        string[] palette = [
            "var(--lod-chart-color-1)",
            "var(--lod-chart-color-2)",
            "var(--lod-chart-color-3)",
            "var(--lod-chart-color-4)",
            "var(--lod-chart-color-5)",
            "var(--lod-chart-color-6)",
            "var(--lod-chart-color-7)",
            "var(--lod-chart-color-8)",
            "var(--lod-chart-color-9)",
            "var(--lod-chart-color-10)"
        ];

        Dictionary<string, int> countByLabel = new(StringComparer.OrdinalIgnoreCase);
        foreach (string label in labels)
        {
            string trimmedLabel = label.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLabel))
            {
                continue;
            }

            if (countByLabel.TryGetValue(trimmedLabel, out int currentCount))
            {
                countByLabel[trimmedLabel] = currentCount + 1;
            }
            else
            {
                countByLabel[trimmedLabel] = 1;
            }
        }

        if (countByLabel.Count == 0)
        {
            return PieChartData.Empty();
        }

        decimal totalRequestCount = labels.Count;
        List<ModelBreakdownItem> items = [];
        foreach (KeyValuePair<string, int> count in countByLabel)
        {
            items.Add(new ModelBreakdownItem(
                count.Key,
                count.Value,
                decimal.Round(count.Value * 100m / totalRequestCount, 1)));
        }

        items = items
            .OrderByDescending(item => item.RequestCount)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        decimal runningPercentage = 0m;
        List<PieChartSegment> segments = [];
        int index = 0;
        foreach (ModelBreakdownItem item in items)
        {
            decimal start = runningPercentage;
            decimal end = start + item.Percentage;
            runningPercentage = end;
            segments.Add(new PieChartSegment(
                item.Label,
                item.RequestCount,
                item.Percentage,
                palette[index % palette.Length],
                start,
                end));
            index++;
        }

        StringBuilder gradient = new();
        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                gradient.Append(", ");
            }

            PieChartSegment segment = segments[i];
            gradient.Append(segment.Color);
            gradient.Append(' ');
            gradient.Append(segment.StartPercentage.ToString("0.0", CultureInfo.InvariantCulture));
            gradient.Append("% ");
            gradient.Append(segment.EndPercentage.ToString("0.0", CultureInfo.InvariantCulture));
            gradient.Append('%');
        }

        int totalRequests = 0;
        foreach (ModelBreakdownItem item in items)
        {
            totalRequests += item.RequestCount;
        }

        return new PieChartData(totalRequests, gradient.ToString(), segments);
    }

    public sealed record class ChatCompletionRequestSnapshot(
        long Id,
        bool Streamed,
        DateTimeOffset ResponseSentUtc,
        DateTimeOffset? RequestSentUtc,
        string? ResponseModel,
        int? ProviderIndex,
        string? ProviderAttemptsJson,
        int HttpStatusCode,
        string? Error);

    public sealed record class NonStreamCompletionSnapshot(
        long RequestId,
        DateTimeOffset ResponseSentUtc,
        string? ResponseModel,
        double? TokensPerSecond);

    public sealed record class StreamCompletionSnapshot(
        long RequestId,
        DateTimeOffset ResponseSentUtc,
        string? ResponseModel,
        int? CompletionTokens,
        double? TokensPerSecond);

    public sealed record class TokenRateSnapshot(string? ResponseModel, double? TokensPerSecond);

    public sealed record class BranchMetricWindow(string Label, BranchMetrics NonStream, BranchMetrics Stream)
    {
        public static BranchMetricWindow Empty(string label)
        {
            return new BranchMetricWindow(label, BranchMetrics.Empty("Non-streamed", "bi bi-file-earmark-text"), BranchMetrics.Empty("Streamed", "bi bi-broadcast"));
        }
    }

    public sealed record class BranchMetrics(
        string Label,
        string Icon,
        PieChartData ModelPieChart,
        PieChartData FailoverRatePieChart,
        IReadOnlyList<ModelTokenRate> AverageTokensPerSecond,
        PercentageStat FailedRequestRate)
    {
        public static BranchMetrics Empty(string label, string icon)
        {
            return new BranchMetrics(label, icon, PieChartData.Empty(), PieChartData.Empty(), [], PercentageStat.Empty());
        }
    }

    public sealed record class ModelBreakdownItem(string Label, int RequestCount, decimal Percentage);

    public sealed record class ModelTokenRate(string Model, decimal AverageTokensPerSecond);

    public sealed record class PercentageStat(int FailedRequestCount, int TotalRequestCount, decimal Percentage)
    {
        public bool HasData => TotalRequestCount > 0;

        public static PercentageStat Empty()
        {
            return new PercentageStat(0, 0, 0m);
        }
    }

    public sealed record class PieChartSegment(
        string Label,
        int RequestCount,
        decimal Percentage,
        string Color,
        decimal StartPercentage,
        decimal EndPercentage);

    public sealed record class PieChartData(int TotalRequests, string Gradient, IReadOnlyList<PieChartSegment> Segments)
    {
        public bool HasData => Segments.Count > 0;

        public static PieChartData Empty()
        {
            return new PieChartData(0, "var(--lod-chart-empty) 0% 100%", []);
        }
    }

}
