namespace Lod.LlmGateway.Gateway.Api;

public record class ApiKeyOptions
{
    public Dictionary<string, string> Clients { get; init; } = [];
}
