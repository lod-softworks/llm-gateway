# Product Requirements Document

## 1. Product

LLM Gateway is a routing and failover API for OpenAI-compatible chat completion providers. It presents a stable client-facing API while selecting an ordered chain of matching upstream HTTP providers for each request.

This repository owns the routing/failover API. Worker execution, worker WebSocket connectivity, and the gateway dedicated to worker dispatch are outside this repository.

## 2. Goals

- Provide OpenAI-compatible chat completions and model listing endpoints.
- Preserve supported and unknown chat-completion request fields when forwarding requests.
- Route requests by model to configured upstream HTTP providers.
- Fail over through matching providers in deterministic configuration order.
- Support streaming and non-streaming responses.
- Authenticate clients with configured API keys.
- Persist request, routing, usage, latency, and cost telemetry.
- Load secrets from Azure Key Vault when explicitly configured.
- Produce one deployable API assembly plus one test assembly.

## 3. Non-Goals

- Durable request queues or replay.
- Multi-region gateway coordination.
- OpenAI APIs other than chat completions and model listing.
- Tenant billing, quotas, or advanced capacity scheduling.

## 4. API

### 4.1 Chat Completions

`POST /v1/chat/completions`

- Requires a configured client API key.
- Accepts OpenAI-style chat completion requests.
- Requires a non-empty `messages` array.
- Supports JSON and server-sent event responses.
- Preserves extension properties and forwards them to the selected upstream.
- Emits `[DONE]` for completed streams.

### 4.2 Models

`GET /v1/models`

- Requires a configured client API key.
- Queries configured providers in order.
- Returns the first successful OpenAI-compatible model-list response.

### 4.3 Errors

- Unauthorized requests return `401` OpenAI-style JSON.
- Invalid request payloads return `400` OpenAI-style JSON.
- An unmatched model returns `400`.
- A provider `400` response stops the chain and is returned as an invalid request.
- Other provider failures advance to the next matching provider.
- Exhausted non-streaming chains return `502`.
- Exhausted streaming chains emit an OpenAI-style error event.

## 5. Routing and Failover

Configuration path: `OpenAIChatCompletions:Providers`.

Each provider supports:

- `Name`: stable provider identifier used in telemetry.
- `Models`: exact model names accepted by the provider.
- `AcceptAnyModel`: matches any requested model.
- `IgnoreProviderPrefix`: compares the portion after the first `/`.
- `BaseUrl`: upstream OpenAI-compatible base URL.
- `Model`: optional outbound model override.
- `AuthToken`: optional bearer token.
- `Headers`: optional outbound headers.
- `CopyMaxCompletionTokensToMaxTokens`: compatibility mapping for providers that require `max_tokens`.

All matching providers form a chain in configuration order. The first successful provider wins. Every attempt is captured in telemetry.

Only direct HTTP API providers are supported in this repository.

## 6. Authentication

Configuration path: `ApiKeys:Clients`.

The value is a dictionary of client names to API keys. The client name is recorded in telemetry.

Accepted transports:

- `X-Api-Key`
- `AuthToken`
- `Authorization`
- `apiKey` query parameter

For `Authorization`, the scheme is removed before key comparison.

## 7. Configuration Sources

The application uses standard ASP.NET Core configuration.

When `AzureKeyVault:VaultUri` is non-empty, the application adds Azure Key Vault using:

- `Azure.Extensions.AspNetCore.Configuration.Secrets`
- `Azure.Identity`
- `DefaultAzureCredential`

When `AzureKeyVault:VaultUri` is absent or empty, no Key Vault connection is attempted.

## 8. Persistence and Telemetry

Supported providers:

- SQL Server
- SQLite

Configuration:

- `Database:Provider`
- `ConnectionStrings:Gateway`

Telemetry records:

- Request and response timestamps
- Client identity
- Requested, configured, and served models
- Streaming status
- Provider winner and winner index
- Ordered provider-attempt JSON
- HTTP status and error
- Token usage and throughput
- Provider-supplied cost metadata when available
- Daily rollups by UTC day and client

SQL Server tables use the `llm_gateway` schema.

## 9. Project Structure

The solution contains:

1. `src/Lod.LlmGateway.Gateway`: deployable ASP.NET Core API.
2. `src/Lod.LlmGateway.Gateway.Tests`: automated tests.

Provider-owned models and services are organized under:

- `OpenAI/ChatCompletions`

There is no separate abstractions project.

## 10. Operational Requirements

- Development exposes OpenAPI JSON and Scalar.
- Unsupported database providers fail during startup.
- Database initialization runs during startup.
- Logs capture authorization failures and provider attempt failures.
- Cancellation from the client is propagated to upstream requests.

## 11. Acceptance Criteria

1. The solution builds with one API project and one test project.
2. No deployable worker-host or abstractions project is present in `src`.
3. `POST /v1/chat/completions` routes through matching HTTP providers in order.
4. Failed providers advance to the next provider except for upstream `400` responses.
5. Streaming and non-streaming requests preserve endpoint-native response behavior.
6. API clients are authenticated using configured keys.
7. Azure Key Vault is loaded only when `AzureKeyVault:VaultUri` is configured.
8. Telemetry is persisted through EF Core.
9. Automated tests pass.

## 12. Documentation Maintenance

- Keep this file current when API behavior, configuration, routing rules, or persistence changes.
- Keep `README.md` focused on human onboarding and operation.
- Keep repository-specific agent guidance in `.agents`.
