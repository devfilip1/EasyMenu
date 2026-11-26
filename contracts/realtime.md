# Contrato de tempo real (SignalR)

Complemento de [`openapi.yaml`](openapi.yaml), com o mesmo dono (agente Arquiteto). Os payloads
usam **os mesmos schemas** de `openapi.yaml` (`components.schemas`), serializados em JSON
camelCase, com enums em snake_case e campos opcionais omitidos quando ausentes.

Base URL: a mesma da API (`http://localhost:5080` em dev). Protocolo: JSON hub protocol do
SignalR. Cliente do front: `@microsoft/signalr`.

## Regra de ouro: REST é a fonte do estado

Os eventos são notificações. Ao **conectar e a cada reconexão**, o cliente MUST recarregar o
estado completo via REST e só então aplicar os eventos recebidos. Isso garante que nenhum pedido
se perde durante uma queda de conexão (edge case da spec).

- Painel da cozinha: `GET /api/kitchen/orders`
- Página do pedido: `GET /api/public/orders/{publicToken}`

Os eventos trazem o **snapshot completo** do recurso (não deltas): o cliente substitui o item
pelo `id`/`publicToken`.

---

## Hub `/hubs/kitchen` (painel da Cozinha)

| | |
|---|---|
| Autenticação | JWT obrigatório. No WebSocket ou SSE, vai na query string `?access_token=<jwt>` (o front usa `accessTokenFactory`). Policy `KitchenAccess`: papel `owner` ou `kitchen`. |
| Grupo | No `OnConnectedAsync`, o servidor adiciona a conexão ao grupo `establishment:{establishmentId}`, usando o id **da claim do token**. O cliente não informa o tenant. |
| Métodos cliente → servidor | Nenhum. As ações (mudar status, retirar) usam REST. |
| Corte de acesso | Quando a conta é desativada (`PUT /api/staff/{id}/deactivate`), o servidor aborta as conexões ativas daquele usuário, e a reconexão é recusada (401 no negotiate). |

### Eventos servidor → cliente

| Evento | Payload | Quando |
|---|---|---|
| `OrderCreated` | `KitchenOrder` | Pedido pago criado no estabelecimento. |
| `OrderUpdated` | `KitchenOrder` | Status de alguma parte mudou e/ou o pedido mudou de status (ex.: virou `ready`). |
| `OrderPickedUp` | `OrderPickedUpEvent` | Pedido marcado como retirado; o cliente remove o pedido da lista de andamento. |

```json
// OrderPickedUpEvent
{ "id": "0193f6c2-…", "pickupCode": "7KQ4" }
```

Regras de exibição no painel (FR-022/FR-023/FR-028):

- Cada `KitchenOrder.parts[i]` aparece **somente** na seção do seu `station` (`bar` → seção Bar,
  `kitchen` → seção Cozinha).
- A ordem é por `createdAt` crescente.
- Uma parte `ready` continua visível até o pedido ser retirado.

Eventos do estabelecimento A nunca chegam a conexões do estabelecimento B, porque os grupos são
isolados por tenant (testado em `KitchenHubTests`).

---

## Hub `/hubs/orders` (página pública do pedido)

| | |
|---|---|
| Autenticação | Anônimo. |
| Métodos cliente → servidor | `SubscribeToOrder(publicToken: string) → boolean`: valida o token. Se ele existir, adiciona a conexão ao grupo `order:{publicToken}` e retorna `true`; se não existir, retorna `false` e não inscreve. Chamar de novo após reconectar. |
| Grupo | `order:{publicToken}`. Só quem conhece o token longo recebe eventos (FR-031a). |
| Rate limiting | `SubscribeToOrder` tem limite por IP (janela fixa, `RateLimiting:OrderSubscribe:*`; padrão 30 chamadas por minuto). Ao exceder, a invocação falha com `HubException` cuja mensagem é exatamente `rate_limited`, e a conexão continua aberta. O cliente espera e tenta de novo; o estado vem do REST. |

### Eventos servidor → cliente

| Evento | Payload | Quando |
|---|---|---|
| `OrderUpdated` | `PublicOrder` | Qualquer mudança de status do pedido (`paid` → `in_preparation` → `ready` → `picked_up`). |

O front mostra o aviso "Seu pedido está pronto — retire no balcão" com o `pickupCode` quando
`status == "ready"` (FR-030).

O evento só é emitido quando o **status do pedido** muda. Mudança de parte que não altera o
status do pedido (ex.: a segunda parte entrando em preparo) não gera evento neste hub.

---

## Latência

O servidor emite os eventos logo depois do commit da transação que causou a mudança. A meta da
spec é de até 5 s entre a ação e a atualização na tela (SC-003/SC-004).

## Testes

- Backend: `Microsoft.AspNetCore.SignalR.Client` contra o `TestServer`, com transporte
  LongPolling e `HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()`. Os helpers
  ficam em `IntegrationTestBase`.
- Frontend: os serviços de realtime recebem a fábrica de `HubConnection` por injeção (token
  `HUB_CONNECTION_FACTORY`), o que permite trocá-la por um fake nos testes de unidade.
