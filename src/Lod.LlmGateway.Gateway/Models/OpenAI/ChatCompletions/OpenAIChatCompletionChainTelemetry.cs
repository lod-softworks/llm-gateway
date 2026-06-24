using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;

public sealed record OpenAIChatCompletionAttempt(
    string Name,
    int Index,
    bool Ok,
    int? HttpStatus,
    string? Error);

public sealed record OpenAIChatCompletionChainTelemetry(
    string? ProviderName,
    int? WinnerIndex,
    string? AttemptsJson)
{
    static readonly JsonSerializerOptions AttemptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool Succeeded => ProviderName is not null;

    public static OpenAIChatCompletionChainTelemetry None => new(null, null, null);

    public static OpenAIChatCompletionChainTelemetry ForWinner(
        string providerName,
        int index,
        IReadOnlyList<OpenAIChatCompletionAttempt> attempts) =>
        new(providerName, index, JsonSerializer.Serialize(attempts, AttemptJsonOptions));

    public static OpenAIChatCompletionChainTelemetry ForAllFailed(IReadOnlyList<OpenAIChatCompletionAttempt> attempts) =>
        new(null, null, JsonSerializer.Serialize(attempts, AttemptJsonOptions));
}

public sealed class OpenAIChatCompletionChainStreamTelemetryCapture
{
    public OpenAIChatCompletionChainTelemetry Telemetry { get; set; } = OpenAIChatCompletionChainTelemetry.None;

    public int? TerminalHttpStatusCode { get; set; }

    public string? TerminalError { get; set; }

    public string? ResponseModel { get; set; }
}
