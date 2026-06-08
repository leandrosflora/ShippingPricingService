using ShippingPricingService.Application;
using ShippingPricingService.Contracts;

namespace ShippingPricingService.Api;

public static class RateCardEndpoints
{
    public static IEndpointRouteBuilder MapRateCardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rate-cards")
            .WithTags("Rate Cards");

        group.MapPost("", async (
            RateCardRequest request,
            RateCardApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/rate-cards/{response.Id}", response);
        });

        group.MapPut("/{rateCardId:guid}", async (
            Guid rateCardId,
            RateCardRequest request,
            RateCardApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.UpdateAsync(rateCardId, request, cancellationToken);
            return Results.Ok(response);
        });

        group.MapPost("/{rateCardId:guid}/activate", async (
            Guid rateCardId,
            RateCardApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.ActivateAsync(rateCardId, cancellationToken);
            return Results.Ok(response);
        });

        group.MapPost("/{rateCardId:guid}/retire", async (
            Guid rateCardId,
            RateCardApplicationService service,
            CancellationToken cancellationToken) =>
        {
            var response = await service.RetireAsync(rateCardId, cancellationToken);
            return Results.Ok(response);
        });

        return app;
    }
}
