using Microsoft.Extensions.Logging.Abstractions;
using ShippingPricingService.Application;
using ShippingPricingService.Application.Ports;
using ShippingPricingService.Contracts;
using ShippingPricingService.Domain;
using Xunit;

namespace ShippingPricingService.Tests.Application;

public sealed class ShippingPricingApplicationServiceTests
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 6, 14, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task QuoteBatchAsync_uses_policy_lookup_and_stores_quote_without_http_database_or_kafka()
    {
        var policy = CreatePolicy();
        var policyProvider = new FakePricingPolicyProvider(policy);
        var cache = new InMemoryShippingPriceCache();
        var service = CreateService(policyProvider, cache);
        var request = CreateRequest(destinationPostalCode: "01001-000");

        var response = await service.QuoteBatchAsync(request, CancellationToken.None);

        var quote = Assert.Single(response.Quotes);
        Assert.True(quote.Available);
        Assert.NotNull(quote.QuoteId);
        Assert.Equal("route-1", quote.RouteId);
        Assert.Equal("BRL", quote.Currency);
        Assert.Equal(2.5m, quote.ChargeableWeightKg);
        Assert.Equal(17.50m, quote.LogisticsCost);
        Assert.Equal(17.50m, quote.CustomerPrice);
        Assert.Equal(3, quote.RateCardVersion);
        Assert.Equal("Calculated", quote.Source);
        Assert.Null(quote.UnavailableReason);
        Assert.NotNull(cache.StoredQuote);
        Assert.NotNull(cache.StoredCalculation);
        Assert.Equal(1001000, policyProvider.LastLookup?.DestinationPostalCode);
        Assert.Equal(2.5m, policyProvider.LastLookup?.ChargeableWeightKg);
        Assert.Equal(RequestedAt, policyProvider.LastLookup?.RequestedAt);
    }

    [Fact]
    public async Task QuoteBatchAsync_returns_unavailable_when_no_rate_card_policy_exists()
    {
        var service = CreateService(new FakePricingPolicyProvider(null), new InMemoryShippingPriceCache());

        var response = await service.QuoteBatchAsync(CreateRequest(destinationPostalCode: "01001000"), CancellationToken.None);

        var quote = Assert.Single(response.Quotes);
        Assert.False(quote.Available);
        Assert.Null(quote.QuoteId);
        Assert.Equal("BRL", quote.Currency);
        Assert.Equal("No active rate card found", quote.UnavailableReason);
        Assert.Empty(quote.Adjustments);
    }

    [Theory]
    [InlineData("123", "Destination postal code is invalid")]
    [InlineData("abcdefgh", "Destination postal code is invalid")]
    public async Task QuoteBatchAsync_marks_candidate_unavailable_when_postal_code_violates_contract(string postalCode, string expectedReason)
    {
        var service = CreateService(new FakePricingPolicyProvider(CreatePolicy()), new InMemoryShippingPriceCache());

        var response = await service.QuoteBatchAsync(CreateRequest(destinationPostalCode: postalCode), CancellationToken.None);

        var quote = Assert.Single(response.Quotes);
        Assert.False(quote.Available);
        Assert.Equal(expectedReason, quote.UnavailableReason);
    }

    [Fact]
    public async Task QuoteBatchAsync_validates_batch_request_before_downstream_ports_are_called()
    {
        var service = CreateService(new FakePricingPolicyProvider(CreatePolicy()), new InMemoryShippingPriceCache());
        var request = CreateRequest(destinationPostalCode: "01001000") with { SellerId = Guid.Empty };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.QuoteBatchAsync(request, CancellationToken.None));

        Assert.Equal("SellerId is required", exception.Message);
    }

    [Fact]
    public async Task QuoteBatchAsync_returns_cached_calculation_for_same_pricing_key()
    {
        var policy = CreatePolicy();
        var cache = new InMemoryShippingPriceCache();
        var cachedQuote = new ShippingPriceQuoteResponse(
            CandidateId: "previous-candidate",
            Available: true,
            QuoteId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            RouteId: "previous-route",
            Currency: "BRL",
            ChargeableWeightKg: 2.5m,
            LogisticsCost: 17.50m,
            CustomerPrice: 17.50m,
            PlatformSubsidy: 0m,
            SellerSubsidy: 0m,
            Discount: 0m,
            Adjustments: [],
            RateCardVersion: 3,
            Source: "Calculated",
            ExpiresAt: RequestedAt.AddMinutes(15),
            UnavailableReason: null);
        cache.Calculations[PricingCacheKeyFactory.Build(CreateRequest("01001000"), CreateRequest("01001000").Candidates[0], policy)] = cachedQuote;
        var service = CreateService(new FakePricingPolicyProvider(policy), cache);

        var response = await service.QuoteBatchAsync(CreateRequest("01001000"), CancellationToken.None);

        var quote = Assert.Single(response.Quotes);
        Assert.Equal("candidate-1", quote.CandidateId);
        Assert.Equal("route-1", quote.RouteId);
        Assert.Equal("Cache", quote.Source);
        Assert.Equal(cachedQuote.QuoteId, quote.QuoteId);
    }

    [Fact]
    public async Task CalculateFreightAsync_returns_documented_freight_cost_fields()
    {
        var service = CreateService(new FakePricingPolicyProvider(CreatePolicy()), new InMemoryShippingPriceCache());
        var batchRequest = CreateRequest(destinationPostalCode: "01001-000");
        var candidate = batchRequest.Candidates.Single();
        var request = new FreightPricingRequest(
            batchRequest.BuyerId,
            batchRequest.SellerId,
            batchRequest.DestinationPostalCode,
            batchRequest.CartTotal,
            batchRequest.Currency,
            batchRequest.RequestedAtUtc,
            candidate.RouteId,
            candidate.OriginNodeId,
            candidate.CarrierCode,
            candidate.ServiceLevelCode,
            candidate.Package);

        var response = await service.CalculateFreightAsync(request, CancellationToken.None);

        Assert.True(response.Available);
        Assert.Equal("route-1", response.RouteId);
        Assert.Equal("MELI", response.CarrierCode);
        Assert.Equal("standard", response.ServiceLevelCode);
        Assert.Equal(17.50m, response.GrossCost);
        Assert.Equal(0m, response.SubsidyAmount);
        Assert.Equal(17.50m, response.BuyerCost);
        Assert.Null(response.UnavailableReason);
    }

    private static ShippingPricingApplicationService CreateService(IPricingPolicyProvider policyProvider, IShippingPriceCache cache)
    {
        return new ShippingPricingApplicationService(
            policyProvider,
            cache,
            new ShippingPricingEngine(),
            NullLogger<ShippingPricingApplicationService>.Instance);
    }

    private static BatchShippingPriceRequest CreateRequest(string destinationPostalCode)
    {
        return new BatchShippingPriceRequest(
            BuyerId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            SellerId: Guid.Parse("55555555-5555-5555-5555-555555555555"),
            DestinationPostalCode: destinationPostalCode,
            CartTotal: 120m,
            Currency: "BRL",
            RequestedAtUtc: RequestedAt,
            Candidates:
            [
                new ShippingPriceCandidateRequest(
                    CandidateId: "candidate-1",
                    RouteId: "route-1",
                    OriginNodeId: Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    CarrierCode: "MELI",
                    ServiceLevelCode: "standard",
                    Package: new PackageProfileDto(ActualWeightKg: 2.5m, CubicWeightKg: 1.1m, IsFragile: false, IsRestricted: false, Category: "books"))
            ]);
    }

    private static PricingPolicySnapshot CreatePolicy()
    {
        return new PricingPolicySnapshot(
            DestinationZone: "SP-CAPITAL",
            IsRemoteArea: false,
            Rate: new RateRule(
                Guid.Parse("77777777-7777-7777-7777-777777777777"),
                RateCardVersion: 3,
                Currency: "BRL",
                BasePrice: 12.50m,
                IncludedWeightKg: 1m,
                WeightIncrementKg: 1m,
                PricePerWeightIncrement: 2.50m,
                FuelSurchargePercentage: 0m,
                RemoteAreaFee: 0m,
                FragileFee: 0m,
                OversizeThresholdKg: 10m,
                OversizeFee: 0m,
                MinimumLogisticsCost: 0m,
                MaximumWeightKg: 30m),
            Promotions: [],
            EffectiveUntil: RequestedAt.AddHours(2));
    }

    private sealed class FakePricingPolicyProvider : IPricingPolicyProvider
    {
        private readonly PricingPolicySnapshot? _policy;

        public FakePricingPolicyProvider(PricingPolicySnapshot? policy)
        {
            _policy = policy;
        }

        public PricingPolicyLookup? LastLookup { get; private set; }

        public Task<PricingPolicySnapshot?> GetPolicyAsync(PricingPolicyLookup lookup, CancellationToken cancellationToken)
        {
            LastLookup = lookup;
            return Task.FromResult(_policy);
        }
    }

    private sealed class InMemoryShippingPriceCache : IShippingPriceCache
    {
        public Dictionary<string, ShippingPriceQuoteResponse> Calculations { get; } = [];
        public ShippingPriceQuoteResponse? StoredQuote { get; private set; }
        public ShippingPriceQuoteResponse? StoredCalculation { get; private set; }

        public Task<ShippingPriceQuoteResponse?> GetCalculationAsync(string cacheKey, CancellationToken cancellationToken)
        {
            Calculations.TryGetValue(cacheKey, out var response);
            return Task.FromResult(response);
        }

        public Task SetCalculationAsync(string cacheKey, ShippingPriceQuoteResponse response, TimeSpan ttl, CancellationToken cancellationToken)
        {
            Calculations[cacheKey] = response;
            StoredCalculation = response;
            return Task.CompletedTask;
        }

        public Task<ShippingPriceQuoteResponse?> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ShippingPriceQuoteResponse?>(StoredQuote?.QuoteId == quoteId ? StoredQuote : null);
        }

        public Task SetQuoteAsync(Guid quoteId, ShippingPriceQuoteResponse response, TimeSpan ttl, CancellationToken cancellationToken)
        {
            StoredQuote = response;
            return Task.CompletedTask;
        }
    }
}
