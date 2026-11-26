# Implementation Plan: EasyMenu MVP — Cardápio Digital e Pedidos

**Branch**: `001-mvp-cardapio-pedidos` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-mvp-cardapio-pedidos/spec.md`

## Summary

MVP ponta a ponta do EasyMenu. O Dono se cadastra com o estabelecimento e monta o cardápio
(categorias e itens com foto, promoção, disponibilidade e setor). O consumidor convidado compra
pelo cardápio público `/m/{slug}`, com pagamento simulado, e recebe um código de retirada. A
Cozinha opera um painel em tempo real (seções Bar e Cozinha) que deriva o status do pedido a
partir das partes. O consumidor acompanha o status ao vivo, e o Dono gerencia a equipe e consulta
os pedidos.

Abordagem técnica:

- API ASP.NET Core (.NET 10, controllers) em Clean Architecture leve, com EF Core e PostgreSQL.
- JWT com claims de papel, de `establishment_id` e de security stamp, validado a cada request
  para cortar acesso imediatamente.
- Global query filter de tenant.
- SignalR com grupos por estabelecimento e por pedido público.
- `IImageStorage` em disco local, servido como arquivo estático.
- `IPaymentGateway` com implementação fake.
- Front Angular 22 (standalone e signals) consumindo apenas o client gerado de
  `contracts/openapi.yaml`.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (backend); TypeScript 6 / Angular 22.3 (frontend)

**Primary Dependencies**:

- Backend:
  - ASP.NET Core 10 (controllers, `Microsoft.AspNetCore.OpenApi`, SignalR, JwtBearer);
  - EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` 10, `EFCore.NamingConventions` 10;
  - `Microsoft.Extensions.Identity.Core` (somente o `PasswordHasher`).
- Frontend:
  - Angular 22 (standalone, signals, router com input binding);
  - `@microsoft/signalr`;
  - `ng-openapi-gen` (client gerado).

**Storage**: PostgreSQL 17 (dados) e disco local `Storage:ImagesPath` (fotos, servidas em
`/media`).

**Testing**:

- Backend: xUnit, FluentAssertions 7, Testcontainers.PostgreSql, `WebApplicationFactory` e
  `Microsoft.AspNetCore.SignalR.Client`, com `Microsoft.Extensions.TimeProvider.Testing` para
  controlar o relógio.
- Frontend: Vitest via `ng test`, com `HttpTestingController` e um fake de `HubConnection`.
- E2E: Playwright contra API e front reais.

**Target Platform**: API em Linux container ou Windows (dev). SPA em navegadores móveis
modernos (≥ 360 px) e desktop (painel/admin).

**Project Type**: aplicação web (`backend/` + `frontend/`, já existentes do scaffolding).

**Performance Goals**: eventos em tempo real ≤ 5 s ponta a ponta (SC-003/SC-004). p95 da API
< 300 ms na escala do MVP (meta de referência, sem verificação automatizada no MVP).

**Constraints**:

- Isolamento multi-tenant estrito.
- Dinheiro como inteiro em centavos.
- Uma instância da API: o tracker de conexões e os grupos SignalR ficam em memória, sem
  backplane.

