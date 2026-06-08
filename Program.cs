using Microsoft.EntityFrameworkCore;
using ShippingPricingService.Api;
using ShippingPricingService.Application;
using ShippingPricingService.Application.Ports;
using ShippingPricingService.Infrastructure.Cache;
using ShippingPricingService.Infrastructure.Outbox;
using ShippingPricingService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<PricingDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("PricingDb"));
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "shipping-pricing:";
});

builder.Services.AddScoped<ShippingPricingApplicationService>();
builder.Services.AddScoped<RateCardApplicationService>();
builder.Services.AddSingleton<ShippingPricingEngine>();
builder.Services.AddScoped<IPricingPolicyProvider, EfPricingPolicyProvider>();
builder.Services.AddScoped<IShippingPriceCache, RedisShippingPriceCache>();
builder.Services.AddScoped<IOutboxWriter, EfOutboxWriter>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<PricingDbContext>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapShippingPricingEndpoints();
app.MapRateCardEndpoints();

app.Run();
