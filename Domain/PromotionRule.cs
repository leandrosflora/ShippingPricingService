namespace ShippingPricingService.Domain;

public sealed record PromotionRule(
    Guid PromotionId,
    string Code,
    int Priority,
    decimal MinimumCartTotal,
    decimal CustomerDiscountPercentage,
    decimal PlatformSubsidyPercentage,
    decimal SellerSubsidyPercentage,
    decimal MaximumBenefit,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt)
{
    public bool IsApplicable(decimal cartTotal, DateTimeOffset requestedAt)
    {
        return cartTotal >= MinimumCartTotal
               && requestedAt >= StartsAt
               && requestedAt < EndsAt;
    }
}
