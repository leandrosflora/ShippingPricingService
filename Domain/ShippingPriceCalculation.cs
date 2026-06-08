namespace ShippingPricingService.Domain;

public sealed record ShippingPriceCalculation(
    decimal ChargeableWeightKg,
    decimal LogisticsCost,
    decimal CustomerPrice,
    decimal CustomerDiscount,
    decimal PlatformSubsidy,
    decimal SellerSubsidy,
    IReadOnlyList<PriceAdjustment> Adjustments);
