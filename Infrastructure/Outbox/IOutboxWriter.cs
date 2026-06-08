namespace ShippingPricingService.Infrastructure.Outbox;

public interface IOutboxWriter
{
    Task AddAsync(string eventType, object payload, CancellationToken cancellationToken);
}
