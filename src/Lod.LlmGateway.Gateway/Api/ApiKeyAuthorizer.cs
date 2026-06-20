using Microsoft.Extensions.Options;

namespace Lod.LlmGateway.Gateway.Api;

public sealed class ApiKeyAuthorizer(IOptionsMonitor<ApiKeyOptions> options)
{
    public IEnumerable<string> ClientObfuscatedKeys => options.CurrentValue.Clients.Values.Select(ObfuscateKey);

    public bool IsClientAuthorized(HttpContext httpContext)
    {
        return options.CurrentValue.Clients.Any(kv => IsAuthorized(httpContext, kv.Value));
    }


    public string? GetAuthorizedClientName(HttpContext httpContext)
    {
        string? apiKey = GetApiKey(httpContext);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        foreach (KeyValuePair<string, string> client in options.CurrentValue.Clients)
        {
            if (string.Equals(client.Value, apiKey, StringComparison.Ordinal))
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
        if (httpContext.Request.Headers.TryGetValue("X-Api-Key", out var values))
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (httpContext.Request.Headers.TryGetValue("AuthToken", out var authTokenValues))
        {
            foreach (var value in authTokenValues)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authorizationValues))
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

        if (httpContext.Request.Query.TryGetValue("apiKey", out var queryValues))
        {
            foreach (var value in queryValues)
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

    static bool IsAuthorized(HttpContext httpContext, string? configuredKey) =>
        string.IsNullOrWhiteSpace(configuredKey) ||
        string.Equals(GetApiKey(httpContext), configuredKey, StringComparison.Ordinal);
}
