namespace ShippingPricingService.Domain;

public sealed class PromotionRuleEntity
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public Guid? SellerId { get; private set; }
    public int Priority { get; private set; }
    public decimal MinimumCartTotal { get; private set; }
    public decimal CustomerDiscountPercentage { get; private set; }
    public decimal PlatformSubsidyPercentage { get; private set; }
    public decimal SellerSubsidyPercentage { get; private set; }
    public decimal MaximumBenefit { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset EndsAt { get; private set; }
    public bool IsActive { get; private set; }

    private PromotionRuleEntity()
    {
    }
}
