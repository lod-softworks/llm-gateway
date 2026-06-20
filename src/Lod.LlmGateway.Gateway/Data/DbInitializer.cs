namespace Lod.LlmGateway.Gateway.Data;

public sealed class DbInitializer(GatewayDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }
}
