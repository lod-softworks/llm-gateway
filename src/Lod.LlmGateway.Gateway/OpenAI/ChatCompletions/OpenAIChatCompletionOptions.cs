namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

public record class OpenAIChatCompletionOptions
{
    public List<OpenAIChatCompletionProvider> Providers { get; init; } = [];
}
