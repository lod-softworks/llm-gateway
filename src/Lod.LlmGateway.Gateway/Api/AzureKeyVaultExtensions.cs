using Azure.Identity;

namespace Lod.LlmGateway.Gateway.Api;

public static class AzureKeyVaultExtensions
{
    public static IConfigurationManager AddAzureKeyVault(this IConfigurationManager configuration)
    {
        string? vaultUri = configuration["AzureKeyVault:VaultUri"] ?? configuration["KeyVault:VaultUri"];

        if (!string.IsNullOrWhiteSpace(vaultUri))
        {
            configuration.AddAzureKeyVault(new Uri(vaultUri), new DefaultAzureCredential());
        }

        return configuration;
    }
}
