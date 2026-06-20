using Microsoft.AspNetCore.WebUtilities;

namespace Lod.LlmGateway.Gateway.Api;

public static class GatewayResults
{
    public static IResult OpenAIError(int statusCode, string message, string? type = null, string? code = null)
    {
        object payload = new
        {
            error = new
            {
                message,
                type = string.IsNullOrWhiteSpace(type) ? "gateway_error" : type,
                code
            }
        };

        return Results.Json(payload, statusCode: statusCode, contentType: "application/json");
    }

    public static Task WriteEndpointErrorAsync(HttpContext context, int statusCode, string? message = null)
    {
        string resolvedMessage = string.IsNullOrWhiteSpace(message)
            ? ReasonPhrases.GetReasonPhrase(statusCode)
            : message!;

        IResult result = OpenAIError(statusCode, resolvedMessage, type: StatusToOpenAiType(statusCode));

        return result.ExecuteAsync(context);
    }

    public static bool IsSupportedApiPath(PathString path) =>
        IsOpenAiPath(path);

    static bool IsOpenAiPath(PathString path) =>
        path.StartsWithSegments("/v1", StringComparison.OrdinalIgnoreCase);

    static string StatusToOpenAiType(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "invalid_request_error",
            StatusCodes.Status401Unauthorized => "authentication_error",
            StatusCodes.Status403Forbidden => "permission_error",
            StatusCodes.Status429TooManyRequests => "rate_limit_error",
            _ when statusCode >= 500 => "server_error",
            _ => "gateway_error"
        };
}
