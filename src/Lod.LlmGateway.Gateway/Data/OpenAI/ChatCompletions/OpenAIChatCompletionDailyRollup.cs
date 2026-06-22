using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lod.LlmGateway.Gateway.Data.OpenAI.ChatCompletions;

[Table("OpenAIChatCompletionDailyRollup", Schema = "llm_gateway")]
public sealed record class OpenAIChatCompletionDailyRollup
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }

    public DateOnly DayUtc { get; init; }

    [MaxLength(128)]
    public string Client { get; init; } = "";

    public int RequestCount { get; init; }

    public int NonStreamRequestCount { get; init; }

    public int StreamRequestCount { get; init; }

    public int PrimaryProviderRequestCount { get; init; }

    public int FailoverRequestCount { get; init; }

    public int FailedRequestCount { get; init; }

    public int NonStreamPromptTokens { get; init; }

    public int NonStreamCompletionTokens { get; init; }

    public int NonStreamTotalTokens { get; init; }

    public int StreamPromptTokens { get; init; }

    public int StreamCompletionTokens { get; init; }

    public int StreamTotalTokens { get; init; }

    [Precision(18, 8)]
    public decimal NonStreamPromptCost { get; init; }

    [Precision(18, 8)]
    public decimal NonStreamCompletionCost { get; init; }

    [Precision(18, 8)]
    public decimal NonStreamTotalCost { get; init; }

    public double? NonStreamAverageTokensPerSecond { get; init; }

    public double? StreamAverageTokensPerSecond { get; init; }
}
