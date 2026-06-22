using ShippingPricingService.Application.Ports;
using ShippingPricingService.Contracts;

namespace ShippingPricingService.Application;

public sealed class ShippingPricingApplicationService
{
    private static readonly TimeSpan CalculationCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan QuoteTtl = TimeSpan.FromMinutes(15);

    private readonly IPricingPolicyProvider _policyProvider;
    private readonly IShippingPriceCache _cache;
    private readonly ShippingPricingEngine _engine;
    private readonly ILogger<ShippingPricingApplicationService> _logger;

    public ShippingPricingApplicationService(
        IPricingPolicyProvider policyProvider,
        IShippingPriceCache cache,
        ShippingPricingEngine engine,
        ILogger<ShippingPricingApplicationService> logger)
    {
        _policyProvider = policyProvider;
        _cache = cache;
        _engine = engine;
        _logger = logger;
    }

    public async Task<BatchShippingPriceResponse> QuoteBatchAsync(BatchShippingPriceRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        var requestedAt = request.RequestedAtUtc ?? DateTimeOffset.UtcNow;
        var tasks = request.Candidates.Select(candidate => QuoteCandidateAsync(request, candidate, requestedAt, cancellationToken));
        var quotes = await Task.WhenAll(tasks);

        return new BatchShippingPriceResponse(quotes);
    }

    public async Task<FreightPricingResponse> CalculateFreightAsync(FreightPricingRequest request, CancellationToken cancellationToken)
    {
        var batchRequest = new BatchShippingPriceRequest(
            request.BuyerId,
            request.SellerId,
            request.DestinationPostalCode,
            request.CartTotal,
            request.Currency,
            request.RequestedAtUtc,
            [
                new ShippingPriceCandidateRequest(
                    CandidateId: request.RouteId,
                    RouteId: request.RouteId,
                    OriginNodeId: request.OriginNodeId,
                    CarrierCode: request.CarrierCode,
                    ServiceLevelCode: request.ServiceLevelCode,
                    Package: request.Package)
            ]);

        var response = await QuoteBatchAsync(batchRequest, cancellationToken);
        var quote = response.Quotes.Single();
        var subsidyAmount = SumNullable(quote.PlatformSubsidy, quote.SellerSubsidy, quote.Discount);

        return new FreightPricingResponse(
            quote.Available,
            quote.QuoteId,
            request.RouteId,
            request.CarrierCode,
            request.ServiceLevelCode,
            quote.Currency,
            quote.ChargeableWeightKg,
            quote.LogisticsCost,
            subsidyAmount,
            quote.CustomerPrice,
            quote.Adjustments,
            quote.RateCardVersion,
            quote.Source,
            quote.ExpiresAt,
            quote.UnavailableReason);
    }

    public Task<ShippingPriceQuoteResponse?> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        return _cache.GetQuoteAsync(quoteId, cancellationToken);
    }

    private async Task<ShippingPriceQuoteResponse> QuoteCandidateAsync(
        BatchShippingPriceRequest request,
        ShippingPriceCandidateRequest candidate,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var chargeableWeight = ChargeableWeightCalculator.Calculate(candidate.Package.ActualWeightKg, candidate.Package.CubicWeightKg);
            var postalCode = NormalizePostalCode(request.DestinationPostalCode);

            var policy = await _policyProvider.GetPolicyAsync(new PricingPolicyLookup(
                request.SellerId,
                candidate.OriginNodeId,
                candidate.CarrierCode,
                candidate.ServiceLevelCode,
                postalCode,
                chargeableWeight,
                request.CartTotal,
                request.Currency,
                requestedAt), cancellationToken);

            if (policy is null)
                return Unavailable(candidate, request.Currency, "No active rate card found");

            var cacheKey = PricingCacheKeyFactory.Build(request, candidate, policy);
            var cached = await _cache.GetCalculationAsync(cacheKey, cancellationToken);

            if (cached is not null)
            {
                return cached with
                {
                    CandidateId = candidate.CandidateId,
                    RouteId = candidate.RouteId,
                    Source = "Cache"
                };
            }

            var calculation = _engine.Calculate(candidate.Package, request.CartTotal, requestedAt, policy);
            var quoteId = Guid.NewGuid();
            var expiresAt = new[] { requestedAt.Add(QuoteTtl), policy.EffectiveUntil }.Min();

            var response = new ShippingPriceQuoteResponse(
                candidate.CandidateId,
                true,
                quoteId,
                candidate.RouteId,
                policy.Rate.Currency,
                calculation.ChargeableWeightKg,
                calculation.LogisticsCost,
                calculation.CustomerPrice,
                calculation.PlatformSubsidy,
                calculation.SellerSubsidy,
                calculation.CustomerDiscount,
                calculation.Adjustments.Select(x => new PriceAdjustmentResponse(x.Code, x.Description, x.Type.ToString(), x.Amount)).ToList(),
                policy.Rate.RateCardVersion,
                "Calculated",
                expiresAt,
                null);

            var quoteTtl = expiresAt - DateTimeOffset.UtcNow;
            if (quoteTtl > TimeSpan.Zero)
                await _cache.SetQuoteAsync(quoteId, response, quoteTtl, cancellationToken);

            await _cache.SetCalculationAsync(cacheKey, response, CalculationCacheTtl, cancellationToken);
            return response;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Pricing failed for candidate {CandidateId}", candidate.CandidateId);
            return Unavailable(candidate, request.Currency, exception.Message);
        }
    }

    private static ShippingPriceQuoteResponse Unavailable(ShippingPriceCandidateRequest candidate, string currency, string reason)
    {
        return new ShippingPriceQuoteResponse(candidate.CandidateId, false, null, candidate.RouteId, currency, null, null, null, null, null, null, [], null, "Calculated", null, reason);
    }

    private static decimal? SumNullable(params decimal?[] values)
    {
        return values.All(value => value is null) ? null : values.Sum(value => value ?? 0);
    }

    private static long NormalizePostalCode(string postalCode)
    {
        var digits = new string(postalCode.Where(char.IsDigit).ToArray());

        if (digits.Length != 8 || !long.TryParse(digits, out var result))
            throw new ArgumentException("Destination postal code is invalid");

        return result;
    }

    private static void Validate(BatchShippingPriceRequest request)
    {
        if (request.SellerId == Guid.Empty)
            throw new ArgumentException("SellerId is required");
        if (request.CartTotal < 0)
            throw new ArgumentException("CartTotal cannot be negative");
        if (string.IsNullOrWhiteSpace(request.Currency))
            throw new ArgumentException("Currency is required");
        if (request.Candidates.Count == 0)
            throw new ArgumentException("At least one route candidate is required");
        if (request.Candidates.Count > 20)
            throw new ArgumentException("A maximum of 20 candidates is allowed");
        if (request.Candidates.GroupBy(x => x.CandidateId).Any(x => x.Count() > 1))
            throw new ArgumentException("CandidateId must be unique");
    }
}
