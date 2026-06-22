using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;

namespace Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;

public static class OpenAIChatCompletionProviderMatcher
{
    public static IReadOnlyList<OpenAIChatCompletionProvider> BuildMatchingChain(
        string? requestModel,
        IReadOnlyList<OpenAIChatCompletionProvider> configuredInOrder)
    {
        if (string.IsNullOrWhiteSpace(requestModel))
        {
            return [];
        }

        List<OpenAIChatCompletionProvider> chain = [];
        foreach (OpenAIChatCompletionProvider provider in configuredInOrder)
        {
            if (Matches(provider, requestModel))
            {
                chain.Add(provider);
            }
        }

        return chain;
    }

    public static bool Matches(OpenAIChatCompletionProvider provider, string requestModel)
    {
        if (provider.AcceptAnyModel)
        {
            return true;
        }

        return ResolveMatchedConfiguredModel(provider, requestModel) is not null;
    }

    public static string? ResolveMatchedConfiguredModel(OpenAIChatCompletionProvider provider, string? requestModel)
    {
        if (string.IsNullOrWhiteSpace(requestModel) || provider.Models is not { Length: > 0 })
        {
            return null;
        }

        StringComparison comparison = StringComparison.Ordinal;
        string normalizedRequestModel = provider.IgnoreProviderPrefix
            ? NormalizeModelName(requestModel)
            : requestModel;

        foreach (string? configuredModel in provider.Models)
        {
            if (string.IsNullOrWhiteSpace(configuredModel))
            {
                continue;
            }

            string normalizedConfiguredModel = provider.IgnoreProviderPrefix
                ? NormalizeModelName(configuredModel)
                : configuredModel;

            if (string.Equals(normalizedRequestModel, normalizedConfiguredModel, comparison))
            {
                return configuredModel.Trim();
            }
        }

        return null;
    }

    private static string NormalizeModelName(string model)
    {
        int separatorIndex = model.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex == model.Length - 1)
        {
            return model;
        }

        return model[(separatorIndex + 1)..];
    }
}