**Scale/Scope**: dezenas de estabelecimentos, até cerca de 200 pedidos por noite por
estabelecimento e até cerca de 10 painéis abertos por estabelecimento. São 30 operações REST,
2 hubs e cerca de 12 telas.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Princípio | Como o plano atende | Status |
|---|---|---|
| I. TDD Inegociável | Em `tasks.md`, toda `[IMPL-*]` depende de uma `[TEST-*]` anterior, escrita pelo Testador e commitada vermelha. Backend: unidade de domínio + integração (WebApplicationFactory + Testcontainers). Front: componente/serviço (Vitest) + E2E Playwright para cadastro → cardápio, compra → acompanhamento e painel → retirada. | ✅ |
| II. Contrato Primeiro | `contracts/openapi.yaml` completo (30 operações) e `contracts/realtime.md` foram escritos **antes** das tasks. Front só usa o client gerado. `OpenApiContractTests` valida contrato × API em rotas, status codes e schemas (constituição 1.0.1). Operações não implementadas estão marcadas `x-pending: true`, e o Dev Back remove a marca ao implementar. | ✅ |
| III. Isolamento Multi-tenant | Claim `establishment_id`, `ITenantContext`, global query filter, guarda no `SaveChanges` e 404 em acesso cruzado. Cada endpoint autenticado tem teste de isolamento (tarefas marcadas "isolamento"). Os grupos SignalR também são por tenant. | ✅ |
| IV. Integridade Monetária | `long` em centavos, com promoção `0 < promo < preço` no domínio. `OrderItem` guarda snapshot de nome/preço/setor. O total é calculado em `Order.Place`, e o request não tem campo de preço. | ✅ |
| V. Simplicidade | `IPaymentGateway` + `FakePaymentGateway`. Consumidor convidado. Sem MediatR, repositórios genéricos, refresh token ou backplane. Abstrações só onde há troca prevista ou necessidade de teste (`IImageStorage`, `IPaymentGateway`, `IOrderNotifier`, `TimeProvider`). | ✅ |
| Stack | .NET 10, EF Core, PostgreSQL, SignalR, JWT Owner/Kitchen; Angular standalone + signals, mobile-first; Docker Compose. | ✅ |

**Re-check pós-design (Fase 1)**: ✅ sem violações. O único ponto de atenção é a
escalabilidade horizontal do SignalR, registrada em Riscos (não é violação).

## Project Structure

### Documentation (this feature)

```text
specs/001-mvp-cardapio-pedidos/
├── spec.md
├── plan.md              # este arquivo
├── research.md          # decisões R1..R13
├── data-model.md        # entidades, invariantes, índices
├── quickstart.md        # roteiro de validação
├── contracts/README.md  # aponta para /contracts (fonte única)
├── checklists/requirements.md
└── tasks.md             # /speckit-tasks
contracts/               # (raiz do repo) openapi.yaml + realtime.md
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── EasyMenu.Domain/
│   │   ├── Common/            # DomainException, ITenantOwned
│   │   ├── Establishments/    # Establishment, Slug
│   │   ├── Users/             # User, Role
│   │   ├── Menu/              # Category, MenuItem, Station
│   │   └── Orders/            # Order, OrderItem, OrderPart, OrderStatus, PartStatus, PickupCode
│   ├── EasyMenu.Application/
│   │   ├── Abstractions/      # IAppDbContext, ITenantContext, IPasswordHasher, ITokenService,
│   │   │                      # IImageStorage, IPaymentGateway, IPickupCodeGenerator,
│   │   │                      # IOrderNotifier, ISessionTerminator
│   │   ├── Common/            # Errors (ErrorCodes, NotFoundException, ValidationException)
│   │   ├── Auth/              # AuthService + DTOs
│   │   ├── Establishments/    # EstablishmentService
│   │   ├── Menu/              # CategoryService, MenuItemService, PublicMenuService, ImageFormat
│   │   ├── Orders/            # CheckoutService, KitchenService, OrderQueryService,
│   │   │                      # RandomPickupCodeGenerator, OrderMappings
│   │   ├── Staff/             # StaffService
│   │   └── DependencyInjection.cs
│   ├── EasyMenu.Infrastructure/
│   │   ├── Persistence/       # AppDbContext, Configurations/, Migrations/, TenantSaveChangesInterceptor
│   │   ├── Auth/              # JwtTokenService, JwtOptions, IdentityPasswordHasher
│   │   ├── Storage/           # LocalDiskImageStorage, StorageOptions
│   │   ├── Payments/          # FakePaymentGateway
│   │   └── DependencyInjection.cs
│   └── EasyMenu.Api/
│       ├── Controllers/       # Auth, Establishment, Categories, MenuItems, PublicMenu,
│       │                      # PublicOrders, Kitchen, Orders, Staff, Health
│       ├── Hubs/              # KitchenHub, OrdersHub
│       ├── Realtime/          # SignalROrderNotifier, HubConnectionTracker
│       ├── Auth/              # HttpTenantContext, SecurityStampValidator, AuthPolicies
│       ├── ErrorHandling/     # ProblemDetailsExceptionHandler
│       └── Program.cs
└── tests/
    ├── EasyMenu.UnitTests/            # Domain/ e Application/ (espelha src)
    └── EasyMenu.IntegrationTests/
        ├── Infrastructure/            # factory, base, ApiDriver (helpers de cenário), hubs helpers
        ├── Contract/                  # OpenApiContractTests, OpenApiContractComparer, OpenApiDocuments
        ├── Auth/  Establishment/  Menu/  PublicMenu/  Checkout/  Kitchen/  Orders/  Staff/
        └── Realtime/                  # KitchenHubTests, OrdersHubTests

frontend/
├── src/app/
│   ├── api/generated/         # client gerado (não versionado)
│   ├── core/
│   │   ├── auth/              # AuthStore, authInterceptor, guards (owner/kitchen/guest)
│   │   ├── realtime/          # HUB_CONNECTION_FACTORY, KitchenRealtimeService, OrderRealtimeService
│   │   ├── errors/            # problemCode(err) helper, mensagens pt-BR por ErrorCode
│   │   └── storage/           # OrderHistoryStore (localStorage)
│   ├── shared/                # BrlPipe, MediaUrlPipe, componentes visuais simples
│   └── features/
│       ├── auth/              # login.page, register.page
│       ├── admin/             # admin-shell, menu/ (categorias, itens, form), staff/, orders/, settings/
│       ├── public-menu/       # public-menu.page, CartStore, cart-sheet, checkout
│       ├── order-tracking/    # order-tracking.page
│       └── kitchen/           # kitchen.page, order-card
└── e2e/                       # specs Playwright + helpers (fixtures de API)
```

