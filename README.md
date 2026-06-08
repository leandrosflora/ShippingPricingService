# ShippingPricingService

Shipping Pricing Service em C#/.NET 8 responsável pela formação determinística do preço de frete.

## Endpoints

- `POST /shipping-prices/quotes/batch`
- `GET /shipping-prices/quotes/{quoteId}`
- `POST /rate-cards`
- `PUT /rate-cards/{rateCardId}`
- `POST /rate-cards/{rateCardId}/activate`
- `POST /rate-cards/{rateCardId}/retire`

## Arquitetura

- Minimal APIs para reduzir infraestrutura HTTP.
- Pricing engine puro, sem acesso a banco, Redis, HTTP ou relógio interno.
- PostgreSQL/EF Core para tabelas tarifárias, zonas postais, promoções e outbox.
- Redis/`IDistributedCache` para cache de cálculos e cotações com validade explícita.
- Rate cards versionados com ativação transacional e evento `PricingConfigurationChanged` no outbox.
