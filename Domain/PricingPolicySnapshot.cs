namespace ShippingPricingService.Domain;

public sealed record PricingPolicySnapshot(
    string DestinationZone,
    bool IsRemoteArea,
    RateRule Rate,
    IReadOnlyList<PromotionRule> Promotions,
    DateTimeOffset EffectiveUntil);
