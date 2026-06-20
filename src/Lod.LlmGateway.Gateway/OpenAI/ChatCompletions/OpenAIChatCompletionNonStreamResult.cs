namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

public sealed record OpenAIChatCompletionNonStreamResult(
    bool Succeeded,
    ChatCompletionResponse? Response,
    OpenAIChatCompletionChainTelemetry ChainTelemetry,
    int? TerminalHttpStatusCode = null,
    string? TerminalError = null);
