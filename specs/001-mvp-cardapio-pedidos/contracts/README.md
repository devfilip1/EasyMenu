# Contratos desta feature

Os contratos ficam na raiz do monorepo, porque são a fonte única da verdade (constituição,
princípio II). Esta pasta não guarda cópias:

- REST: [`/contracts/openapi.yaml`](../../../contracts/openapi.yaml), com 30 operações do MVP
  mais `/health`.
- Tempo real: [`/contracts/realtime.md`](../../../contracts/realtime.md), com os hubs
  `/hubs/kitchen` e `/hubs/orders`.

## Mapa operação → user story

| Story | Operações |
|---|---|
| Foundational | `POST /api/auth/register-owner`, `POST /api/auth/login`, `GET /api/auth/me` |
| US1 | `GET/PUT /api/establishment`, `/api/categories*`, `/api/menu-items*` |
| US2 | `GET /api/public/establishments/{slug}/menu`, `POST /api/public/establishments/{slug}/orders`, `GET /api/public/orders/{publicToken}` (só a leitura inicial) |
| US3 | `GET /api/kitchen/orders`, `PUT /api/kitchen/orders/{orderId}/parts/{station}/status`, `PUT /api/kitchen/orders/{orderId}/pickup`, hub `/hubs/kitchen` |
| US4 | `GET /api/public/orders/{publicToken}` (página completa), hub `/hubs/orders` |
| US5 | `/api/staff*`, `GET /api/orders`, `GET /api/orders/{orderId}` |

## Como o contrato é verificado

- Backend: `OpenApiContractTests` compara rotas, parâmetros, status codes e schemas do contrato
  com o `/openapi/v1.json` gerado pela API. As operações ainda não implementadas estão marcadas
  `x-pending: true`, e o Dev Back remove a marca no commit que implementa a operação (única
  edição de contrato permitida a ele, constituição 1.0.1).
- Frontend: o client é gerado a partir do contrato (`npm run generate:api`), então uma mudança
  no contrato que quebre o uso aparece como erro de compilação.
