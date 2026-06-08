using ShippingPricingService.Contracts;

namespace ShippingPricingService.Domain;

public sealed class RateBand
{
    public Guid Id { get; private set; }
    public Guid RateCardId { get; private set; }
    public Guid OriginNodeId { get; private set; }
    public string DestinationZone { get; private set; } = default!;
    public decimal MinimumWeightKg { get; private set; }
    public decimal MaximumWeightKg { get; private set; }
    public decimal BasePrice { get; private set; }
    public decimal IncludedWeightKg { get; private set; }
    public decimal WeightIncrementKg { get; private set; }
    public decimal PricePerWeightIncrement { get; private set; }
    public decimal FuelSurchargePercentage { get; private set; }
    public decimal RemoteAreaFee { get; private set; }
    public decimal FragileFee { get; private set; }
    public decimal OversizeThresholdKg { get; private set; }
    public decimal OversizeFee { get; private set; }
    public decimal MinimumLogisticsCost { get; private set; }

    private RateBand()
    {
    }

    public static RateBand Create(RateBandRequest request)
    {
        if (request.OriginNodeId == Guid.Empty)
            throw new ArgumentException("OriginNodeId is required");
        if (string.IsNullOrWhiteSpace(request.DestinationZone))
            throw new ArgumentException("DestinationZone is required");
        if (request.MinimumWeightKg < 0 || request.MaximumWeightKg <= request.MinimumWeightKg)
            throw new ArgumentException("Invalid weight range");
        if (request.WeightIncrementKg <= 0)
            throw new ArgumentException("WeightIncrementKg must be greater than zero");

        return new RateBand
        {
            Id = Guid.NewGuid(),
            OriginNodeId = request.OriginNodeId,
            DestinationZone = request.DestinationZone.Trim(),
            MinimumWeightKg = request.MinimumWeightKg,
            MaximumWeightKg = request.MaximumWeightKg,
            BasePrice = request.BasePrice,
            IncludedWeightKg = request.IncludedWeightKg,
            WeightIncrementKg = request.WeightIncrementKg,
            PricePerWeightIncrement = request.PricePerWeightIncrement,
            FuelSurchargePercentage = request.FuelSurchargePercentage,
            RemoteAreaFee = request.RemoteAreaFee,
            FragileFee = request.FragileFee,
            OversizeThresholdKg = request.OversizeThresholdKg,
            OversizeFee = request.OversizeFee,
            MinimumLogisticsCost = request.MinimumLogisticsCost
        };
    }
}
