using Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

namespace Lod.LlmGateway.Gateway.Tests;

public sealed class OpenAIChatCompletionProviderMatcherTests
{
    [Fact]
    public void Matches_ReturnsTrue_ForExactMatch()
    {
        OpenAIChatCompletionProvider provider = new()
        {
            Models = ["gpt-4.1"]
        };

        bool matched = OpenAIChatCompletionProviderMatcher.Matches(provider, "gpt-4.1");

        Assert.True(matched);
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenIgnorePrefixEnabled_AndSuffixesAreEqual()
    {
        OpenAIChatCompletionProvider provider = new()
        {
            IgnoreProviderPrefix = true,
            Models = ["openai/gpt-4.1"]
        };

        bool matched = OpenAIChatCompletionProviderMatcher.Matches(provider, "my-org/gpt-4.1");

        Assert.True(matched);
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenIgnorePrefixEnabled_AndOnlyPartialSuffixOverlaps()
    {
        OpenAIChatCompletionProvider provider = new()
        {
            IgnoreProviderPrefix = true,
            Models = ["openai/gpt-4.1"]
        };

        bool matched = OpenAIChatCompletionProviderMatcher.Matches(provider, "my-org/4.1");

        Assert.False(matched);
    }

    [Fact]
    public void Matches_UsesCaseSensitiveComparison()
    {
        OpenAIChatCompletionProvider provider = new()
        {
            IgnoreProviderPrefix = true,
            Models = ["openai/GPT-4.1"]
        };

        bool matched = OpenAIChatCompletionProviderMatcher.Matches(provider, "my-org/gpt-4.1");

        Assert.False(matched);
    }

    [Fact]
    public void BuildMatchingChain_FiltersOnlyMatchingProvidersInOrder()
    {
        OpenAIChatCompletionProvider firstProvider = new()
        {
            Name = "first",
            IgnoreProviderPrefix = true,
            Models = ["openai/gpt-4.1"]
        };

        OpenAIChatCompletionProvider secondProvider = new()
        {
            Name = "second",
            Models = ["gpt-4o"]
        };

        OpenAIChatCompletionProvider thirdProvider = new()
        {
            Name = "third",
            AcceptAnyModel = true
        };

        IReadOnlyList<OpenAIChatCompletionProvider> chain = OpenAIChatCompletionProviderMatcher.BuildMatchingChain(
            "tenant/gpt-4.1",
            [firstProvider, secondProvider, thirdProvider]);

        Assert.Equal(["first", "third"], chain.Select(provider => provider.Name));
    }

    [Fact]
    public void ResolveMatchedConfiguredModel_ReturnsExactMatchedModel()
    {
        OpenAIChatCompletionProvider provider = new()
        {
            Models = ["gpt-4.1", "gpt-4o"]
        };

        string? model = OpenAIChatCompletionProviderMatcher.ResolveMatchedConfiguredModel(provider, "gpt-4o");

        Assert.Equal("gpt-4o", model);
    }

    [Fact]
    public void ResolveMatchedConfiguredModel_ReturnsConfiguredModel_WhenIgnorePrefixEnabled()
    {
        OpenAIChatCompletionProvider provider = new()
        {
            IgnoreProviderPrefix = true,
            Models = ["openai/gpt-4.1", "openai/gpt-4o"]
        };

        string? model = OpenAIChatCompletionProviderMatcher.ResolveMatchedConfiguredModel(provider, "tenant/gpt-4o");

        Assert.Equal("openai/gpt-4o", model);
    }

    [Fact]
    public void ResolveMatchedConfiguredModel_ReturnsNull_ForAcceptAnyProviderWithoutConfiguredMatch()
    {
        OpenAIChatCompletionProvider provider = new()
        {
            AcceptAnyModel = true
        };

        string? model = OpenAIChatCompletionProviderMatcher.ResolveMatchedConfiguredModel(provider, "client/model");

        Assert.Null(model);
    }

}
