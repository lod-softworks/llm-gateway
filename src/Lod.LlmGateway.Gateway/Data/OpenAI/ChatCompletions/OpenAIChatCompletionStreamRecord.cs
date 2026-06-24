using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lod.LlmGateway.Gateway.Data.OpenAI.ChatCompletions;

[Table("OpenAIChatCompletionStream", Schema = "llm_gateway")]
public sealed record class OpenAIChatCompletionStreamRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }

    public long RequestId { get; init; }

    [ForeignKey(nameof(RequestId))]
    public OpenAIChatCompletionRequestRecord Request { get; init; } = null!;

    public DateTimeOffset? FirstChunkSentUtc { get; init; }

    public DateTimeOffset? FinalChunkSentUtc { get; init; }

    public int ChunkCount { get; init; }

    public int? PromptTokens { get; init; }

    public int? CompletionTokens { get; init; }

    public int? TotalTokens { get; init; }

    public double? DurationSeconds { get; init; }

    public double? TimeToFirstChunkSeconds { get; init; }

    public double? TokensPerSecond { get; init; }

    public string? RawUsageJson { get; init; }
}
