using Lod.LlmGateway.Gateway.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Lod.LlmGateway.Gateway.Tests;

public sealed class GatewayResultsTests
{
    [Fact]
    public async Task OpenAiAuthError_ReturnsJsonPayload()
    {
        IResult result = GatewayResults.OpenAIError(
            StatusCodes.Status401Unauthorized,
            "Missing or invalid API key.",
            type: "authentication_error");

        (int statusCode, string contentType, string body) = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, statusCode);
        Assert.Equal("application/json", contentType);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("Missing or invalid API key.", document.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal("authentication_error", document.RootElement.GetProperty("error").GetProperty("type").GetString());
    }

    [Fact]
    public async Task OpenAiBadRequestError_ReturnsJsonPayload()
    {
        IResult result = GatewayResults.OpenAIError(
            StatusCodes.Status400BadRequest,
            "Invalid chat completion request.",
            type: "invalid_request_error");

        (int statusCode, string contentType, string body) = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal("application/json", contentType);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal("Invalid chat completion request.", document.RootElement.GetProperty("error").GetProperty("message").GetString());
        Assert.Equal("invalid_request_error", document.RootElement.GetProperty("error").GetProperty("type").GetString());
    }

    static async Task<(int statusCode, string contentType, string body)> ExecuteAsync(IResult result)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(_ => { });

        DefaultHttpContext context = new();
        context.RequestServices = services.BuildServiceProvider();
        context.Response.Body = new MemoryStream();
        await result.ExecuteAsync(context);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new(context.Response.Body);
        string body = await reader.ReadToEndAsync();
        return (context.Response.StatusCode, context.Response.ContentType ?? string.Empty, body);
    }
}
