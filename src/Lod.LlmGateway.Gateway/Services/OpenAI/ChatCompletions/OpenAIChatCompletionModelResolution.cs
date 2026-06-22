namespace Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;

public static class OpenAIChatCompletionModelResolution
{
    public static string? ResolveForTelemetry(
        string? responseModel,
        string? streamCaptureModel,
        string? configuredModel,
        string? fallbackModel)
    {
        if (IsAuthoritativeUpstreamModelLabel(responseModel))
        {
            return responseModel!.Trim();
        }

        if (IsAuthoritativeUpstreamModelLabel(streamCaptureModel))
        {
            return streamCaptureModel!.Trim();
        }

        if (IsAuthoritativeUpstreamModelLabel(configuredModel))
        {
            return configuredModel!.Trim();
        }

        if (IsAuthoritativeUpstreamModelLabel(fallbackModel))
        {
            return fallbackModel!.Trim();
        }

        return null;
    }

    public static bool IsAuthoritativeUpstreamModelLabel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return false;
        }

        string trimmed = model.Trim();
        return string.Equals(trimmed, "auto", StringComparison.OrdinalIgnoreCase) is false
            && string.Equals(trimmed, "unknown", StringComparison.OrdinalIgnoreCase) is false;
    }
}
