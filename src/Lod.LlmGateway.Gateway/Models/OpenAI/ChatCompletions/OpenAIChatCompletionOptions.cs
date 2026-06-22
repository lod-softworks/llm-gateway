namespace Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;

public record class OpenAIChatCompletionOptions
{
    public List<OpenAIChatCompletionProvider> Providers { get; init; } = [];
}