**Structure Decision**: aplicação web com `backend/` e `frontend/` independentes (constituição).
As páginas-esqueleto de `frontend/src/app/pages/` são movidas para `features/` na fase
Foundational.

Rotas finais do front:

| Rota | Acesso |
|---|---|
| `/login` e `/cadastro` | apenas visitante (`guestGuard`) |
| `/m/:slug` | público |
| `/pedido/:publicToken` | público |
| `/admin` → `/admin/cardapio`, `/admin/equipe`, `/admin/pedidos`, `/admin/pedidos/:orderId`, `/admin/estabelecimento` | `ownerGuard` |
| `/cozinha` | `kitchenGuard`: Owner ou Kitchen |

## Decisões-chave (detalhes em research.md)

1. **Auth** (R1/R2):
   - JWT de 8 h com `sub`, `role`, `establishment_id` e `sstamp`.
   - `OnTokenValidated` confere `IsActive` e `SecurityStamp` no banco a cada request.
   - Desativar troca o stamp e aborta as conexões SignalR do usuário: corte imediato.
2. **Tenant** (R3): query filter global, interceptor no `SaveChanges` e 404 em acesso cruzado.
   `User` fica fora do filtro (login global por e-mail).
3. **Fotos** (R4):
   - `IImageStorage` → `LocalDiskImageStorage`.
   - Upload multipart `PUT /api/menu-items/{id}/photo`.
   - JPEG/PNG/WebP validados por magic bytes, com no máximo 5 MB.
   - Servidas em `/media`.
4. **Tempo real** (R5):
   - `/hubs/kitchen`: grupo `establishment:{id}`, recebido da claim.
   - `/hubs/orders`: `SubscribeToOrder(publicToken)` → grupo `order:{token}`.
   - Os eventos carregam o snapshot completo, e o cliente recarrega via REST ao (re)conectar.
