namespace ShippingPricingService.Application;

public static class ChargeableWeightCalculator
{
    public static decimal Calculate(decimal actualWeightKg, decimal cubicWeightKg)
    {
        if (actualWeightKg <= 0)
            throw new ArgumentException("Actual weight must be greater than zero");

        if (cubicWeightKg < 0)
            throw new ArgumentException("Cubic weight cannot be negative");

        return Math.Max(actualWeightKg, cubicWeightKg);
    }
}
