using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ShippingPricingService.Application.Ports;
using ShippingPricingService.Contracts;

namespace ShippingPricingService.Infrastructure.Cache;

public sealed class RedisShippingPriceCache : IShippingPriceCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache;

    public RedisShippingPriceCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public Task<ShippingPriceQuoteResponse?> GetCalculationAsync(string cacheKey, CancellationToken cancellationToken)
    {
        return GetAsync(cacheKey, cancellationToken);
    }

    public Task SetCalculationAsync(string cacheKey, ShippingPriceQuoteResponse response, TimeSpan ttl, CancellationToken cancellationToken)
    {
        return SetAsync(cacheKey, response, ttl, cancellationToken);
    }

    public Task<ShippingPriceQuoteResponse?> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        return GetAsync(BuildQuoteKey(quoteId), cancellationToken);
    }

    public Task SetQuoteAsync(Guid quoteId, ShippingPriceQuoteResponse response, TimeSpan ttl, CancellationToken cancellationToken)
    {
        return SetAsync(BuildQuoteKey(quoteId), response, ttl, cancellationToken);
    }

    private async Task<ShippingPriceQuoteResponse?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var json = await _cache.GetStringAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ShippingPriceQuoteResponse>(json, JsonOptions);
    }

    private Task SetAsync(string key, ShippingPriceQuoteResponse response, TimeSpan ttl, CancellationToken cancellationToken)
    {
        return _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(response, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            cancellationToken);
    }

    private static string BuildQuoteKey(Guid quoteId)
    {
        return $"quote:{quoteId:N}";
    }
}
