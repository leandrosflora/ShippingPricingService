using ShippingPricingService.Contracts;
using ShippingPricingService.Domain;
using Xunit;

namespace ShippingPricingService.Tests.Domain;

public sealed class RateCardTests
{
    [Fact]
    public void Create_normalizes_currency_and_starts_as_draft_version_zero()
    {
        var rateCard = RateCard.Create(CreateRequest(currency: "brl"));

        Assert.Equal("BRL", rateCard.Currency);
        Assert.Equal(RateCardStatus.Draft, rateCard.Status);
        Assert.Equal(0, rateCard.Version);
        Assert.Single(rateCard.Bands);
    }

    [Fact]
    public void Activate_transitions_draft_to_active_and_increments_version()
    {
        var rateCard = RateCard.Create(CreateRequest(currency: "BRL"));

        rateCard.Activate();

        Assert.Equal(RateCardStatus.Active, rateCard.Status);
        Assert.Equal(1, rateCard.Version);
    }

    [Fact]
    public void UpdateDraft_rejects_non_draft_rate_cards()
    {
        var rateCard = RateCard.Create(CreateRequest(currency: "BRL"));
        rateCard.Activate();

        var exception = Assert.Throws<InvalidOperationException>(() => rateCard.UpdateDraft(CreateRequest(currency: "BRL")));

        Assert.Equal("Only draft rate cards can be updated", exception.Message);
    }

    [Theory]
    [InlineData("", "Code is required")]
    [InlineData(" ", "Code is required")]
    public void Create_validates_required_code(string code, string expectedMessage)
    {
        var exception = Assert.Throws<ArgumentException>(() => RateCard.Create(CreateRequest(currency: "BRL") with { Code = code }));

        Assert.Equal(expectedMessage, exception.Message);
    }

    private static RateCardRequest CreateRequest(string currency)
    {
        return new RateCardRequest(
            Code: "standard-sao-paulo",
            CarrierCode: "MELI",
            ServiceLevelCode: "standard",
            Currency: currency,
            EffectiveFrom: new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero),
            EffectiveUntil: new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
            Bands:
            [
                new RateBandRequest(
                    OriginNodeId: Guid.Parse("88888888-8888-8888-8888-888888888888"),
                    DestinationZone: "SP-CAPITAL",
                    MinimumWeightKg: 0m,
                    MaximumWeightKg: 30m,
                    BasePrice: 12.50m,
                    IncludedWeightKg: 1m,
                    WeightIncrementKg: 1m,
                    PricePerWeightIncrement: 2.50m,
                    FuelSurchargePercentage: 0m,
                    RemoteAreaFee: 0m,
                    FragileFee: 0m,
                    OversizeThresholdKg: 10m,
                    OversizeFee: 0m,
                    MinimumLogisticsCost: 0m)
            ]);
    }
}
