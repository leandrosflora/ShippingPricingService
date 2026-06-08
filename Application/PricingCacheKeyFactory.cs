using System.Security.Cryptography;
using System.Text;
using ShippingPricingService.Contracts;
using ShippingPricingService.Domain;

namespace ShippingPricingService.Application;

public static class PricingCacheKeyFactory
{
    public static string Build(BatchShippingPriceRequest request, ShippingPriceCandidateRequest candidate, PricingPolicySnapshot policy)
    {
        var promotionVersions = string.Join(",", policy.Promotions
            .OrderBy(x => x.PromotionId)
            .Select(x => x.PromotionId));

        var raw = string.Join(":",
            request.BuyerId,
            request.SellerId,
            NormalizePostalCode(request.DestinationPostalCode),
            request.CartTotal,
            request.Currency,
            candidate.RouteId,
            candidate.OriginNodeId,
            candidate.CarrierCode,
            candidate.ServiceLevelCode,
            candidate.Package.ActualWeightKg,
            candidate.Package.CubicWeightKg,
            candidate.Package.IsFragile,
            candidate.Package.IsRestricted,
            candidate.Package.Category,
            policy.Rate.RateCardVersion,
            promotionVersions);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"calculation:{Convert.ToHexString(hash)}";
    }

    private static string NormalizePostalCode(string postalCode)
    {
        return new string(postalCode.Where(char.IsDigit).ToArray());
    }
}
