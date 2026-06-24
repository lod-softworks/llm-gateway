using Microsoft.Extensions.Options;
using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;

namespace Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;

public sealed class OpenAIModelListProviderService(
    ILogger<OpenAIModelListProviderService> logger,
    IOptionsMonitor<OpenAIChatCompletionOptions> options,
    OpenAIModelListHttpExecutor httpExecutor)
{
    public async Task<OpenAIModelListResponse> ListModelsAsync(CancellationToken cancellationToken)
    {
        List<OpenAIChatCompletionProvider> providers = options.CurrentValue.Providers;
        if (providers.Count == 0)
        {
            throw new InvalidOperationException("No OpenAI providers are configured.");
        }

        Exception? lastError = null;
        for (int i = 0; i < providers.Count; i++)
        {
            OpenAIChatCompletionProvider provider = providers[i];
            try
            {
                return await httpExecutor.ListModelsAsync(provider, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "OpenAI model-list provider step '{Name}' (index {Index}) failed.", provider.Name, i);
                lastError = ex;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OpenAI model-list provider step '{Name}' (index {Index}) failed.", provider.Name, i);
                lastError = ex;
            }
        }

        throw new InvalidOperationException(
            "All OpenAI model-list provider steps failed.",
            lastError);
    }
}
