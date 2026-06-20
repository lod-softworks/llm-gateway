using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

[Table("OpenAIChatCompletionRequest", Schema = "llm_gateway")]
public sealed record class OpenAIChatCompletionRequestRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; init; }

    [MaxLength(64)]
    public string GatewayRequestId { get; init; } = "";

    [MaxLength(128)]
    public string? Client { get; init; }

    [MaxLength(64)]
    public string Api { get; init; } = "chat.completions";

    [MaxLength(128)]
    public string Endpoint { get; init; } = "/v1/chat/completions";

    public bool Streamed { get; init; }

    public DateTimeOffset RequestReceivedUtc { get; init; }

    public DateTimeOffset? RequestSentUtc { get; init; }

    public DateTimeOffset ResponseSentUtc { get; init; }

    [MaxLength(128)]
    public string RequestedModel { get; init; } = "";

    [MaxLength(128)]
    public string? ResponseModel { get; init; }

    [MaxLength(128)]
    public string? Provider { get; init; }

    public int? ProviderIndex { get; init; }

    public string? ProviderAttemptsJson { get; init; }

    public bool FailoverUsed { get; init; }

    public int HttpStatusCode { get; init; }

    public string? Error { get; init; }
}
