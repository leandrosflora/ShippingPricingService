using ShippingPricingService.Domain;

namespace ShippingPricingService.Application.Ports;

public interface IPricingPolicyProvider
{
    Task<PricingPolicySnapshot?> GetPolicyAsync(PricingPolicyLookup lookup, CancellationToken cancellationToken);
}

public sealed record PricingPolicyLookup(
    Guid SellerId,
    Guid OriginNodeId,
    string CarrierCode,
    string ServiceLevelCode,
    long DestinationPostalCode,
    decimal ChargeableWeightKg,
    decimal CartTotal,
    string Currency,
    DateTimeOffset RequestedAt);
