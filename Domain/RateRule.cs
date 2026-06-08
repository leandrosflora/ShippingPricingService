namespace ShippingPricingService.Domain;

public sealed record RateRule(
    Guid RateCardId,
    long RateCardVersion,
    string Currency,
    decimal BasePrice,
    decimal IncludedWeightKg,
    decimal WeightIncrementKg,
    decimal PricePerWeightIncrement,
    decimal FuelSurchargePercentage,
    decimal RemoteAreaFee,
    decimal FragileFee,
    decimal OversizeThresholdKg,
    decimal OversizeFee,
    decimal MinimumLogisticsCost,
    decimal MaximumWeightKg);
