using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

[Table("OpenAIChatCompletionNonStream", Schema = "llm_gateway")]
public sealed record class OpenAIChatCompletionNonStreamRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }

    public long RequestId { get; init; }

    [ForeignKey(nameof(RequestId))]
    public OpenAIChatCompletionRequestRecord Request { get; init; } = null!;

    [MaxLength(128)]
    public string? UpstreamResponseId { get; init; }

    public int? PromptTokens { get; init; }

    public int? CompletionTokens { get; init; }

    public int? TotalTokens { get; init; }

    public double? DurationSeconds { get; init; }

    public double? TokensPerSecond { get; init; }

    [Precision(18, 8)]
    public decimal? PromptCost { get; init; }

    [Precision(18, 8)]
    public decimal? CompletionCost { get; init; }

    [Precision(18, 8)]
    public decimal? TotalCost { get; init; }

    public string? RawUsageJson { get; init; }
}
