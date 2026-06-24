using Lod.LlmGateway.Gateway.Data.OpenAI.ChatCompletions;
using Microsoft.EntityFrameworkCore;

namespace Lod.LlmGateway.Gateway.Data;

public sealed class GatewayDbContext(DbContextOptions<GatewayDbContext> options)
    : DbContext(options)
{
    public DbSet<OpenAIChatCompletionRequestRecord> OpenAIChatCompletionRequest => Set<OpenAIChatCompletionRequestRecord>();

    public DbSet<OpenAIChatCompletionNonStreamRecord> OpenAIChatCompletionNonStream => Set<OpenAIChatCompletionNonStreamRecord>();

    public DbSet<OpenAIChatCompletionStreamRecord> OpenAIChatCompletionStream => Set<OpenAIChatCompletionStreamRecord>();

    public DbSet<OpenAIChatCompletionDailyRollup> ChatCompletionDailyRollup => Set<OpenAIChatCompletionDailyRollup>();
}
