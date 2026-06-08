namespace ShippingPricingService.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(Guid id, string eventType, string payload, DateTimeOffset occurredAt)
    {
        Id = id;
        EventType = eventType;
        Payload = payload;
        OccurredAt = occurredAt;
    }
}
