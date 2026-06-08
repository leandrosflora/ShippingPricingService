using Microsoft.EntityFrameworkCore;
using ShippingPricingService.Domain;
using ShippingPricingService.Infrastructure.Outbox;

namespace ShippingPricingService.Infrastructure.Persistence;

public sealed class PricingDbContext : DbContext
{
    public PricingDbContext(DbContextOptions<PricingDbContext> options) : base(options)
    {
    }

    public DbSet<RateCard> RateCards => Set<RateCard>();
    public DbSet<RateBand> RateBands => Set<RateBand>();
    public DbSet<PostalZone> PostalZones => Set<PostalZone>();
    public DbSet<PromotionRuleEntity> PromotionRules => Set<PromotionRuleEntity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RateCard>(entity =>
        {
            entity.ToTable("rate_cards");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.CarrierCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.ServiceLevelCode).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.HasIndex(x => new { x.CarrierCode, x.ServiceLevelCode, x.Currency, x.Status });
            entity.HasMany(x => x.Bands).WithOne().HasForeignKey(x => x.RateCardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RateBand>(entity =>
        {
            entity.ToTable("rate_bands");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DestinationZone).HasMaxLength(50).IsRequired();
            entity.Property(x => x.MinimumWeightKg).HasPrecision(12, 3);
            entity.Property(x => x.MaximumWeightKg).HasPrecision(12, 3);
            entity.Property(x => x.BasePrice).HasPrecision(18, 4);
            entity.Property(x => x.IncludedWeightKg).HasPrecision(12, 3);
            entity.Property(x => x.WeightIncrementKg).HasPrecision(12, 3);
            entity.Property(x => x.PricePerWeightIncrement).HasPrecision(18, 4);
            entity.Property(x => x.FuelSurchargePercentage).HasPrecision(8, 4);
            entity.Property(x => x.RemoteAreaFee).HasPrecision(18, 4);
            entity.Property(x => x.FragileFee).HasPrecision(18, 4);
            entity.Property(x => x.OversizeThresholdKg).HasPrecision(12, 3);
            entity.Property(x => x.OversizeFee).HasPrecision(18, 4);
            entity.Property(x => x.MinimumLogisticsCost).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.RateCardId, x.OriginNodeId, x.DestinationZone, x.MinimumWeightKg, x.MaximumWeightKg });
        });

        modelBuilder.Entity<PostalZone>(entity =>
        {
            entity.ToTable("postal_zones");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.PostalCodeFrom, x.PostalCodeTo });
        });

        modelBuilder.Entity<PromotionRuleEntity>(entity =>
        {
            entity.ToTable("promotion_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
            entity.Property(x => x.MinimumCartTotal).HasPrecision(18, 2);
            entity.Property(x => x.CustomerDiscountPercentage).HasPrecision(8, 4);
            entity.Property(x => x.PlatformSubsidyPercentage).HasPrecision(8, 4);
            entity.Property(x => x.SellerSubsidyPercentage).HasPrecision(8, 4);
            entity.Property(x => x.MaximumBenefit).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.IsActive, x.StartsAt, x.EndsAt });
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.ProcessedAt, x.OccurredAt });
        });
    }
}
