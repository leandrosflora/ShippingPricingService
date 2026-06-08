namespace ShippingPricingService.Domain;

public sealed record PriceAdjustment(
    string Code,
    string Description,
    PriceAdjustmentType Type,
    decimal Amount);
