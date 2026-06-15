using ShippingPricingService.Application;
using ShippingPricingService.Contracts;
using ShippingPricingService.Domain;
using Xunit;

namespace ShippingPricingService.Tests.Application;

public sealed class ShippingPricingEngineTests
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 6, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_applies_weight_surcharges_and_promotion_caps_without_external_dependencies()
    {
        var package = new PackageProfileDto(ActualWeightKg: 3.2m, CubicWeightKg: 4.1m, IsFragile: true, IsRestricted: false, Category: "electronics");
        var policy = CreatePolicy(isRemoteArea: true, promotions:
        [
            new PromotionRule(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "FREE-SHIPPING-CAP",
                Priority: 1,
                MinimumCartTotal: 100m,
                CustomerDiscountPercentage: 50m,
                PlatformSubsidyPercentage: 25m,
                SellerSubsidyPercentage: 10m,
                MaximumBenefit: 20m,
                StartsAt: RequestedAt.AddDays(-1),
                EndsAt: RequestedAt.AddDays(1))
        ]);

        var result = new ShippingPricingEngine().Calculate(package, cartTotal: 150m, RequestedAt, policy);

        Assert.Equal(4.1m, result.ChargeableWeightKg);
        Assert.Equal(59.50m, result.LogisticsCost);
        Assert.Equal(39.50m, result.CustomerPrice);
        Assert.Equal(20m, result.CustomerDiscount);
        Assert.Equal(0m, result.PlatformSubsidy);
        Assert.Equal(0m, result.SellerSubsidy);
        Assert.Contains(result.Adjustments, x => x.Code == "BASE_FREIGHT" && x.Amount == 30m);
        Assert.Contains(result.Adjustments, x => x.Code == "WEIGHT_CHARGE" && x.Amount == 15m);
        Assert.Contains(result.Adjustments, x => x.Code == "FUEL_SURCHARGE" && x.Amount == 4.50m);
        Assert.Contains(result.Adjustments, x => x.Code == "REMOTE_AREA" && x.Amount == 7m);
        Assert.Contains(result.Adjustments, x => x.Code == "FRAGILE" && x.Amount == 3m);
        Assert.Contains(result.Adjustments, x => x.Code == "CUSTOMER_DISCOUNT" && x.Amount == -20m);
    }

    [Fact]
    public void Calculate_rejects_package_above_contractual_rate_weight_limit()
    {
        var package = new PackageProfileDto(ActualWeightKg: 10.01m, CubicWeightKg: 0m, IsFragile: false, IsRestricted: false, Category: null);
        var policy = CreatePolicy(isRemoteArea: false, promotions: []);

        var exception = Assert.Throws<InvalidOperationException>(() => new ShippingPricingEngine().Calculate(package, 10m, RequestedAt, policy));

        Assert.Equal("Package exceeds the maximum tariff weight", exception.Message);
    }

    private static PricingPolicySnapshot CreatePolicy(bool isRemoteArea, IReadOnlyList<PromotionRule> promotions)
    {
        return new PricingPolicySnapshot(
            DestinationZone: "SP-CAPITAL",
            IsRemoteArea: isRemoteArea,
            Rate: new RateRule(
                RateCardId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                RateCardVersion: 7,
                Currency: "BRL",
                BasePrice: 30m,
                IncludedWeightKg: 1m,
                WeightIncrementKg: 1m,
                PricePerWeightIncrement: 5m,
                FuelSurchargePercentage: 10m,
                RemoteAreaFee: 7m,
                FragileFee: 3m,
                OversizeThresholdKg: 5m,
                OversizeFee: 9m,
                MinimumLogisticsCost: 20m,
                MaximumWeightKg: 10m),
            Promotions: promotions,
            EffectiveUntil: RequestedAt.AddHours(1));
    }
}
