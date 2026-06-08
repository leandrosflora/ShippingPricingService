using ShippingPricingService.Contracts;

namespace ShippingPricingService.Domain;

public sealed class RateCard
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = default!;
    public string CarrierCode { get; private set; } = default!;
    public string ServiceLevelCode { get; private set; } = default!;
    public string Currency { get; private set; } = default!;
    public long Version { get; private set; }
    public RateCardStatus Status { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset EffectiveUntil { get; private set; }
    public List<RateBand> Bands { get; private set; } = [];

    private RateCard()
    {
    }

    public static RateCard Create(RateCardRequest request)
    {
        Validate(request);

        return new RateCard
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            CarrierCode = request.CarrierCode.Trim(),
            ServiceLevelCode = request.ServiceLevelCode.Trim(),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Version = 0,
            Status = RateCardStatus.Draft,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveUntil = request.EffectiveUntil,
            Bands = request.Bands.Select(RateBand.Create).ToList()
        };
    }

    public void UpdateDraft(RateCardRequest request)
    {
        if (Status != RateCardStatus.Draft)
            throw new InvalidOperationException("Only draft rate cards can be updated");

        Validate(request);

        Code = request.Code.Trim();
        CarrierCode = request.CarrierCode.Trim();
        ServiceLevelCode = request.ServiceLevelCode.Trim();
        Currency = request.Currency.Trim().ToUpperInvariant();
        EffectiveFrom = request.EffectiveFrom;
        EffectiveUntil = request.EffectiveUntil;
        Bands.Clear();
        Bands.AddRange(request.Bands.Select(RateBand.Create));
    }

    public void Activate()
    {
        if (Status != RateCardStatus.Draft)
            throw new InvalidOperationException("Only draft rate cards can be activated");

        Status = RateCardStatus.Active;
        Version++;
    }

    public void Retire()
    {
        if (Status == RateCardStatus.Retired)
            return;

        Status = RateCardStatus.Retired;
        Version++;
    }

    private static void Validate(RateCardRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            throw new ArgumentException("Code is required");
        if (string.IsNullOrWhiteSpace(request.CarrierCode))
            throw new ArgumentException("CarrierCode is required");
        if (string.IsNullOrWhiteSpace(request.ServiceLevelCode))
            throw new ArgumentException("ServiceLevelCode is required");
        if (request.Currency.Trim().Length != 3)
            throw new ArgumentException("Currency must be an ISO-4217 code");
        if (request.EffectiveUntil <= request.EffectiveFrom)
            throw new ArgumentException("EffectiveUntil must be greater than EffectiveFrom");
        if (request.Bands.Count == 0)
            throw new ArgumentException("At least one rate band is required");
    }
}
