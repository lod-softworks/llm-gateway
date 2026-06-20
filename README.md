# LLM Gateway

LLM Gateway is an ASP.NET Core routing and failover API for OpenAI-compatible model providers. Clients use one API endpoint while the gateway selects matching upstream providers in configured order and advances to the next provider when an attempt fails.

This repository contains the routing/failover API only. The worker runtime and its worker-facing gateway are maintained as a separate project.

## Capabilities

- OpenAI-compatible `POST /v1/chat/completions`
- OpenAI-compatible `GET /v1/models`
- Streaming and non-streaming chat completions
- Ordered provider matching and failover by requested model
- Per-provider model overrides, headers, authentication, and request compatibility settings
- API-key authentication for clients
- EF Core telemetry using SQL Server or SQLite
- Optional Azure Key Vault configuration
- Development OpenAPI and Scalar documentation

## Repository Layout

```text
src/
  Lod.LlmGateway.Gateway/
    Lod.LlmGateway.Gateway.csproj
    Api/                         Shared HTTP and authentication infrastructure
    OpenAI/ChatCompletions/      OpenAI contracts, routing, handlers, and telemetry
  Lod.LlmGateway.Gateway.Tests/
    Lod.LlmGateway.Gateway.Tests.csproj
```

The solution has one deployable API project and one test project. Provider contracts and models are compiled directly into the API assembly.

## Prerequisites

- .NET 10 SDK
- SQL Server or SQLite
- One or more OpenAI-compatible upstream APIs

## Run Locally

```bash
dotnet run --project src/Lod.LlmGateway.Gateway/Lod.LlmGateway.Gateway.csproj
```

The development configuration uses SQLite and assumes an LM Studio-compatible OpenAI endpoint at `http://localhost:1234`.

Run tests with:

```bash
dotnet test src/Lod.LlmGateway.slnx
```

## Configuration

### Client API Keys

```json
{
  "ApiKeys": {
    "Clients": {
      "development": "replace-with-a-secret"
    }
  }
}
```

Clients can provide the key using `X-Api-Key`, `AuthToken`, `Authorization`, or the `apiKey` query parameter.

### Providers

Providers are evaluated in configuration order. Every provider whose `Models` or `AcceptAnyModel` setting matches the requested model becomes part of the failover chain.

```json
{
  "OpenAIChatCompletions": {
    "Providers": [
      {
        "Name": "Local",
        "BaseUrl": "http://localhost:1234",
        "AcceptAnyModel": true,
        "CopyMaxCompletionTokensToMaxTokens": true
      },
      {
        "Name": "Cloud",
        "BaseUrl": "https://api.example.com",
        "Models": [ "gpt-5" ],
        "AuthToken": "replace-with-a-secret"
      }
    ]
  }
}
```

### Azure Key Vault

Set `AzureKeyVault:VaultUri` to load secrets through `DefaultAzureCredential`. When the value is empty or absent, Key Vault is not added as a configuration source.

```json
{
  "AzureKeyVault": {
    "VaultUri": "https://example.vault.azure.net/"
  }
}
```

Use standard ASP.NET Core configuration key mapping for Key Vault secret names.

### Database

Set `Database:Provider` to `SqlServer` or `Sqlite`, and set `ConnectionStrings:Gateway` to the corresponding connection string.

## Azure App Service

The root `.deployment` file directs Kudu to the deployable API project under `src`.

Configure the App Service runtime stack for .NET 10. For source ZIP deployments, set `SCM_DO_BUILD_DURING_DEPLOYMENT=true`; pre-published ZIP deployments should leave remote build disabled.

## Documentation

See [REQUIREMENTS.md](REQUIREMENTS.md) for product behavior, constraints, and acceptance criteria.