5. **Pagamento** (R6): `IPaymentGateway` com `FakePaymentGateway`, que sempre aprova.
6. **Pedido** (R7/R8):
   - Código de 4 caracteres com índice único parcial e retry.
   - `PublicToken` de 43 caracteres, base64url.
   - Status derivado das partes e persistido.
   - Transições monotônicas e idempotentes, com concorrência por `xmin`.
7. **JSON**:
   - camelCase, enums em snake_case minúsculo
     (`JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`).
   - `DefaultIgnoreCondition = WhenWritingNull`: campos opcionais são omitidos. No contrato,
     propriedades opcionais são `type: [T, "null"]` (equivalente a `T?` no C#), e as
     obrigatórias nunca são nulas.
   - `NumberHandling = JsonNumberHandling.Strict`, para o OpenAPI gerado emitir `integer` puro
     (o padrão Web aceita número como string e gera `[integer, string]`).
8. **Erros** (R10): ProblemDetails com `code` estável (enum `ErrorCode` no contrato).
9. **Cardápio público**:
   - Mostra os esgotados com `isAvailable=false`, porque US2-3 exige que o consumidor os veja
     sem poder adicionar.
   - Esconde categorias sem nenhum item disponível.
   - `categories: []` significa "cardápio indisponível".
10. **Regras de implementação que o contrato impõe**:
    - O nome do parâmetro de rota no controller MUST ser igual ao do contrato (`{id}`,
      `{orderId}`, `{slug}`, `{publicToken}`, `{station}`), porque o teste de contrato compara
      os caminhos literalmente.
    - Não usar prefixo de versão nas rotas.
11. **Endurecimento pós-revisão** (research R4, R14 e R15):
    - rate limiting por IP em login, register-owner, checkout e `SubscribeToOrder` (429
      `rate_limited`);
    - `/media` só com tipos de imagem, `nosniff` e sem listagem;
    - upload grande demais dá 413 `image_too_large`;
    - CSP e headers de segurança na imagem nginx do front.

## Ordem de execução e paralelismo

1. **Foundational**: entidades base, DbContext/migrations, auth/tenant, error handling, helpers
   de teste, núcleo do front (auth store, interceptor, guards, pipes, rotas). Bloqueia tudo.
2. **US1 → US2 → US3**: cada uma depende dos dados da anterior.
   - US2 precisa de cardápio cadastrado. Nos testes, isso é montado pelo `ApiDriver`, então o
     **back** de US2 pode começar assim que o back de US1 estiver verde.
   - US3 precisa de pedidos (US2).
3. **US4** depende de US2 (pedido público) e de US3 (eventos de status).
   **US5 (equipe)** depende só da Foundational e pode correr em paralelo com US2–US4.
   **US5 (pedidos admin)** depende de US2.
4. Dentro de cada story, a trilha **back** e a trilha **front** andam em paralelo:
   - o front testa contra o client gerado com `HttpTestingController`, sem depender da API
     pronta;
   - só o E2E da story depende das duas trilhas.

## Riscos

| Risco | Mitigação |
|---|---|
| Escalar a API para mais de 1 instância quebra os grupos SignalR e o tracker em memória | Fora do escopo do MVP. Quando necessário: Redis backplane e tracker distribuído. Documentado. |
| Consulta ao usuário a cada request autenticado | Consulta por PK, desprezível na escala alvo. Cache só se medições indicarem. |
| Divergência entre API e contrato (rota, status, schema) | `OpenApiContractTests` em nível de schema + `x-pending`. Para conformidade, cada action declara **todos** os status do contrato com `[ProducesResponseType<T>(status, contentType)]`, e erros usam um DTO `ApiProblem` igual ao schema `ProblemDetails` do contrato. |
| Fotos em disco local se perdem em deploy efêmero | Em produção, montar volume ou trocar por S3/R2 via `IImageStorage`. |

## Complexity Tracking

Nenhuma violação da constituição a justificar.
