using ShippingPricingService.Contracts;

namespace ShippingPricingService.Application.Ports;

public interface IShippingPriceCache
{
    Task<ShippingPriceQuoteResponse?> GetCalculationAsync(string cacheKey, CancellationToken cancellationToken);
    Task SetCalculationAsync(string cacheKey, ShippingPriceQuoteResponse response, TimeSpan ttl, CancellationToken cancellationToken);
    Task<ShippingPriceQuoteResponse?> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken);
    Task SetQuoteAsync(Guid quoteId, ShippingPriceQuoteResponse response, TimeSpan ttl, CancellationToken cancellationToken);
}
