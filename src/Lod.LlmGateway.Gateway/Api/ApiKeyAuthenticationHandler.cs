using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Lod.LlmGateway.Gateway.Api;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ApiKeyAuthorizer _apiKeyAuthorizer;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ApiKeyAuthorizer apiKeyAuthorizer)
        : base(options, logger, encoder)
    {
        _apiKeyAuthorizer = apiKeyAuthorizer;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? clientName = _apiKeyAuthorizer.GetAuthorizedClientName(Context);
        if (clientName is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or invalid API key."));
        }

        Claim[] claims = [
            new Claim(ClaimTypes.Name, clientName),
            new Claim("client_name", clientName)
        ];
        ClaimsIdentity identity = new(claims, Scheme.Name);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        await GatewayResults.WriteEndpointErrorAsync(Response.HttpContext, StatusCodes.Status401Unauthorized, "Missing or invalid API key.");
    }
}
