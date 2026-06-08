namespace ShippingPricingService.Contracts;

public sealed record RateCardRequest(
    string Code,
    string CarrierCode,
    string ServiceLevelCode,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveUntil,
    IReadOnlyList<RateBandRequest> Bands);

public sealed record RateBandRequest(
    Guid OriginNodeId,
    string DestinationZone,
    decimal MinimumWeightKg,
    decimal MaximumWeightKg,
    decimal BasePrice,
    decimal IncludedWeightKg,
    decimal WeightIncrementKg,
    decimal PricePerWeightIncrement,
    decimal FuelSurchargePercentage,
    decimal RemoteAreaFee,
    decimal FragileFee,
    decimal OversizeThresholdKg,
    decimal OversizeFee,
    decimal MinimumLogisticsCost);

public sealed record RateCardResponse(
    Guid Id,
    string Code,
    string CarrierCode,
    string ServiceLevelCode,
    string Currency,
    long Version,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveUntil,
    IReadOnlyList<RateBandResponse> Bands);

public sealed record RateBandResponse(
    Guid Id,
    Guid OriginNodeId,
    string DestinationZone,
    decimal MinimumWeightKg,
    decimal MaximumWeightKg,
    decimal BasePrice,
    decimal IncludedWeightKg,
    decimal WeightIncrementKg,
    decimal PricePerWeightIncrement,
    decimal FuelSurchargePercentage,
    decimal RemoteAreaFee,
    decimal FragileFee,
    decimal OversizeThresholdKg,
    decimal OversizeFee,
    decimal MinimumLogisticsCost);
