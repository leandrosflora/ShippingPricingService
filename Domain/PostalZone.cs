namespace ShippingPricingService.Domain;

public sealed class PostalZone
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public long PostalCodeFrom { get; private set; }
    public long PostalCodeTo { get; private set; }
    public bool IsRemoteArea { get; private set; }
    public int Priority { get; private set; }

    private PostalZone()
    {
    }
}
