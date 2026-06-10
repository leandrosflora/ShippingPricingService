# ShippingPricingService

Documentação do microserviço **ShippingPricingService**, responsável por calcular, versionar e consultar preços de frete a partir de tabelas tarifárias, zonas postais, regras promocionais e cache distribuído.

## Índice

- [Visão geral](#visão-geral)
- [Responsabilidades do serviço](#responsabilidades-do-serviço)
- [Arquitetura](#arquitetura)
- [Tecnologias e dependências](#tecnologias-e-dependências)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Modelo de domínio](#modelo-de-domínio)
- [Fluxo de cálculo de preço](#fluxo-de-cálculo-de-preço)
- [Cache e validade das cotações](#cache-e-validade-das-cotações)
- [Outbox e eventos](#outbox-e-eventos)
- [Configuração](#configuração)
- [Como executar localmente](#como-executar-localmente)
- [Health check e Swagger](#health-check-e-swagger)
- [Endpoints da API](#endpoints-da-api)
- [Contratos HTTP](#contratos-http)
- [Exemplos de uso](#exemplos-de-uso)
- [Persistência](#persistência)
- [Validações e regras de negócio](#validações-e-regras-de-negócio)
- [Observabilidade](#observabilidade)
- [Testes e qualidade](#testes-e-qualidade)
- [Troubleshooting](#troubleshooting)
- [Roadmap sugerido](#roadmap-sugerido)

## Visão geral

O `ShippingPricingService` é uma API ASP.NET Core baseada em Minimal APIs para precificar opções de entrega. O serviço recebe uma lista de rotas candidatas para um pedido, identifica a política tarifária vigente para cada rota, aplica sobretaxas e promoções, grava cotações temporárias em Redis e retorna o preço final para o cliente.

O microserviço também expõe endpoints administrativos para criar, atualizar, ativar e aposentar `rate cards` (tabelas tarifárias). A ativação de uma tabela é transacional: tabelas ativas equivalentes são aposentadas e um evento `PricingConfigurationChanged` é registrado no outbox para integração assíncrona com outros componentes.

## Responsabilidades do serviço

### Faz parte do escopo

- Calcular preço de frete em lote para até 20 rotas candidatas.
- Determinar o peso cobrável a partir do maior valor entre peso real e peso cúbico.
- Localizar zona postal de destino a partir do CEP informado.
- Selecionar uma tabela tarifária ativa conforme transportadora, nível de serviço, moeda, origem, zona de destino, faixa de peso e vigência.
- Aplicar componentes de preço:
  - frete base;
  - cobrança por peso excedente;
  - adicional de combustível;
  - taxa de área remota;
  - taxa de item frágil;
  - taxa de pacote excedente/oversize;
  - desconto ao cliente;
  - subsídio da plataforma;
  - subsídio do vendedor.
- Armazenar cotações e cálculos em Redis com TTL.
- Gerenciar ciclo de vida de rate cards: rascunho, ativo e aposentado.
- Registrar eventos de mudança de configuração tarifária em tabela de outbox.

### Fora do escopo atual

- Autenticação e autorização.
- Processamento/publicação efetiva dos eventos do outbox.
- Migrations do Entity Framework Core.
- Interface administrativa/web.
- Integração direta com transportadoras externas.
- Testes automatizados versionados no repositório.

## Arquitetura

O projeto segue uma organização simples em camadas:

```text
ShippingPricingService
├── Api/                       # Definição dos endpoints Minimal API
├── Application/               # Casos de uso, engine de cálculo e portas
│   └── Ports/                 # Interfaces de infraestrutura
├── Contracts/                 # DTOs de request/response da API
├── Domain/                    # Entidades, value records, enums e regras centrais
├── Infrastructure/            # EF Core, Redis e Outbox
│   ├── Cache/
│   ├── Outbox/
│   └── Persistence/
├── Program.cs                 # Composição da aplicação e DI
├── appsettings.json           # Configuração local padrão
└── ShippingPricingService.http # Exemplos para execução manual de requests
```

### Decisões arquiteturais

- **Minimal APIs**: endpoints enxutos, com injeção direta dos application services.
- **Engine pura de cálculo**: `ShippingPricingEngine` não acessa banco, Redis, HTTP ou relógio interno; recebe todos os dados necessários por parâmetro.
- **Ports and adapters**: a aplicação depende de interfaces (`IPricingPolicyProvider`, `IShippingPriceCache`) e a infraestrutura fornece implementações concretas.
- **PostgreSQL + EF Core**: persistência das tabelas tarifárias, zonas postais, promoções e outbox.
- **Redis via IDistributedCache**: cache distribuído para cálculos e cotações.
- **Outbox pattern**: mudanças tarifárias geram mensagens persistidas junto à transação de domínio.

## Tecnologias e dependências

- .NET 8 / C#
- ASP.NET Core Minimal APIs
- Entity Framework Core 8
- Npgsql Entity Framework Core Provider para PostgreSQL
- Redis com `Microsoft.Extensions.Caching.StackExchangeRedis`
- Health checks do ASP.NET Core/EF Core
- Swashbuckle/Swagger para documentação interativa em ambiente de desenvolvimento

## Estrutura do projeto

| Caminho | Responsabilidade |
| --- | --- |
| `Program.cs` | Registra serviços, EF Core, Redis, health checks, Swagger e endpoints. |
| `Api/ShippingPricingEndpoints.cs` | Endpoints de cotação de preços de frete. |
| `Api/RateCardEndpoints.cs` | Endpoints de administração de rate cards. |
| `Application/ShippingPricingApplicationService.cs` | Orquestra validação, busca de política, cache e engine de cálculo. |
| `Application/ShippingPricingEngine.cs` | Implementa as fórmulas de cálculo e aplicação de promoções. |
| `Application/RateCardApplicationService.cs` | Implementa criação, alteração, ativação e aposentadoria de rate cards. |
| `Application/ChargeableWeightCalculator.cs` | Calcula o peso cobrável. |
| `Application/PricingCacheKeyFactory.cs` | Gera chave determinística de cache para cálculos. |
| `Application/Ports/` | Contratos para provedores de política e cache. |
| `Contracts/` | DTOs públicos da API. |
| `Domain/` | Entidades, enums e records de domínio. |
| `Infrastructure/Persistence/` | `PricingDbContext` e provider EF de políticas tarifárias. |
| `Infrastructure/Cache/` | Implementação Redis do cache de cotações. |
| `Infrastructure/Outbox/` | Entidade e writer do outbox. |

## Modelo de domínio

### RateCard

Representa uma tabela tarifária versionada para uma combinação de:

- transportadora (`CarrierCode`);
- nível de serviço (`ServiceLevelCode`);
- moeda (`Currency`);
- janela de vigência (`EffectiveFrom`/`EffectiveUntil`);
- lista de faixas tarifárias (`Bands`).

Estados possíveis:

| Estado | Descrição |
| --- | --- |
| `Draft` | Tabela criada ou editável, ainda não usada em cálculos. |
| `Active` | Tabela vigente e elegível para cálculo. |
| `Retired` | Tabela aposentada, não elegível para novos cálculos. |

Ao ativar um `RateCard`, sua versão é incrementada. Ao aposentar, a versão também é incrementada.

### RateBand

Define uma faixa de preço por origem, zona de destino e intervalo de peso:

- `OriginNodeId`: nó/centro de origem.
- `DestinationZone`: zona postal de destino.
- `MinimumWeightKg` e `MaximumWeightKg`: intervalo de peso cobrável.
- `BasePrice`: preço base do frete.
- `IncludedWeightKg`: peso incluído no preço base.
- `WeightIncrementKg`: incremento usado para cobrar peso excedente.
- `PricePerWeightIncrement`: preço por incremento adicional.
- `FuelSurchargePercentage`: percentual de adicional de combustível.
- `RemoteAreaFee`: taxa adicional para área remota.
- `FragileFee`: taxa adicional para pacote frágil.
- `OversizeThresholdKg`: limite de peso para taxa de oversize.
- `OversizeFee`: taxa de oversize.
- `MinimumLogisticsCost`: custo logístico mínimo garantido.

### PostalZone

Mapeia intervalos de CEP para zonas de entrega:

- `Code`: código da zona.
- `PostalCodeFrom` e `PostalCodeTo`: intervalo numérico do CEP.
- `IsRemoteArea`: indica cobrança de taxa de área remota.
- `Priority`: prioridade quando mais de uma zona cobre o CEP.

### PromotionRule

Define regras de benefício aplicáveis ao frete:

- valor mínimo do carrinho;
- percentuais de desconto/subsídio;
- teto máximo de benefício;
- janela de vigência;
- prioridade de seleção.

A engine seleciona a promoção aplicável com menor valor de `Priority`.

## Fluxo de cálculo de preço

1. O cliente chama `POST /shipping-prices/quotes/batch` com os dados do comprador, vendedor, destino, carrinho e rotas candidatas.
2. O application service valida o request:
   - `SellerId` obrigatório;
   - `CartTotal` não negativo;
   - `Currency` obrigatória;
   - pelo menos 1 candidato;
   - no máximo 20 candidatos;
   - `CandidateId` único.
3. Para cada candidato, o serviço calcula o **peso cobrável**: `max(actualWeightKg, cubicWeightKg)`.
4. O CEP é normalizado para 8 dígitos numéricos.
5. O provider EF localiza:
   - a zona postal do CEP;
   - o rate card ativo e vigente;
   - a faixa tarifária correspondente;
   - as promoções ativas e aplicáveis ao vendedor/carrinho.
6. Se não houver política tarifária, o candidato retorna como indisponível.
7. A chave de cache do cálculo é gerada com dados do pedido, rota, pacote, versão tarifária e promoções.
8. Se houver cálculo em cache, a resposta retorna com `Source = "Cache"`.
9. Caso contrário, a engine calcula os componentes de preço.
10. O resultado é salvo em Redis:
    - cálculo: TTL de 60 segundos;
    - cotação por `QuoteId`: TTL de até 15 minutos, limitado pela vigência da política tarifária.
11. A API retorna uma lista de cotações, preservando uma resposta por candidato.

### Fórmula simplificada

```text
peso_cobravel = max(peso_real, peso_cubico)

peso_excedente = max(0, peso_cobravel - peso_incluido)
incrementos = ceil(peso_excedente / incremento_de_peso)
cobranca_peso = incrementos * preco_por_incremento

subtotal = frete_base + cobranca_peso
adicional_combustivel = subtotal * percentual_combustivel / 100

taxas = adicional_combustivel
      + taxa_area_remota_quando_aplicavel
      + taxa_fragil_quando_aplicavel
      + taxa_oversize_quando_aplicavel

custo_logistico = max(frete_base + cobranca_peso + taxas, custo_logistico_minimo)

preco_cliente = max(0, custo_logistico - desconto_cliente - subsidio_plataforma - subsidio_vendedor)
```

Todos os valores monetários calculados pela engine são arredondados para 2 casas decimais com `MidpointRounding.AwayFromZero`.

## Cache e validade das cotações

O serviço utiliza Redis para dois tipos de cache:

| Tipo | Chave | TTL | Finalidade |
| --- | --- | --- | --- |
| Cálculo | `calculation:{SHA256}` | 60 segundos | Evitar recalcular a mesma combinação de pedido/rota/política. |
| Cotação | `quote:{quoteId}` | Até 15 minutos | Permitir recuperar uma cotação pelo identificador retornado. |

A validade real da cotação é o menor valor entre:

- `requestedAt + 15 minutos`;
- `EffectiveUntil` da política tarifária.

## Outbox e eventos

Ao ativar ou aposentar um rate card, o serviço grava uma mensagem na tabela `outbox_messages` com o tipo:

```text
PricingConfigurationChanged
```

Payload registrado:

```json
{
  "rateCardId": "guid",
  "carrierCode": "MELI-LOGISTICS",
  "serviceLevelCode": "EXPRESS",
  "currency": "BRL",
  "version": 1,
  "occurredAt": "2026-06-10T00:00:00Z"
}
```

> Observação: o projeto grava a mensagem no outbox, mas não contém um worker/publicador para processar e marcar `ProcessedAt`.

## Configuração

A configuração padrão fica em `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "PricingDb": "Host=localhost;Port=5432;Database=shipping_pricing;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Variáveis de ambiente

Em ambientes de deploy, recomenda-se sobrescrever as connection strings por variáveis de ambiente:

```bash
ConnectionStrings__PricingDb="Host=postgres;Port=5432;Database=shipping_pricing;Username=app;Password=senha-segura"
ConnectionStrings__Redis="redis:6379"
ASPNETCORE_ENVIRONMENT="Production"
```

### User secrets em desenvolvimento

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:PricingDb" "Host=localhost;Port=5432;Database=shipping_pricing;Username=postgres;Password=postgres"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
```

## Como executar localmente

### Pré-requisitos

- .NET SDK 8 instalado.
- PostgreSQL acessível.
- Redis acessível.

### Restaurar e compilar

```bash
dotnet restore
dotnet build
```

### Executar a API

```bash
dotnet run --project ShippingPricingService.csproj
```

A porta local depende do perfil de execução do ASP.NET Core. O arquivo `ShippingPricingService.http` usa como exemplo:

```text
http://localhost:5075
```

## Health check e Swagger

### Health check

```http
GET /health
```

O health check inclui verificação do `PricingDbContext`.

### Swagger

Em ambiente `Development`, a aplicação habilita:

```text
/swagger
/swagger/index.html
```

## Endpoints da API

### Cotações de frete

#### `POST /shipping-prices/quotes/batch`

Calcula cotações para uma lista de rotas candidatas.

**Request:** `BatchShippingPriceRequest`

| Campo | Tipo | Obrigatório | Descrição |
| --- | --- | --- | --- |
| `buyerId` | `guid` | Sim | Identificador do comprador. |
| `sellerId` | `guid` | Sim | Identificador do vendedor. |
| `destinationPostalCode` | `string` | Sim | CEP de destino. Deve conter 8 dígitos após normalização. |
| `cartTotal` | `decimal` | Sim | Valor total do carrinho. Não pode ser negativo. |
| `currency` | `string` | Sim | Moeda da cotação, por exemplo `BRL`. |
| `requestedAtUtc` | `datetime?` | Não | Data/hora de referência. Se omitida, usa UTC atual. |
| `candidates` | `array` | Sim | Lista de rotas candidatas. Mínimo 1 e máximo 20. |

**Candidate:** `ShippingPriceCandidateRequest`

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `candidateId` | `string` | Identificador único do candidato dentro do request. |
| `routeId` | `string` | Identificador da rota logística. |
| `originNodeId` | `guid` | Origem logística usada para seleção da faixa tarifária. |
| `carrierCode` | `string` | Código da transportadora. |
| `serviceLevelCode` | `string` | Código do nível de serviço. |
| `package` | `object` | Perfil do pacote. |

**Package:** `PackageProfileDto`

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `actualWeightKg` | `decimal` | Peso real. Deve ser maior que zero. |
| `cubicWeightKg` | `decimal` | Peso cúbico. Não pode ser negativo. |
| `isFragile` | `bool` | Indica se aplica taxa de item frágil. |
| `isRestricted` | `bool` | Reservado no contrato atual; não participa da fórmula de cálculo. |
| `category` | `string?` | Categoria do item; participa da chave de cache. |

**Response:** `BatchShippingPriceResponse`

```json
{
  "quotes": [
    {
      "candidateId": "route-1",
      "available": true,
      "quoteId": "f7ac6304-a4b7-4149-a3a1-f4f584a6889f",
      "routeId": "route_a13f22",
      "currency": "BRL",
      "chargeableWeightKg": 6.8,
      "logisticsCost": 32.99,
      "customerPrice": 22.99,
      "platformSubsidy": 3.00,
      "sellerSubsidy": 2.00,
      "discount": 5.00,
      "adjustments": [
        {
          "code": "BASE_FREIGHT",
          "description": "Base freight",
          "type": "BaseFreight",
          "amount": 19.90
        }
      ],
      "rateCardVersion": 1,
      "source": "Calculated",
      "expiresAt": "2026-06-10T12:15:00Z",
      "unavailableReason": null
    }
  ]
}
```

Quando o candidato não pode ser precificado, `available` retorna `false`, os valores monetários retornam `null` e `unavailableReason` informa a causa.

#### `GET /shipping-prices/quotes/{quoteId}`

Recupera uma cotação previamente salva no cache Redis.

**Respostas:**

| Status | Descrição |
| --- | --- |
| `200 OK` | Cotação encontrada. |
| `404 Not Found` | Cotação inexistente ou expirada. |

### Rate cards

#### `POST /rate-cards`

Cria um novo rate card em estado `Draft`.

**Response:** `201 Created` com `RateCardResponse`.

#### `PUT /rate-cards/{rateCardId}`

Atualiza um rate card existente. Apenas rate cards em `Draft` podem ser atualizados.

**Response:** `200 OK` com `RateCardResponse`.

#### `POST /rate-cards/{rateCardId}/activate`

Ativa um rate card em `Draft`. Durante a ativação:

1. abre uma transação;
2. localiza rate cards ativos da mesma combinação de transportadora, nível de serviço e moeda;
3. aposenta os rate cards ativos encontrados;
4. ativa o rate card solicitado;
5. registra evento `PricingConfigurationChanged` no outbox;
6. persiste e confirma a transação.

**Response:** `200 OK` com `RateCardResponse`.

#### `POST /rate-cards/{rateCardId}/retire`

Aposenta um rate card e registra evento `PricingConfigurationChanged` no outbox.

**Response:** `200 OK` com `RateCardResponse`.

## Contratos HTTP

### RateCardRequest

```json
{
  "code": "MELI-EXPRESS-BRL-2026-01",
  "carrierCode": "MELI-LOGISTICS",
  "serviceLevelCode": "EXPRESS",
  "currency": "BRL",
  "effectiveFrom": "2026-06-01T00:00:00Z",
  "effectiveUntil": "2026-12-31T23:59:59Z",
  "bands": [
    {
      "originNodeId": "db082eda-6e8d-4eb8-9c18-70002da6fc97",
      "destinationZone": "SP-CAPITAL",
      "minimumWeightKg": 0.0,
      "maximumWeightKg": 10.0,
      "basePrice": 19.90,
      "includedWeightKg": 2.0,
      "weightIncrementKg": 1.0,
      "pricePerWeightIncrement": 2.50,
      "fuelSurchargePercentage": 5.0,
      "remoteAreaFee": 8.00,
      "fragileFee": 3.50,
      "oversizeThresholdKg": 8.0,
      "oversizeFee": 12.00,
      "minimumLogisticsCost": 15.00
    }
  ]
}
```

### RateCardResponse

```json
{
  "id": "0a3caebf-3a08-451c-99fd-66b4ce198c0d",
  "code": "MELI-EXPRESS-BRL-2026-01",
  "carrierCode": "MELI-LOGISTICS",
  "serviceLevelCode": "EXPRESS",
  "currency": "BRL",
  "version": 0,
  "status": "Draft",
  "effectiveFrom": "2026-06-01T00:00:00Z",
  "effectiveUntil": "2026-12-31T23:59:59Z",
  "bands": []
}
```

## Exemplos de uso

### Health check

```bash
curl -i http://localhost:5075/health
```

### Criar rate card

```bash
curl -X POST "http://localhost:5075/rate-cards" \
  -H "Content-Type: application/json" \
  -d '{
    "code": "MELI-EXPRESS-BRL-2026-01",
    "carrierCode": "MELI-LOGISTICS",
    "serviceLevelCode": "EXPRESS",
    "currency": "BRL",
    "effectiveFrom": "2026-06-01T00:00:00Z",
    "effectiveUntil": "2026-12-31T23:59:59Z",
    "bands": [
      {
        "originNodeId": "db082eda-6e8d-4eb8-9c18-70002da6fc97",
        "destinationZone": "SP-CAPITAL",
        "minimumWeightKg": 0.0,
        "maximumWeightKg": 10.0,
        "basePrice": 19.90,
        "includedWeightKg": 2.0,
        "weightIncrementKg": 1.0,
        "pricePerWeightIncrement": 2.50,
        "fuelSurchargePercentage": 5.0,
        "remoteAreaFee": 8.00,
        "fragileFee": 3.50,
        "oversizeThresholdKg": 8.0,
        "oversizeFee": 12.00,
        "minimumLogisticsCost": 15.00
      }
    ]
  }'
```

### Ativar rate card

```bash
curl -X POST "http://localhost:5075/rate-cards/{rateCardId}/activate"
```

### Calcular cotações em lote

```bash
curl -X POST "http://localhost:5075/shipping-prices/quotes/batch" \
  -H "Content-Type: application/json" \
  -d '{
    "buyerId": "046776ea-44ca-4921-8f18-6cce4d0848f8",
    "sellerId": "26937f31-12b4-457c-826f-f1df41a03d17",
    "destinationPostalCode": "05726-100",
    "cartTotal": 349.90,
    "currency": "BRL",
    "requestedAtUtc": "2026-06-10T16:00:00Z",
    "candidates": [
      {
        "candidateId": "route-1",
        "routeId": "route_a13f22",
        "originNodeId": "db082eda-6e8d-4eb8-9c18-70002da6fc97",
        "carrierCode": "MELI-LOGISTICS",
        "serviceLevelCode": "EXPRESS",
        "package": {
          "actualWeightKg": 4.2,
          "cubicWeightKg": 6.8,
          "isFragile": false,
          "isRestricted": false,
          "category": "electronics"
        }
      }
    ]
  }'
```

### Recuperar cotação por ID

```bash
curl -i "http://localhost:5075/shipping-prices/quotes/{quoteId}"
```

## Persistência

O `PricingDbContext` mapeia as seguintes tabelas:

| Tabela | Entidade | Uso |
| --- | --- | --- |
| `rate_cards` | `RateCard` | Cabeçalho das tabelas tarifárias. |
| `rate_bands` | `RateBand` | Faixas de preço por origem, zona e peso. |
| `postal_zones` | `PostalZone` | Intervalos de CEP e metadados de zona. |
| `promotion_rules` | `PromotionRuleEntity` | Promoções e subsídios aplicáveis. |
| `outbox_messages` | `OutboxMessage` | Eventos pendentes para publicação externa. |

### Observação sobre migrations

O repositório atual não contém migrations do EF Core. Para criar migrations, use um comando semelhante a:

```bash
dotnet ef migrations add InitialCreate
```

E aplique no banco com:

```bash
dotnet ef database update
```

> Ajuste a estratégia de migrations conforme o pipeline de deploy do ambiente.

## Validações e regras de negócio

### Request de cotação

- `SellerId` não pode ser `Guid.Empty`.
- `CartTotal` não pode ser negativo.
- `Currency` deve ser informada.
- Deve haver ao menos 1 candidato.
- São permitidos no máximo 20 candidatos por request.
- `CandidateId` deve ser único dentro do request.
- CEP de destino deve conter exatamente 8 dígitos após normalização.

### Pacote

- `actualWeightKg` deve ser maior que zero.
- `cubicWeightKg` não pode ser negativo.
- Pacote acima do `MaximumWeightKg` da regra tarifária é marcado como indisponível para aquele candidato.

### Rate card

- `Code`, `CarrierCode` e `ServiceLevelCode` são obrigatórios.
- `Currency` deve ter 3 caracteres, seguindo o padrão ISO-4217.
- `EffectiveUntil` deve ser maior que `EffectiveFrom`.
- Deve haver ao menos uma faixa tarifária.
- Apenas rate cards em `Draft` podem ser atualizados.
- Apenas rate cards em `Draft` podem ser ativados.

### Rate band

- `OriginNodeId` é obrigatório.
- `DestinationZone` é obrigatória.
- `MinimumWeightKg` não pode ser negativo.
- `MaximumWeightKg` deve ser maior que `MinimumWeightKg`.
- `WeightIncrementKg` deve ser maior que zero.

## Observabilidade

Recursos já presentes:

- `ILogger` no fluxo de cotação para registrar falhas por candidato.
- Health check em `/health` com validação do `PricingDbContext`.
- Swagger em ambiente de desenvolvimento.

Recomendações para produção:

- Adicionar correlation ID por request.
- Exportar métricas de latência, taxa de cache hit/miss, indisponibilidade por motivo e quantidade de candidatos por request.
- Instrumentar tracing distribuído com OpenTelemetry.
- Monitorar backlog e idade das mensagens em `outbox_messages`.

## Testes e qualidade

Atualmente o repositório não contém um projeto de testes. Recomenda-se adicionar cobertura para:

- `ChargeableWeightCalculator`:
  - peso real maior que cúbico;
  - peso cúbico maior que real;
  - peso real inválido;
  - peso cúbico negativo.
- `ShippingPricingEngine`:
  - cálculo de frete base;
  - cobrança de peso excedente;
  - adicional de combustível;
  - taxa de área remota;
  - taxa de frágil;
  - taxa de oversize;
  - custo logístico mínimo;
  - promoções com teto de benefício.
- `RateCard`:
  - criação válida;
  - validações de campos obrigatórios;
  - ativação e incremento de versão;
  - bloqueio de atualização fora de `Draft`.
- `ShippingPricingApplicationService`:
  - candidato sem política retorna indisponível;
  - resposta de cache retorna `Source = "Cache"`;
  - limite máximo de candidatos.

Comandos úteis:

```bash
dotnet restore
dotnet build
dotnet test
```

## Troubleshooting

### `/health` retorna unhealthy

Verifique:

- connection string `ConnectionStrings:PricingDb`;
- disponibilidade do PostgreSQL;
- usuário, senha e permissões do banco;
- existência das tabelas esperadas.

### Erro ao calcular cotação: `Destination postal code is invalid`

O CEP informado precisa conter exatamente 8 dígitos após remoção de caracteres não numéricos. Exemplos válidos:

- `05726-100`
- `05726100`

### Candidato retorna `No active rate card found`

Possíveis causas:

- não existe zona postal para o CEP;
- não existe rate card ativo para transportadora, nível de serviço e moeda;
- a data solicitada está fora da vigência do rate card;
- não existe faixa tarifária para origem, zona de destino e peso cobrável;
- moeda do request difere da moeda do rate card.

### `GET /shipping-prices/quotes/{quoteId}` retorna 404

A cotação pode não existir ou ter expirado no Redis. O TTL máximo atual é de 15 minutos e também é limitado pelo `EffectiveUntil` da política tarifária.

### Swagger não aparece

O Swagger é habilitado apenas quando `ASPNETCORE_ENVIRONMENT=Development`.

## Roadmap sugerido

- Adicionar projeto de testes unitários e integração.
- Criar migrations iniciais do EF Core.
- Implementar worker de outbox para publicar eventos e preencher `ProcessedAt`.
- Adicionar autenticação/autorização nos endpoints administrativos.
- Adicionar versionamento explícito da API.
- Criar Dockerfile e docker-compose com PostgreSQL e Redis.
- Adicionar pipeline CI com restore, build, test e análise estática.
- Padronizar respostas de erro com detalhes de validação.
- Adicionar seed de zonas postais e rate cards para ambiente local.

## Licença e manutenção

Este repositório ainda não contém arquivo `LICENSE`. Defina a licença conforme a política do projeto.

Mantenedor indicado no README anterior: Leandro Sflora.
