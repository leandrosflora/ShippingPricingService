using Microsoft.EntityFrameworkCore;
using ShippingPricingService.Contracts;
using ShippingPricingService.Domain;
using ShippingPricingService.Infrastructure.Outbox;
using ShippingPricingService.Infrastructure.Persistence;

namespace ShippingPricingService.Application;

public sealed class RateCardApplicationService
{
    private readonly PricingDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;

    public RateCardApplicationService(PricingDbContext dbContext, IOutboxWriter outboxWriter)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
    }

    public async Task<RateCardResponse> CreateAsync(RateCardRequest request, CancellationToken cancellationToken)
    {
        var rateCard = RateCard.Create(request);
        _dbContext.RateCards.Add(rateCard);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(rateCard);
    }

    public async Task<RateCardResponse> UpdateAsync(Guid rateCardId, RateCardRequest request, CancellationToken cancellationToken)
    {
        var rateCard = await _dbContext.RateCards
            .Include(x => x.Bands)
            .SingleOrDefaultAsync(x => x.Id == rateCardId, cancellationToken)
            ?? throw new KeyNotFoundException("Rate card not found");

        rateCard.UpdateDraft(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(rateCard);
    }

    public async Task<RateCardResponse> ActivateAsync(Guid rateCardId, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var rateCard = await _dbContext.RateCards
            .Include(x => x.Bands)
            .SingleOrDefaultAsync(x => x.Id == rateCardId, cancellationToken)
            ?? throw new KeyNotFoundException("Rate card not found");

        var currentlyActive = await _dbContext.RateCards
            .Where(x => x.CarrierCode == rateCard.CarrierCode
                        && x.ServiceLevelCode == rateCard.ServiceLevelCode
                        && x.Currency == rateCard.Currency
                        && x.Status == RateCardStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var activeCard in currentlyActive)
            activeCard.Retire();

        rateCard.Activate();

        await _outboxWriter.AddAsync("PricingConfigurationChanged", new
        {
            RateCardId = rateCard.Id,
            rateCard.CarrierCode,
            rateCard.ServiceLevelCode,
            rateCard.Currency,
            rateCard.Version,
            OccurredAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(rateCard);
    }

    public async Task<RateCardResponse> RetireAsync(Guid rateCardId, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var rateCard = await _dbContext.RateCards
            .Include(x => x.Bands)
            .SingleOrDefaultAsync(x => x.Id == rateCardId, cancellationToken)
            ?? throw new KeyNotFoundException("Rate card not found");

        rateCard.Retire();

        await _outboxWriter.AddAsync("PricingConfigurationChanged", new
        {
            RateCardId = rateCard.Id,
            rateCard.CarrierCode,
            rateCard.ServiceLevelCode,
            rateCard.Currency,
            rateCard.Version,
            OccurredAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(rateCard);
    }

    private static RateCardResponse ToResponse(RateCard rateCard)
    {
        return new RateCardResponse(
            rateCard.Id,
            rateCard.Code,
            rateCard.CarrierCode,
            rateCard.ServiceLevelCode,
            rateCard.Currency,
            rateCard.Version,
            rateCard.Status.ToString(),
            rateCard.EffectiveFrom,
            rateCard.EffectiveUntil,
            rateCard.Bands.Select(x => new RateBandResponse(
                x.Id,
                x.OriginNodeId,
                x.DestinationZone,
                x.MinimumWeightKg,
                x.MaximumWeightKg,
                x.BasePrice,
                x.IncludedWeightKg,
                x.WeightIncrementKg,
                x.PricePerWeightIncrement,
                x.FuelSurchargePercentage,
                x.RemoteAreaFee,
                x.FragileFee,
                x.OversizeThresholdKg,
                x.OversizeFee,
                x.MinimumLogisticsCost)).ToList());
    }
}
