using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Security.Cryptography;
using System.Text;

namespace Lod.LlmGateway.Gateway.Api;

public sealed class ApiKeyAuthorizer(IOptionsMonitor<ApiKeyOptions> options)
{
    public bool IsClientAuthorized(HttpContext httpContext)
    {
        return GetAuthorizedClientName(httpContext) is not null;
    }


    public string? GetAuthorizedClientName(HttpContext httpContext)
    {
        string? apiKey = GetApiKey(httpContext);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        byte[] apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);

        foreach (KeyValuePair<string, string> client in options.CurrentValue.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.Value))
            {
                continue;
            }

            byte[] clientKeyBytes = Encoding.UTF8.GetBytes(client.Value);
            if (CryptographicOperations.FixedTimeEquals(apiKeyBytes, clientKeyBytes))
            {
                return client.Key;
            }
        }

        return null;
    }

    public static string ObfuscateKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 4)
        {
            return "****";
        }
        int visibleChars = Math.Min(4, key.Length / 2);
        return string.Concat(new string('*', key.Length - visibleChars), key.AsSpan(key.Length - visibleChars));
    }

    public static string? GetApiKey(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("X-Api-Key", out StringValues values))
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (httpContext.Request.Headers.TryGetValue("AuthToken", out StringValues authTokenValues))
        {
            foreach (string? value in authTokenValues)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out StringValues authorizationValues))
        {
            foreach (string? value in authorizationValues)
            {
                string? credential = GetCredentialFromAuthorizationValue(value);
                if (!string.IsNullOrWhiteSpace(credential))
                {
                    return credential;
                }
            }
        }

        if (httpContext.Request.Query.TryGetValue("apiKey", out StringValues queryValues))
        {
            foreach (string? value in queryValues)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    public static string? GetCredentialFromAuthorizationValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        ReadOnlySpan<char> span = raw.AsSpan().Trim();
        int space = span.IndexOf(' ');
        if (space < 0)
        {
            return new string(span);
        }

        ReadOnlySpan<char> remainder = span[(space + 1)..].Trim();
        return remainder.Length is 0 ? null : new string(remainder);
    }
}
