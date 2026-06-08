using System.Text.Json;
using ShippingPricingService.Infrastructure.Persistence;

namespace ShippingPricingService.Infrastructure.Outbox;

public sealed class EfOutboxWriter : IOutboxWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PricingDbContext _dbContext;

    public EfOutboxWriter(PricingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(string eventType, object payload, CancellationToken cancellationToken)
    {
        var message = new OutboxMessage(Guid.NewGuid(), eventType, JsonSerializer.Serialize(payload, JsonOptions), DateTimeOffset.UtcNow);
        _dbContext.OutboxMessages.Add(message);
        return Task.CompletedTask;
    }
}
