using ShippingPricingService.Application;
using ShippingPricingService.Contracts;

namespace ShippingPricingService.Api;

public static class ShippingPricingEndpoints
{
    public static IEndpointRouteBuilder MapShippingPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/shipping-prices")
            .WithTags("Shipping Pricing");

        group.MapPost("/quotes/batch", async (
            BatchShippingPriceRequest request,
            ShippingPricingApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.QuoteBatchAsync(request, cancellationToken);
            return Results.Ok(response);
        });

        group.MapGet("/quotes/{quoteId:guid}", async (
            Guid quoteId,
            ShippingPricingApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.GetQuoteAsync(quoteId, cancellationToken);
            return response is null ? Results.NotFound() : Results.Ok(response);
        });

        return app;
    }
}
