using Microsoft.Extensions.Options;
using System.Net;
using System.Runtime.CompilerServices;

namespace Lod.LlmGateway.Gateway.OpenAI.ChatCompletions;

public sealed class OpenAIChatCompletionProviderChainService(
    ILogger<OpenAIChatCompletionProviderChainService> logger,
    IOptions<OpenAIChatCompletionOptions> options,
    OpenAIChatCompletionHttpExecutor httpExecutor)
{
    public IReadOnlyList<OpenAIChatCompletionProvider> BuildMatchingChain(string? requestModel) =>
        OpenAIChatCompletionProviderMatcher.BuildMatchingChain(requestModel, options.Value.Providers);

    public async Task<OpenAIChatCompletionNonStreamResult> TryRunChainAsync(
        ChatCompletionRequest request,
        IReadOnlyList<OpenAIChatCompletionProvider> chain,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chain.Count, 1);

        List<OpenAIChatCompletionAttempt> attempts = [];
        for (int i = 0; i < chain.Count; i++)
        {
            OpenAIChatCompletionProvider step = chain[i];
            try
            {
                ChatCompletionResponse response = await httpExecutor
                    .CreateChatCompletionAsync(step, request, cancellationToken)
                    .ConfigureAwait(false);
                attempts.Add(new OpenAIChatCompletionAttempt(step.Name, i, true, 200, null));
                return new OpenAIChatCompletionNonStreamResult(
                    true,
                    response,
                    OpenAIChatCompletionChainTelemetry.ForWinner(step.Name, i, attempts));
            }
            catch (HttpRequestException ex)
            {
                attempts.Add(new OpenAIChatCompletionAttempt(
                    step.Name, i, false, (int?)ex.StatusCode, ex.Message));
                logger.LogWarning(ex, "OpenAI provider chain step '{Name}' (Api, index {Index}) failed.", step.Name, i);
                if (ex.StatusCode == HttpStatusCode.BadRequest)
                {
                    return new OpenAIChatCompletionNonStreamResult(
                        false,
                        null,
                        OpenAIChatCompletionChainTelemetry.ForAllFailed(attempts),
                        (int)HttpStatusCode.BadRequest,
                        ex.Message);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempts.Add(new OpenAIChatCompletionAttempt(step.Name, i, false, null, ex.Message));
                logger.LogWarning(ex, "OpenAI provider chain step '{Name}' (Api, index {Index}) failed.", step.Name, i);
            }
        }
        return new OpenAIChatCompletionNonStreamResult(false, null, OpenAIChatCompletionChainTelemetry.ForAllFailed(attempts));
    }

    public async IAsyncEnumerable<string> StreamWithChainAsync(
        IReadOnlyList<OpenAIChatCompletionProvider> chain,
        ChatCompletionRequest request,
        OpenAIChatCompletionChainStreamTelemetryCapture capture,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chain.Count, 1);

        List<OpenAIChatCompletionAttempt> attempts = [];
        for (int i = 0; i < chain.Count; i++)
        {
            OpenAIChatCompletionProvider step = chain[i];
            IAsyncEnumerable<string> single = httpExecutor.CreateChatCompletionStreamAsync(
                step,
                request,
                cancellationToken);
            await using IAsyncEnumerator<string> enumerator = single.GetAsyncEnumerator(cancellationToken);
            bool moved;
            try
            {
                moved = await enumerator.MoveNextAsync();
            }
            catch (HttpRequestException ex)
            {
                attempts.Add(new OpenAIChatCompletionAttempt(
                    step.Name, i, false, (int?)ex.StatusCode, ex.Message));
                logger.LogWarning(
                    ex, "OpenAI provider chain step '{Name}' (Api, index {Index}) failed while opening stream.", step.Name, i);
                if (ex.StatusCode == HttpStatusCode.BadRequest)
                {
                    capture.Telemetry = OpenAIChatCompletionChainTelemetry.ForAllFailed(attempts);
                    capture.TerminalHttpStatusCode = (int)HttpStatusCode.BadRequest;
                    capture.TerminalError = ex.Message;
                    yield break;
                }

                continue;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempts.Add(new OpenAIChatCompletionAttempt(step.Name, i, false, null, ex.Message));
                logger.LogWarning(
                    ex, "OpenAI provider chain step '{Name}' (Api, index {Index}) failed while opening stream.", step.Name, i);
                continue;
            }

            if (moved is false)
            {
                attempts.Add(new OpenAIChatCompletionAttempt(
                    step.Name, i, false, null, "Empty stream from provider."));
                continue;
            }

            attempts.Add(new OpenAIChatCompletionAttempt(step.Name, i, true, 200, null));
            capture.Telemetry = OpenAIChatCompletionChainTelemetry.ForWinner(step.Name, i, attempts);
            capture.ResponseModel = ResolveResponseModel(step);

            yield return enumerator.Current;
            while (await enumerator.MoveNextAsync())
            {
                yield return enumerator.Current;
            }

            yield break;
        }
        capture.Telemetry = OpenAIChatCompletionChainTelemetry.ForAllFailed(attempts);
    }

    static string? ResolveResponseModel(OpenAIChatCompletionProvider provider) =>
        string.IsNullOrWhiteSpace(provider.Model) is false ? provider.Model : null;
}
