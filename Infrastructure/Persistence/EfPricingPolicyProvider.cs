using Microsoft.EntityFrameworkCore;
using ShippingPricingService.Application.Ports;
using ShippingPricingService.Domain;

namespace ShippingPricingService.Infrastructure.Persistence;

public sealed class EfPricingPolicyProvider : IPricingPolicyProvider
{
    private readonly PricingDbContext _dbContext;

    public EfPricingPolicyProvider(PricingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PricingPolicySnapshot?> GetPolicyAsync(PricingPolicyLookup lookup, CancellationToken cancellationToken)
    {
        var zone = await _dbContext.PostalZones
            .AsNoTracking()
            .Where(x => lookup.DestinationPostalCode >= x.PostalCodeFrom && lookup.DestinationPostalCode <= x.PostalCodeTo)
            .OrderBy(x => x.Priority)
            .FirstOrDefaultAsync(cancellationToken);

        if (zone is null)
            return null;

        var rate = await (
            from card in _dbContext.RateCards.AsNoTracking()
            join band in _dbContext.RateBands.AsNoTracking() on card.Id equals band.RateCardId
            where card.Status == RateCardStatus.Active
            where card.CarrierCode == lookup.CarrierCode
            where card.ServiceLevelCode == lookup.ServiceLevelCode
            where card.Currency == lookup.Currency
            where lookup.RequestedAt >= card.EffectiveFrom
            where lookup.RequestedAt < card.EffectiveUntil
            where band.OriginNodeId == lookup.OriginNodeId
            where band.DestinationZone == zone.Code
            where lookup.ChargeableWeightKg >= band.MinimumWeightKg
            where lookup.ChargeableWeightKg <= band.MaximumWeightKg
            orderby card.Version descending
            select new { Card = card, Band = band })
            .FirstOrDefaultAsync(cancellationToken);

        if (rate is null)
            return null;

        var promotions = await _dbContext.PromotionRules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Where(x => x.SellerId == null || x.SellerId == lookup.SellerId)
            .Where(x => lookup.RequestedAt >= x.StartsAt && lookup.RequestedAt < x.EndsAt)
            .Where(x => lookup.CartTotal >= x.MinimumCartTotal)
            .OrderBy(x => x.Priority)
            .Select(x => new PromotionRule(
                x.Id,
                x.Code,
                x.Priority,
                x.MinimumCartTotal,
                x.CustomerDiscountPercentage,
                x.PlatformSubsidyPercentage,
                x.SellerSubsidyPercentage,
                x.MaximumBenefit,
                x.StartsAt,
                x.EndsAt))
            .ToListAsync(cancellationToken);

        return new PricingPolicySnapshot(
            zone.Code,
            zone.IsRemoteArea,
            new RateRule(
                rate.Card.Id,
                rate.Card.Version,
                rate.Card.Currency,
                rate.Band.BasePrice,
                rate.Band.IncludedWeightKg,
                rate.Band.WeightIncrementKg,
                rate.Band.PricePerWeightIncrement,
                rate.Band.FuelSurchargePercentage,
                rate.Band.RemoteAreaFee,
                rate.Band.FragileFee,
                rate.Band.OversizeThresholdKg,
                rate.Band.OversizeFee,
                rate.Band.MinimumLogisticsCost,
                rate.Band.MaximumWeightKg),
            promotions,
            rate.Card.EffectiveUntil);
    }
}
