using Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

namespace Lod.LlmGateway.Gateway.Tests;

public sealed class OpenAIChatCompletionModelResolutionTests
{
    [Theory]
    [InlineData("auto")]
    [InlineData("AUTO")]
    [InlineData(" Auto ")]
    [InlineData("unknown")]
    [InlineData(" UNKNOWN ")]
    public void ResolveForTelemetry_IgnoresNonAuthoritativeResponseBody_UsesConfiguredModel(string responseModel)
    {
        string? resolved = OpenAIChatCompletionModelResolution.ResolveForTelemetry(
            responseModel: responseModel,
            streamCaptureModel: null,
            configuredModel: "my-org/real-model",
            fallbackModel: "client/model");

        Assert.Equal("my-org/real-model", resolved);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("AUTO")]
    [InlineData("unknown")]
    public void ResolveForTelemetry_IgnoresNonAuthoritativeStreamCapture_WhenBodyMissing(string streamModel)
    {
        string? resolved = OpenAIChatCompletionModelResolution.ResolveForTelemetry(
            responseModel: null,
            streamCaptureModel: streamModel,
            configuredModel: "my-org/real-model",
            fallbackModel: "client/model");

        Assert.Equal("my-org/real-model", resolved);
    }

    [Fact]
    public void ResolveForTelemetry_PrefersAuthoritativeResponseBody_OverConfiguredModel()
    {
        string? resolved = OpenAIChatCompletionModelResolution.ResolveForTelemetry(
            responseModel: "upstream/returned-id",
            streamCaptureModel: null,
            configuredModel: "gateway/default",
            fallbackModel: "client/model");

        Assert.Equal("upstream/returned-id", resolved);
    }

    [Fact]
    public void ResolveForTelemetry_UsesStreamWhenBodyMissing()
    {
        string? resolved = OpenAIChatCompletionModelResolution.ResolveForTelemetry(
            responseModel: null,
            streamCaptureModel: "stream/model",
            configuredModel: "gateway/default",
            fallbackModel: "client/model");

        Assert.Equal("stream/model", resolved);
    }

    [Fact]
    public void ResolveForTelemetry_UsesRequestModel_WhenResponseStreamAndConfiguredModelsAreMissing()
    {
        string? resolved = OpenAIChatCompletionModelResolution.ResolveForTelemetry(
            responseModel: null,
            streamCaptureModel: null,
            configuredModel: null,
            fallbackModel: "client/model");

        Assert.Equal("client/model", resolved);
    }

    [Fact]
    public void ResolveForTelemetry_ReturnsNull_WhenAllModelsAreNonAuthoritative()
    {
        string? resolved = OpenAIChatCompletionModelResolution.ResolveForTelemetry(
            responseModel: "unknown",
            streamCaptureModel: "auto",
            configuredModel: " ",
            fallbackModel: null);

        Assert.Null(resolved);
    }
}
