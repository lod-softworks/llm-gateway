using Lod.LlmGateway.Gateway.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Lod.LlmGateway.Gateway.Tests;

public sealed class ApiKeyAuthorizerTests
{
    [Fact]
    public void Authorization_UsesCurrentOptions()
    {
        MutableOptionsMonitor<ApiKeyOptions> options = new(new ApiKeyOptions
        {
            Clients = new Dictionary<string, string> { ["first"] = "old-key" }
        });
        ApiKeyAuthorizer authorizer = new(options);
        DefaultHttpContext context = new();
        context.Request.Headers["X-Api-Key"] = "old-key";

        Assert.True(authorizer.IsClientAuthorized(context));

        options.CurrentValue = new ApiKeyOptions
        {
            Clients = new Dictionary<string, string> { ["second"] = "new-key" }
        };

        Assert.False(authorizer.IsClientAuthorized(context));

        context.Request.Headers["X-Api-Key"] = "new-key";
        Assert.True(authorizer.IsClientAuthorized(context));
        Assert.Equal("second", authorizer.GetAuthorizedClientName(context));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Authorization_RejectsEmptyConfiguredKeys(string? emptyConfiguredKey)
    {
        MutableOptionsMonitor<ApiKeyOptions> options = new(new ApiKeyOptions
        {
            Clients = new Dictionary<string, string> { ["first"] = emptyConfiguredKey! }
        });
        ApiKeyAuthorizer authorizer = new(options);
        
        DefaultHttpContext context = new();
        context.Request.Headers["X-Api-Key"] = "";
        Assert.False(authorizer.IsClientAuthorized(context));

        context.Request.Headers["X-Api-Key"] = "some-key";
        Assert.False(authorizer.IsClientAuthorized(context));

        DefaultHttpContext contextNull = new();
        Assert.False(authorizer.IsClientAuthorized(contextNull));
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
