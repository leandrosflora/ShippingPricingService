using ShippingPricingService.Contracts;
using ShippingPricingService.Domain;

namespace ShippingPricingService.Application;

public sealed class ShippingPricingEngine
{
    public ShippingPriceCalculation Calculate(
        PackageProfileDto package,
        decimal cartTotal,
        DateTimeOffset requestedAt,
        PricingPolicySnapshot policy)
    {
        var chargeableWeight = ChargeableWeightCalculator.Calculate(package.ActualWeightKg, package.CubicWeightKg);

        if (chargeableWeight > policy.Rate.MaximumWeightKg)
            throw new InvalidOperationException("Package exceeds the maximum tariff weight");

        var adjustments = new List<PriceAdjustment>();
        var baseFreight = Round(policy.Rate.BasePrice);

        adjustments.Add(new PriceAdjustment("BASE_FREIGHT", "Base freight", PriceAdjustmentType.BaseFreight, baseFreight));

        var excessWeight = Math.Max(0, chargeableWeight - policy.Rate.IncludedWeightKg);
        var weightIncrements = excessWeight <= 0 ? 0 : Math.Ceiling(excessWeight / policy.Rate.WeightIncrementKg);
        var weightCharge = Round(weightIncrements * policy.Rate.PricePerWeightIncrement);

        if (weightCharge > 0)
            adjustments.Add(new PriceAdjustment("WEIGHT_CHARGE", "Additional chargeable weight", PriceAdjustmentType.WeightCharge, weightCharge));

        var subtotalBeforeSurcharges = baseFreight + weightCharge;
        var fuelSurcharge = Round(subtotalBeforeSurcharges * policy.Rate.FuelSurchargePercentage / 100m);

        if (fuelSurcharge > 0)
            adjustments.Add(new PriceAdjustment("FUEL_SURCHARGE", "Fuel surcharge", PriceAdjustmentType.FuelSurcharge, fuelSurcharge));

        var remoteAreaFee = policy.IsRemoteArea ? Round(policy.Rate.RemoteAreaFee) : 0;
        if (remoteAreaFee > 0)
            adjustments.Add(new PriceAdjustment("REMOTE_AREA", "Remote area fee", PriceAdjustmentType.RemoteAreaFee, remoteAreaFee));

        var fragileFee = package.IsFragile ? Round(policy.Rate.FragileFee) : 0;
        if (fragileFee > 0)
            adjustments.Add(new PriceAdjustment("FRAGILE", "Fragile item handling", PriceAdjustmentType.FragileFee, fragileFee));

        var oversizeFee = chargeableWeight > policy.Rate.OversizeThresholdKg ? Round(policy.Rate.OversizeFee) : 0;
        if (oversizeFee > 0)
            adjustments.Add(new PriceAdjustment("OVERSIZE", "Oversized package fee", PriceAdjustmentType.OversizeFee, oversizeFee));

        var logisticsCost = Round(baseFreight + weightCharge + fuelSurcharge + remoteAreaFee + fragileFee + oversizeFee);
        logisticsCost = Math.Max(logisticsCost, policy.Rate.MinimumLogisticsCost);

        var promotion = policy.Promotions
            .Where(x => x.IsApplicable(cartTotal, requestedAt))
            .OrderBy(x => x.Priority)
            .FirstOrDefault();

        var customerDiscount = 0m;
        var platformSubsidy = 0m;
        var sellerSubsidy = 0m;

        if (promotion is not null)
        {
            customerDiscount = CalculateBenefit(logisticsCost, promotion.CustomerDiscountPercentage, promotion.MaximumBenefit);
            var remainingAfterDiscount = logisticsCost - customerDiscount;
            platformSubsidy = CalculateBenefit(remainingAfterDiscount, promotion.PlatformSubsidyPercentage, promotion.MaximumBenefit - customerDiscount);
            var remainingAfterPlatform = remainingAfterDiscount - platformSubsidy;
            sellerSubsidy = CalculateBenefit(remainingAfterPlatform, promotion.SellerSubsidyPercentage, promotion.MaximumBenefit - customerDiscount - platformSubsidy);
        }

        AddNegativeAdjustment(adjustments, "CUSTOMER_DISCOUNT", "Shipping discount", PriceAdjustmentType.CustomerDiscount, customerDiscount);
        AddNegativeAdjustment(adjustments, "PLATFORM_SUBSIDY", "Platform subsidy", PriceAdjustmentType.PlatformSubsidy, platformSubsidy);
        AddNegativeAdjustment(adjustments, "SELLER_SUBSIDY", "Seller subsidy", PriceAdjustmentType.SellerSubsidy, sellerSubsidy);

        var customerPrice = Round(Math.Max(0, logisticsCost - customerDiscount - platformSubsidy - sellerSubsidy));

        return new ShippingPriceCalculation(chargeableWeight, logisticsCost, customerPrice, customerDiscount, platformSubsidy, sellerSubsidy, adjustments);
    }

    private static decimal CalculateBenefit(decimal baseAmount, decimal percentage, decimal remainingCap)
    {
        if (baseAmount <= 0 || percentage <= 0 || remainingCap <= 0)
            return 0;

        var benefit = Round(baseAmount * percentage / 100m);
        return Math.Min(benefit, remainingCap);
    }

    private static void AddNegativeAdjustment(ICollection<PriceAdjustment> adjustments, string code, string description, PriceAdjustmentType type, decimal amount)
    {
        if (amount <= 0)
            return;

        adjustments.Add(new PriceAdjustment(code, description, type, -amount));
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
