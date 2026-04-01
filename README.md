# Vendelo Fake Shipping Provider (C# 8 / .NET 6)

API fake em ASP.NET Core para validar integração de `MsgAccount` do `Vendelo.Back` com fornecedores externos de frete.

## Objetivo

Simular com fidelidade os endpoints consumidos no `Vendelo.Back`:

- `GET /oauth/authorize`
- `POST /oauth/token`
- `POST /api/v1/shipment/calculate`
- `POST /api/v1/cart`
- `POST /api/v1/shipment/generate`
- `POST /api/v1/cart/cancel`
- `GET /api/v1/orders/{orderId}`
- `GET /tracking/{tracking}`

Com suporte a:

- token fixo (`STATIC`)
- OAuth por `authorization_code` e `refresh_token`
- logs detalhados de request/response
- validações de payload com retorno de erros no padrão `{ error, errors }`

## Comportamento atual da API fake

- `POST /api/v1/shipment/calculate` retorna serviços com `id` `"1"`, `"2"` e `"3"` (compatíveis com o fluxo do Vendelo.Back).
- `POST /api/v1/cart` retorna `200 OK` com `{ id, protocol, self_tracking, error, errors }`.
- O pedido nasce com `status = "pending"`.
- `POST /api/v1/shipment/generate` muda o pedido para `status = "released"` e retorna `label_url` + `tracking`.
- `POST /api/v1/cart/cancel` muda o pedido para `status = "cancelled"`.

## Compatibilidade com fluxo do Vendelo.Back

Este projeto foi alinhado ao comportamento observado em:

- `VendeloApiShippingProviderClient.GetQuotes(...)`
- `ShippingProviderDispatchUseCase` (`AddCart`, `GenerateLabel`, `Cancel`, `GetOrderInfo`)
- refresh OAuth em `/oauth/token` com `grant_type=refresh_token`

## Configuração rápida

1. Build:

```bash
dotnet build .\Vendelo.FakeShippingProvider.csproj
```

2. Subir:

```bash
dotnet run --project .\Vendelo.FakeShippingProvider.csproj
```

3. Health:

```bash
curl http://localhost:80/health
```

## Configuração de autenticação

Variáveis relevantes:

- `Auth__Mode`: `static`, `oauth` ou `both`
- `Auth__StaticToken`
- `Auth__OAuthClientId`
- `Auth__OAuthClientSecret`
- `Auth__OAuthRedirectUri` (opcional, trava o redirect URI permitido no authorize/token)
- `Auth__OAuthRefreshToken`
- `Auth__OAuthAccessToken`

### Token fixo (STATIC)

Use no header:

```txt
Authorization: Bearer vendelo-static-token
```

### OAuth refresh

Fluxo completo suportado:

1. `GET /oauth/authorize` com `response_type=code`
2. `POST /oauth/token` com `grant_type=authorization_code`
3. `POST /oauth/token` com `grant_type=refresh_token`

Exemplo authorize:

```bash
curl -i "http://localhost:80/oauth/authorize?response_type=code&client_id=vendelo-client&redirect_uri=https%3A%2F%2Fgql001.vendelo.cloud%2Fapi%2FexternalShippingApiAuth&state=test-state"
```

O endpoint retorna `302 Found` redirecionando para `redirect_uri` com `code` e `state`.

Request:

```bash
curl -X POST http://localhost:80/oauth/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=refresh_token&refresh_token=vendelo-oauth-refresh-token&client_id=vendelo-client&client_secret=vendelo-secret"
```

Resposta:

```json
{
  "token_type": "Bearer",
  "access_token": "vendelo-oauth-access-token",
  "refresh_token": "vendelo-oauth-refresh-token",
  "expires_in": 3600
}
```

## Exemplo de fluxo end-to-end

### 1) Calcular frete

```bash
curl -X POST http://localhost:80/api/v1/shipment/calculate \
  -H "Authorization: Bearer vendelo-static-token" \
  -H "Content-Type: application/json" \
  -d '{
    "from": { "postal_code": "01001000" },
    "to": { "postal_code": "13083000" },
    "products": [
      {
        "id": "SKU-1",
        "width": 12,
        "height": 8,
        "length": 20,
        "weight": 0.8,
        "quantity": 2,
        "unit_price": 100
      }
    ]
  }'
```

### 2) Criar cart/pedido

```bash
curl -X POST http://localhost:80/api/v1/cart \
  -H "Authorization: Bearer vendelo-static-token" \
  -H "Content-Type: application/json" \
  -d '{
    "service": "1",
    "from": { "postal_code": "01001000", "name": "Origem Teste" },
    "to": { "postal_code": "13083000", "name": "Destino Teste" },
    "products": [
      {
        "id": "SKU-1",
        "width": 12,
        "height": 8,
        "length": 20,
        "weight": 0.8,
        "quantity": 2,
        "unit_price": 100
      }
    ]
  }'
```

Observação: o fake aceita IDs numéricos de serviço (`"1"`, `"2"`, `"3"`). Valores legados (`"sedex"`, `"pac"`, `"jadlog"`) continuam sendo normalizados internamente para manter compatibilidade.

### 3) Gerar etiqueta

```bash
curl -X POST http://localhost:80/api/v1/shipment/generate \
  -H "Authorization: Bearer vendelo-static-token" \
  -H "Content-Type: application/json" \
  -d '{ "orders": ["ord_xxxxxxxxxxxxxxxx"] }'
```

### 4) Consultar pedido

```bash
curl http://localhost:80/api/v1/orders/ord_xxxxxxxxxxxxxxxx \
  -H "Authorization: Bearer vendelo-static-token"
```

### 5) Cancelar pedido

```bash
curl -X POST http://localhost:80/api/v1/cart/cancel \
  -H "Authorization: Bearer vendelo-static-token" \
  -H "Content-Type: application/json" \
  -d '{
    "order": {
      "id": "ord_xxxxxxxxxxxxxxxx",
      "reason_id": "2",
      "description": "Cancelamento de teste"
    }
}'
```

### 6) Consultar tracking por URL pública

```bash
curl http://localhost:80/tracking/VX1234567BR
```

## MsgAccount (recomendado)

Para `EXTERNAL_SHIPPING_PROVIDER`:

- `Host`: URL do container (ex: `https://seu-dominio`)
- `UserName`: valor de `Auth__OAuthClientId` (se OAuth)
- `Password`: valor de `Auth__OAuthClientSecret` (se OAuth)
- `Token`: estático (`STATIC`) ou OAuth

## Debug e observabilidade

- `GET /debug/orders`: lista pedidos internos
- `POST /debug/reset`: limpa pedidos
- logs com requestId e body em middleware (`RequestAuditMiddleware`)

Se quiser desabilitar debug:

- `Behavior__EnableDebugRoutes=false`

## Docker para EasyPanel (container estático)

`Dockerfile` já pronto. Configure no EasyPanel:

- porta exposta: `80`
- variáveis de ambiente (base em `.env.example`)
- volume persistente em `/app/data` (recomendado)

Build local:

```bash
docker build -t vendelo-fake-shipping-provider:csharp .
```

Run local:

```bash
docker run -d --name fake-sp-cs \
  -p 80:80 \
  --env-file .env \
  -v %cd%/data:/app/data \
  vendelo-fake-shipping-provider:csharp
```
