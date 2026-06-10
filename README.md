# ShippingPricingService

## Visão geral

O `ShippingPricingService` é um serviço responsável por calcular preços e prazos de frete para pedidos. Esta API fornece endpoints REST para consultar tarifas, simular envios e validar opções de transporte. O repositório destina-se a ser executado em .NET 8.

## Tecnologias

- .NET 8
- C#
- ASP.NET Core Web API
- (Opcional) Docker
- (Opcional) xUnit / NUnit para testes

## Requisitos

- .NET 8 SDK
- Git
- (Opcional) Docker

## Instalação

1. Clone o repositório:

   ```bash
   git clone https://github.com/leandrosflora/ShippingPricingService.git
   cd ShippingPricingService
   ```

2. Restaure pacotes e compile:

   ```bash
   dotnet restore
   dotnet build -c Release
   ```

## Configuração

As configurações ficam em `appsettings.json` e podem incluir chaves como `ShippingProviders`, `DefaultCurrency`, `Logging` e `ConnectionStrings` (se houver acesso a banco). Exemplo mínimo:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Shipping": {
    "DefaultProvider": "ProviderA",
    "Providers": {
      "ProviderA": {
        "ApiKey": "sua-chave-aqui",
        "BaseUrl": "https://api.provider-a.com"
      }
    }
  }
}
```

- Para variáveis sensíveis, use _secrets_ (`dotnet user-secrets`) ou variáveis de ambiente.

## Execução

Para executar localmente:

```bash
dotnet run --project src/ShippingPricingService
```

Ao iniciar, a API estará disponível em `https://localhost:5001` (por padrão). Use o Swagger (se habilitado) em `/swagger` para explorar os endpoints.

## Endpoints principais (exemplos)

- `GET /api/pricing/quote?origin={zip}&destination={zip}&weight={kg}`
  - Retorna cotações de frete para os provedores configurados.

- `POST /api/pricing/simulate`
  - Body: `{ "origin": "01001-000", "destination": "02002-000", "items": [{ "weight": 1.2, "length": 10, "width": 10, "height": 5 }] }`
  - Retorna preço e prazo estimado.

- `GET /api/providers` 
  - Lista provedores configurados e status.

A documentação do Swagger (se presente) fornece descrições completas de cada endpoint.

## Exemplo de uso

Curl para obter cotação:

```bash
curl "https://localhost:5001/api/pricing/quote?origin=01001-000&destination=02002-000&weight=2.5"
```

POST de simulação:

```bash
curl -X POST "https://localhost:5001/api/pricing/simulate" -H "Content-Type: application/json" -d '{"origin":"01001-000","destination":"02002-000","items":[{"weight":2.5,"length":20,"width":15,"height":10}]}'

```

## Testes

Se o projeto incluir testes (recomendado), execute:

```bash
dotnet test
```

Crie testes unitários cobrindo regras de cálculo de preço, integração com provedores e validações de entrada.

## Contribuição

1. Fork do repositório
2. Crie uma branch feature: `git checkout -b feature/nome-da-feature`
3. Faça commits pequenos e descritivos
4. Abra um Pull Request explicando a mudança

Siga o arquivo `CONTRIBUTING.md` e as regras de estilo definidas em `.editorconfig`.

## Convenções de código

- O projeto utiliza `.editorconfig` para formatação e estilo. Respeite as regras de nomenclatura e formatação.
- Escreva testes unitários para novas regras e mantenha cobertura adequada.

## CI / Pipeline

Adicionar pipeline para build, lint e testes (GitHub Actions, Azure Pipelines, etc.). Exemplo mínimo: build com `dotnet build --configuration Release` e `dotnet test`.

## Docker (opcional)

Exemplo de `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","ShippingPricingService.dll"]
```

## Arquitetura

- Minimal APIs para reduzir infraestrutura HTTP.
- Pricing engine puro, sem acesso a banco, Redis, HTTP ou relógio interno.
- PostgreSQL/EF Core para tabelas tarifárias, zonas postais, promoções e outbox.
- Redis/`IDistributedCache` para cache de cálculos e cotações com validade explícita.
- Rate cards versionados com ativação transacional e evento `PricingConfigurationChanged` no outbox.

## Licença

Especifique a licença do projeto (por exemplo, MIT) adicionando um arquivo `LICENSE` na raiz.

## Contato

Mantenedor: Leandro Sflora  
Repositório: https://github.com/leandrosflora/ShippingPricingService

---

Se precisar, posso gerar também exemplos de `appsettings.json`, `Dockerfile`, ou arquivos de pipeline CI em YAML.
