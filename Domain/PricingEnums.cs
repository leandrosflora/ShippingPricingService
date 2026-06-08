namespace ShippingPricingService.Domain;

public enum PriceAdjustmentType
{
    BaseFreight = 1,
    WeightCharge = 2,
    FuelSurcharge = 3,
    RemoteAreaFee = 4,
    FragileFee = 5,
    OversizeFee = 6,
    CustomerDiscount = 7,
    PlatformSubsidy = 8,
    SellerSubsidy = 9
}

public enum RateCardStatus
{
    Draft = 1,
    Active = 2,
    Retired = 3
}
