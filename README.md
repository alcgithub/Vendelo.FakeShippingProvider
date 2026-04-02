# Vendelo Fake Shipping Provider (C# 8 / .NET 6)

API fake em ASP.NET Core para validar a integracao do `Vendelo.Back` com provedores externos de frete.

## Objetivo

Este projeto simula o ciclo completo de frete externo:

- autenticacao (`static` e `oauth`)
- cotacao de frete
- criacao de pedido (cart)
- geracao de etiqueta
- consulta de pedido e rastreio
- cancelamento

Tambem possui rotas de debug para facilitar testes locais.

## Servicos fake expostos

Atualmente o fake retorna 4 servicos de frete no `shipment/calculate`:

- `id = "1"`: `SEDEX (Correios)`
- `id = "2"`: `PAC (Correios)`
- `id = "3"`: `Jadlog Package (Jadlog)`
- `id = "4"`: `ChinaLog (ChinaLog)`

Regras atuais de calculo fake:

- valor base e prazo sao predefinidos por servico
- payload de entrada e validado (CEP, produtos, dimensoes/peso)
- respostas de erro seguem formato `{ "error": "...", "errors": { ... } }`

## Rotas (o que cada uma faz)

### Health

- `GET /health`
- Uso: verificar se a API fake esta no ar.

### OAuth - iniciar autorizacao

- `GET /oauth/authorize`
- Uso: simula tela de autorizacao e retorna `302` para `redirect_uri` com `code` e `state`.
- Query principal: `response_type=code`, `client_id`, `redirect_uri`, `state`.

### OAuth - trocar code ou refresh token

- `POST /oauth/token`
- Uso: retorna `access_token`/`refresh_token`.
- Suporta:
  - `grant_type=authorization_code`
  - `grant_type=refresh_token`

### Cotacao

- `POST /api/v1/shipment/calculate`
- Uso: calcula opcoes de frete por servico.
- Retorno: lista de servicos fake (`1`, `2`, `3`, `4`) com prazo/valor.

### Criar cart/pedido

- `POST /api/v1/cart`
- Uso: cria pedido de envio no provider fake.
- Retorno: `{ id, protocol, self_tracking, error, errors }`.
- Estado inicial: `pending`.

### Gerar etiqueta

- `POST /api/v1/shipment/generate`
- Uso: gera etiqueta para pedidos informados.
- Efeito: muda estado para `released`.
- Retorno: `label_url` e `tracking`.

Importante:

- para `EXTERNAL_SHIPPING_PROVIDER`, `tracking` deve ser URL completa (nao codigo puro)
- exemplo: `https://provider.fake/tracking/TRK123`

### Cancelar cart/pedido

- `POST /api/v1/cart/cancel`
- Uso: cancela pedido existente.
- Efeito: muda estado para `cancelled`.

### Consultar pedido

- `GET /api/v1/orders/{orderId}`
- Uso: consultar status e metadados do pedido no provider fake.

### Pagina publica de rastreio fake

- `GET /tracking/{tracking}`
- Uso: simular abertura de link de rastreio no navegador.

### Debug - listar pedidos internos

- `GET /debug/orders`
- Uso: inspecionar pedidos criados no armazenamento interno fake.

### Debug - resetar pedidos internos

- `POST /debug/reset`
- Uso: limpar base de pedidos fake para reexecutar cenarios.

## Compatibilidade com Vendelo.Back

Fluxo alinhado com:

- `VendeloApiShippingProviderClient.GetQuotes(...)`
- `ShippingProviderDispatchUseCase` (`AddCart`, `GenerateLabel`, `Cancel`, `GetOrderInfo`)
- refresh OAuth em `/oauth/token` com `grant_type=refresh_token`

Observacao para o front-end:

- fallback para `melhorrastreio` deve ocorrer apenas quando `integrationTag == "melhor envio"` (lower case)
- para provider externo, usar `tracking` ja no formato de URL completa

## Configuracao rapida

1. Build:

```bash
dotnet build .\Vendelo.FakeShippingProvider.csproj
```

2. Run:

```bash
dotnet run --project .\Vendelo.FakeShippingProvider.csproj
```

3. Health check:

```bash
curl http://localhost:80/health
```

## Variaveis de ambiente (autenticacao)

- `Auth__Mode`: `static`, `oauth` ou `both`
- `Auth__StaticToken`
- `Auth__OAuthClientId`
- `Auth__OAuthClientSecret`
- `Auth__OAuthRedirectUri` (opcional, valida `redirect_uri`)
- `Auth__OAuthRefreshToken`
- `Auth__OAuthAccessToken`
- `Behavior__EnableDebugRoutes` (`true/false`)

## MsgAccount recomendada no Vendelo

Para `EXTERNAL_SHIPPING_PROVIDER`:

- `Host`: URL do fake provider
- `UserName`: `Auth__OAuthClientId` (quando OAuth)
- `Password`: `Auth__OAuthClientSecret` (quando OAuth)
- `Token`: estatico (`static`) ou OAuth (`oauth/both`)

## Exemplo rapido de OAuth (authorize)

```bash
curl -i "http://localhost:80/oauth/authorize?response_type=code&client_id=vendelo-client&redirect_uri=https%3A%2F%2Fgql001.vendelo.cloud%2Fapi%2FexternalShippingApiAuth&state=test-state"
```

## Docker / EasyPanel

O projeto ja possui `Dockerfile`.

- porta: `80`
- configurar vars de ambiente (base `.env.example`)
- volume persistente em `/app/data` (recomendado)

Build:

```bash
docker build -t vendelo-fake-shipping-provider:csharp .
```

Run:

```bash
docker run -d --name fake-sp-cs \
  -p 80:80 \
  --env-file .env \
  -v %cd%/data:/app/data \
  vendelo-fake-shipping-provider:csharp
```
