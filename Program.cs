using Microsoft.EntityFrameworkCore;
using ShippingPricingService.Api;
using ShippingPricingService.Application;
using ShippingPricingService.Application.Ports;
using ShippingPricingService.Infrastructure.Cache;
using ShippingPricingService.Infrastructure.Outbox;
using ShippingPricingService.Infrastructure.Persistence;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Environment.ApplicationName;
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:5107";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

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

app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

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
