using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;
using Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lod.LlmGateway.Gateway.Tests;

public sealed class OpenAIChatCompletionProviderChainServiceTests
{
    [Fact]
    public async Task TryRunChainAsync_PropagatesTerminalErrorAndHttpStatus_OnAllFailed()
    {
        // Arrange
        var provider1 = new OpenAIChatCompletionProvider
        {
            Name = "Provider1",
            BaseUrl = "http://provider1",
            AcceptAnyModel = true
        };
        var provider2 = new OpenAIChatCompletionProvider
        {
            Name = "Provider2",
            BaseUrl = "http://provider2",
            AcceptAnyModel = true
        };

        var httpHandler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri?.Host == "provider1")
            {
                return new HttpResponseMessage(HttpStatusCode.BadGateway)
                {
                    Content = new StringContent("Bad gateway from provider 1", Encoding.UTF8, "application/json")
                };
            }
            if (req.RequestUri?.Host == "provider2")
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":{\"message\":\"Invalid request format on provider 2\"}}", Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(httpHandler);
        var httpClientFactory = new MockHttpClientFactory(httpClient);
        var httpExecutor = new OpenAIChatCompletionHttpExecutor(httpClientFactory);

        var options = new MutableOptionsMonitor<OpenAIChatCompletionOptions>(new OpenAIChatCompletionOptions
        {
            Providers = [provider1, provider2]
        });

        var service = new OpenAIChatCompletionProviderChainService(
            NullLogger<OpenAIChatCompletionProviderChainService>.Instance,
            options,
            httpExecutor);

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = JsonSerializer.SerializeToElement(new[]
            {
                new { role = "user", content = "hello" }
            })
        };

        // Act
        var result = await service.TryRunChainAsync(request, [provider1, provider2], CancellationToken.None);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Response);
        Assert.Equal(400, result.TerminalHttpStatusCode);
        Assert.Equal("{\"error\":{\"message\":\"Invalid request format on provider 2\"}}", result.TerminalError);
    }

    [Fact]
    public async Task StreamWithChainAsync_PropagatesTerminalErrorAndHttpStatus_OnAllFailed()
    {
        // Arrange
        var provider1 = new OpenAIChatCompletionProvider
        {
            Name = "Provider1",
            BaseUrl = "http://provider1",
            AcceptAnyModel = true
        };
        var provider2 = new OpenAIChatCompletionProvider
        {
            Name = "Provider2",
            BaseUrl = "http://provider2",
            AcceptAnyModel = true
        };

        var httpHandler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri?.Host == "provider1")
            {
                return new HttpResponseMessage(HttpStatusCode.BadGateway)
                {
                    Content = new StringContent("Bad gateway from provider 1", Encoding.UTF8, "application/json")
                };
            }
            if (req.RequestUri?.Host == "provider2")
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":{\"message\":\"Invalid request format on provider 2\"}}", Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(httpHandler);
        var httpClientFactory = new MockHttpClientFactory(httpClient);
        var httpExecutor = new OpenAIChatCompletionHttpExecutor(httpClientFactory);

        var options = new MutableOptionsMonitor<OpenAIChatCompletionOptions>(new OpenAIChatCompletionOptions
        {
            Providers = [provider1, provider2]
        });

        var service = new OpenAIChatCompletionProviderChainService(
            NullLogger<OpenAIChatCompletionProviderChainService>.Instance,
            options,
            httpExecutor);

        var request = new ChatCompletionRequest
        {
            Model = "gpt-4",
            Messages = JsonSerializer.SerializeToElement(new[]
            {
                new { role = "user", content = "hello" }
            }),
            Stream = true
        };

        var capture = new OpenAIChatCompletionChainStreamTelemetryCapture();

        // Act
        var chunks = new List<string>();
        await foreach (var chunk in service.StreamWithChainAsync([provider1, provider2], request, capture, CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Empty(chunks);
        Assert.False(capture.Telemetry.Succeeded);
        Assert.Equal(400, capture.TerminalHttpStatusCode);
        Assert.Equal("{\"error\":{\"message\":\"Invalid request format on provider 2\"}}", capture.TerminalError);
    }

    sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handlerFunc) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handlerFunc(request));
        }
    }

    sealed class MockHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    sealed class MutableOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; set; } = currentValue;

        public TOptions Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<TOptions, string?> listener) => EmptyDisposable.Instance;
    }

    sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
