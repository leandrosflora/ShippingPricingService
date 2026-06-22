namespace ShippingPricingService.Contracts;

public sealed record BatchShippingPriceRequest(
    Guid BuyerId,
    Guid SellerId,
    string DestinationPostalCode,
    decimal CartTotal,
    string Currency,
    DateTimeOffset? RequestedAtUtc,
    IReadOnlyList<ShippingPriceCandidateRequest> Candidates);

public sealed record FreightPricingRequest(
    Guid BuyerId,
    Guid SellerId,
    string DestinationPostalCode,
    decimal CartTotal,
    string Currency,
    DateTimeOffset? RequestedAtUtc,
    string RouteId,
    Guid OriginNodeId,
    string CarrierCode,
    string ServiceLevelCode,
    PackageProfileDto Package);

public sealed record ShippingPriceCandidateRequest(
    string CandidateId,
    string RouteId,
    Guid OriginNodeId,
    string CarrierCode,
    string ServiceLevelCode,
    PackageProfileDto Package);

public sealed record PackageProfileDto(
    decimal ActualWeightKg,
    decimal CubicWeightKg,
    bool IsFragile,
    bool IsRestricted,
    string? Category);

public sealed record BatchShippingPriceResponse(
    IReadOnlyList<ShippingPriceQuoteResponse> Quotes);

public sealed record FreightPricingResponse(
    bool Available,
    Guid? QuoteId,
    string RouteId,
    string CarrierCode,
    string ServiceLevelCode,
    string Currency,
    decimal? ChargeableWeightKg,
    decimal? GrossCost,
    decimal? SubsidyAmount,
    decimal? BuyerCost,
    IReadOnlyList<PriceAdjustmentResponse> Adjustments,
    long? RateCardVersion,
    string Source,
    DateTimeOffset? ExpiresAt,
    string? UnavailableReason);

public sealed record ShippingPriceQuoteResponse(
    string CandidateId,
    bool Available,
    Guid? QuoteId,
    string? RouteId,
    string Currency,
    decimal? ChargeableWeightKg,
    decimal? LogisticsCost,
    decimal? CustomerPrice,
    decimal? PlatformSubsidy,
    decimal? SellerSubsidy,
    decimal? Discount,
    IReadOnlyList<PriceAdjustmentResponse> Adjustments,
    long? RateCardVersion,
    string Source,
    DateTimeOffset? ExpiresAt,
    string? UnavailableReason);

public sealed record PriceAdjustmentResponse(
    string Code,
    string Description,
    string Type,
    decimal Amount);
