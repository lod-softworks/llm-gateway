using Lod.LlmGateway.Gateway.Api;
using Lod.LlmGateway.Gateway.Data;
using Lod.LlmGateway.Gateway.Handlers.OpenAI.ChatCompletions;
using Lod.LlmGateway.Gateway.Models.OpenAI.ChatCompletions;
using Lod.LlmGateway.Gateway.Services.OpenAI.ChatCompletions;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureKeyVault();

builder.Services.Configure<Lod.LlmGateway.Gateway.Api.ApiKeyOptions>(builder.Configuration.GetSection("ApiKeys"));
builder.Services.Configure<OpenAIChatCompletionOptions>(builder.Configuration.GetSection("OpenAIChatCompletions"));
builder.Services.AddHttpClient(OpenAIChatCompletionHttpExecutor.HttpClientName, client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSingleton<OpenAIChatCompletionHttpExecutor>();
builder.Services.AddSingleton<OpenAIModelListHttpExecutor>();
builder.Services.AddSingleton<OpenAIChatCompletionProviderChainService>();
builder.Services.AddSingleton<OpenAIModelListProviderService>();

string databaseProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
builder.Services.AddDbContext<GatewayDbContext>(options =>
{
    string? connectionString = builder.Configuration.GetConnectionString("Gateway");
    if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString, c =>
        {
            c.MigrationsHistoryTable("__EFMigrationsHistory", "llm_gateway");
        });
    }
    else
    {
        throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'. Supported values are 'SqlServer' and 'Sqlite'.");
    }
});
builder.Services.AddScoped<OpenAIChatCompletionTelemetryWriter>();
builder.Services.AddSingleton<ApiKeyAuthorizer>();
builder.Services.AddScoped<OpenAIChatCompletionHandler>();
builder.Services.AddScoped<OpenAIModelListHandler>();
builder.Services.AddHostedService<OpenAIChatCompletionDailyRollupWorker>();

builder.Services.AddRazorPages();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        if (!GatewayResults.IsSupportedApiPath(context.Request.Path))
        {
            return;
        }

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await GatewayResults.WriteEndpointErrorAsync(
            context,
            StatusCodes.Status500InternalServerError,
            "An unexpected server error occurred.");
    });
});

app.UseStatusCodePages(async statusContext =>
{
    HttpContext context = statusContext.HttpContext;
    if (!GatewayResults.IsSupportedApiPath(context.Request.Path))
    {
        return;
    }

    if (context.Response.HasStarted ||
        !string.IsNullOrWhiteSpace(context.Response.ContentType) ||
        context.Response.ContentLength is > 0)
    {
        return;
    }

    await GatewayResults.WriteEndpointErrorAsync(context, context.Response.StatusCode);
});

app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("LLM Gateway");
    });
}

app.MapRazorPages();

RouteGroupBuilder openAiGroup = app.MapGroup("/v1")
    .WithTags("OpenAI");

openAiGroup.MapPost("/chat/completions", async (HttpContext context, OpenAIChatCompletionHandler handler, CancellationToken cancellationToken) =>
        await handler.HandleAsync(context, cancellationToken))
    .WithSummary("Chat with the LLM using OpenAI Chat Completions")
    .WithDescription("""
                     OpenAI-compatible chat completions endpoint. The gateway matches the requested model to an ordered list of configured OpenAI-compatible HTTP providers. A chain of all matching providers is run in order until a step succeeds.

                     The gateway deserializes the request into a full OpenAI-style chat-completions object, preserves JSON extension properties, and forwards the merged body to the upstream provider. Per-step `Model` overrides and `stream` flags are applied for each hop. `max_completion_tokens` to `max_tokens` mirroring is configurable per provider (`CopyMaxCompletionTokensToMaxTokens`) so cloud providers can preserve exact request semantics while LM Studio-oriented providers can enable legacy-field compatibility. Unsupported fields on a given upstream are rejected by that server, not stripped by the gateway.

                     Official OpenAI API documentation:
                     - https://platform.openai.com/docs/api-reference/chat

                     Gateway note: additional request properties not listed in the OpenAPI contract are still accepted at runtime and passed through when serializing the outbound request.
                     """)
    .Accepts<ChatCompletionRequest>("application/json")
    .Produces<ChatCompletionResponse>(StatusCodes.Status200OK, "application/json");

openAiGroup.MapGet("/models", async (HttpContext context, OpenAIModelListHandler handler, CancellationToken cancellationToken) =>
        await handler.HandleAsync(context, cancellationToken))
    .WithSummary("List available OpenAI models")
    .WithDescription("""
                    OpenAI-compatible model listing endpoint. The gateway queries configured OpenAI-compatible HTTP providers in order and returns the first successful `/v1/models` response.
                    """)
    .Produces<OpenAIModelListResponse>(StatusCodes.Status200OK, "application/json");

using (IServiceScope scope = app.Services.CreateScope())
{
    DbContext dbContext = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

    if (dbContext.Database.IsSqlite())
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
    else
    {
        await dbContext.Database.MigrateAsync();
    }
}

await app.RunAsync();
