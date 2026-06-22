using Lod.LlmGateway.Gateway.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.IO;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Lod.LlmGateway.Gateway.Tests;

public sealed class ApiKeyAuthenticationHandlerTests
{
    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsSuccess_WhenKeyIsValid()
    {
        // Arrange
        var optionsMonitor = new MutableOptionsMonitor<ApiKeyOptions>(new ApiKeyOptions
        {
            Clients = new Dictionary<string, string> { ["client-1"] = "valid-key" }
        });
        var authorizer = new ApiKeyAuthorizer(optionsMonitor);

        var schemeOptions = new MockOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions());
        
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "valid-key";

        var handler = new ApiKeyAuthenticationHandler(
            schemeOptions,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            authorizer);

        var scheme = new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Principal);
        Assert.Equal("client-1", result.Principal.Identity?.Name);
        Assert.Equal("client-1", result.Principal.FindFirst("client_name")?.Value);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ReturnsFailure_WhenKeyIsInvalid()
    {
        // Arrange
        var optionsMonitor = new MutableOptionsMonitor<ApiKeyOptions>(new ApiKeyOptions
        {
            Clients = new Dictionary<string, string> { ["client-1"] = "valid-key" }
        });
        var authorizer = new ApiKeyAuthorizer(optionsMonitor);

        var schemeOptions = new MockOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions());
        
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Api-Key"] = "invalid-key";

        var handler = new ApiKeyAuthenticationHandler(
            schemeOptions,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            authorizer);

        var scheme = new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("Missing or invalid API key.", result.Failure?.Message);
    }

    [Fact]
    public async Task ChallengeAsync_WritesOpenAiErrorResponse()
    {
        // Arrange
        var optionsMonitor = new MutableOptionsMonitor<ApiKeyOptions>(new ApiKeyOptions
        {
            Clients = new Dictionary<string, string> { ["client-1"] = "valid-key" }
        });
        var authorizer = new ApiKeyAuthorizer(optionsMonitor);

        var schemeOptions = new MockOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions());
        
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Setup dependency injection for context (needed for WriteEndpointErrorAsync)
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(_ => { });
        context.RequestServices = services.BuildServiceProvider();

        var handler = new ApiKeyAuthenticationHandler(
            schemeOptions,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            authorizer);

        var scheme = new AuthenticationScheme("ApiKey", "ApiKey", typeof(ApiKeyAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        string body = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("Missing or invalid API key.", doc.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal("authentication_error", doc.RootElement.GetProperty("error").GetProperty("type").GetString());
    }

    sealed class MutableOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; set; } = currentValue;
        public TOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<TOptions, string?> listener) => EmptyDisposable.Instance;
    }

    sealed class MockOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = currentValue;
        public TOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<TOptions, string?> listener) => EmptyDisposable.Instance;
    }

    sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();
        public void Dispose() {}
    }
}
