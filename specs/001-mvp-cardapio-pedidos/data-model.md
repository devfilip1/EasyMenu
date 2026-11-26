# Data Model — 001 MVP Cardápio e Pedidos

Modelo de domínio (`backend/src/EasyMenu.Domain`) e mapeamento EF Core/PostgreSQL
(`backend/src/EasyMenu.Infrastructure/Persistence`).

Convenções:

- As PKs são `Guid` (`uuid`, gerado na aplicação com `Guid.CreateVersion7()`).
- Os nomes de tabela e coluna ficam em snake_case (`UseSnakeCaseNamingConvention` via
  `EFCore.NamingConventions`).
- Dinheiro é `long` em centavos (`bigint`), sempre com sufixo `Cents`.
- Datas são `DateTimeOffset` em UTC (`timestamptz`), obtidas de `TimeProvider`.
- Enums são gravados como `text` em snake_case (`HasConversion<string>` com o conversor do
  projeto), com os mesmos valores do contrato.
- Toda entidade marcada com **[T]** implementa `ITenantOwned` e recebe o global query filter de
  tenant (research R3).
- **FK entre duas entidades [T] MUST ser composta com o tenant** (defesa em profundidade, revisão
  US1): a principal ganha a alternate key `(establishment_id, id)`, e a dependente referencia
  `(establishment_id, x_id)`. Assim o banco recusa, com 23503, um item apontando para a categoria
  de outro estabelecimento, mesmo se a aplicação errar. Hoje se aplica a
  `menu_items → categories`. Nos pedidos, `orders` só referencia `establishments`, e
  `order_items`/`order_parts` dependem de `orders` (não são [T]); `OrderItem.MenuItemId` continua
  sem FK (snapshot). Toda FK futura entre entidades [T] segue esta regra.

## Diagrama

```
Establishment 1───* User            (Owner: exatamente 1; Kitchen: 0..*)
Establishment 1───* Category 1───* MenuItem
Establishment 1───* Order 1───* OrderItem
                       └──1───* OrderPart  (1 por Station presente no pedido)
OrderItem.MenuItemId ─ ─ ─> MenuItem   (referência lógica, SEM FK: o item pode ser removido)
```

## Establishment

| Campo | Tipo | Regras |
|---|---|---|
| Id | Guid | PK |
| Name | string(100) | obrigatório, trim, 2–100 caracteres; editável (FR-005) |
| Slug | string(50) | obrigatório; único (índice único); regex `^[a-z0-9](?:[a-z0-9-]{1,48})[a-z0-9]$`; **imutável** (FR-003) |
| CreatedAt | DateTimeOffset | |

Validação do slug: o domínio expõe `Slug.Create(string)` (value object), que normaliza com trim
e converte para minúsculas. Slug inválido gera `DomainException("invalid_slug")` e slug em uso
gera `slug_taken` (verificado na Application e garantido pelo índice).

## User

| Campo | Tipo | Regras |
|---|---|---|
| Id | Guid | PK |
| EstablishmentId | Guid | FK → Establishment, obrigatório |
| Name | string(100)? | obrigatório para Kitchen; opcional para Owner |
| Email | string(254) | e-mail válido, como digitado |
| NormalizedEmail | string(254) | `Email.Trim().ToUpperInvariant()`; **único global** (FR-002) |
| PasswordHash | string | `PasswordHasher<User>`; a senha precisa ter ≥ 8 caracteres |
| Role | enum `Role` | `owner` \| `kitchen` |
| IsActive | bool | padrão true |
| SecurityStamp | string(64) | regenerado ao desativar (research R2) |
| CreatedAt | DateTimeOffset | |

`User` **não** recebe o filtro global, porque o login precisa achá-lo por e-mail entre todos os
tenants. Os serviços de equipe filtram explicitamente por `EstablishmentId` do `ITenantContext`.

Regras:

- O Owner não pode ser desativado. É invariante de domínio (`cannot_deactivate_owner`), mas a
  API nunca chega a expô-lo: os endpoints de equipe só enxergam usuários `kitchen`, então o id do
  Dono dá 404.
- `Deactivate()` faz `IsActive = false` e gera um novo `SecurityStamp`; repetir a operação é
  idempotente.

## Category [T]

| Campo | Tipo | Regras |
|---|---|---|
| Id | Guid | PK |
| EstablishmentId | Guid | FK |
| Name | string(60) | obrigatório, trim, 1–60 caracteres |
| Position | int | ≥ 0; nova categoria entra no fim (max + 1) |

Regras:

- Reordenar recebe a lista **completa** de ids do tenant, e `Position` passa a ser o índice na
  lista. Lista incompleta, com duplicatas ou com ids desconhecidos dá 400 `invalid_order_list`.
- Remover categoria que ainda tem itens dá 409 `category_not_empty` (FR-006).

## MenuItem [T]

| Campo | Tipo | Regras |
|---|---|---|
| Id | Guid | PK |
| EstablishmentId | Guid | FK |
| CategoryId | Guid | FK **composta** `(establishment_id, category_id)` → `categories(establishment_id, id)` (Restrict); o banco garante o mesmo tenant |
| Name | string(100) | obrigatório, 1–100 caracteres |
| Description | string(500)? | opcional; trim, e vazio ou só espaços vira `null` (na criação e na edição) |
| PhotoPath | string(300)? | caminho relativo no storage (`{establishmentId}/{guid}.jpg`) |
| PriceCents | long | > 0 e ≤ 10.000.000 (R$ 100 mil, teto de sanidade) |
| PromotionalPriceCents | long? | se presente: > 0 e < PriceCents (FR-008, constituição IV) |
| IsAvailable | bool | `true` = ativo; `false` = esgotado (FR-010) |
| Station | enum `Station` | `bar` \| `kitchen` (obrigatório) |
| Position | int | ordem dentro da categoria; novo item ou item movido entra no fim |

Campos derivados (não persistidos): `EffectivePriceCents = PromotionalPriceCents ?? PriceCents`;
`PhotoUrl = "/media/" + PhotoPath`.

Promoção inválida gera `DomainException("invalid_promotional_price")` e preço inválido gera
`invalid_price`. Os mesmos invariantes valem na criação e na edição (método `Update(...)` do
domínio).

Remoção é física, e o arquivo de foto também é apagado. Os pedidos não são afetados, porque
`OrderItem` guarda snapshot e não tem FK.

## Order [T]

| Campo | Tipo | Regras |
|---|---|---|
| Id | Guid | PK; usado nas rotas autenticadas (admin/cozinha) |
| EstablishmentId | Guid | FK |
| PublicToken | string(43) | 32 bytes aleatórios, base64url; **único**; usado em `/pedido/{publicToken}` (FR-031a) |
| PickupCode | string(4) | alfabeto `23456789ABCDEFGHJKMNPQRSTUVWXYZ`; índice único parcial `(establishment_id, pickup_code) WHERE status <> 'picked_up'` (FR-021) |
| CustomerName | string(60)? | trim; vazio vira null (FR-016) |
| Status | enum `OrderStatus` | `paid` \| `in_preparation` \| `ready` \| `picked_up` |
| TotalCents | long | = Σ `OrderItem.SubtotalCents`; calculado no domínio (FR-018) |
| PaymentTransactionId | string(100) | retornado pelo `IPaymentGateway` |
| CreatedAt | DateTimeOffset | momento da compra (= pago) |
| PickedUpAt | DateTimeOffset? | |
| xmin | uint | token de concorrência (Npgsql `IsRowVersion`) |

Construção: `Order.Place(establishmentId, customerName, lines, pickupCode, publicToken, now)`.
`lines` são `(MenuItem snapshot, quantity)`. O método:

- recusa lista vazia (`empty_cart`) e quantidade fora de 1..99 (`invalid_quantity`);
- agrupa linhas repetidas do mesmo item, somando as quantidades (a soma também respeita ≤ 99);
- cria um `OrderItem` por item e uma `OrderPart` por `Station` presente, com status `received`;
- calcula `TotalCents`;
- define `Status = paid`.

### Status derivado (FR-026)

`Order.RecalculateStatus()`, chamado após toda mudança de parte:

| Condição | Status |
|---|---|
| `PickedUpAt` definido | `picked_up` |
| todas as partes `ready` | `ready` |
| alguma parte `in_preparation` ou `ready` | `in_preparation` |
| todas `received` | `paid` |

`Order.MarkPickedUp(now)` só é permitido quando `Status == ready`; caso contrário dá
`DomainException("order_not_ready")`. Marcar um pedido já `picked_up` é idempotente.

## OrderItem

| Campo | Tipo | Regras |
|---|---|---|
| Id | Guid | PK |
| OrderId | Guid | FK → Order (Cascade) |
| MenuItemId | Guid | referência lógica, sem FK |
| NameSnapshot | string(100) | nome no momento da compra (FR-019) |
| UnitPriceCents | long | preço efetivo no momento da compra |
| Quantity | int | 1..99 |
| Station | enum `Station` | snapshot do setor |
| SubtotalCents | long | = UnitPriceCents × Quantity (persistido) |

## OrderPart

| Campo | Tipo | Regras |
|---|---|---|
| Id | Guid | PK |
| OrderId | Guid | FK → Order (Cascade); único `(order_id, station)` |
| Station | enum `Station` | |
| Status | enum `PartStatus` | `received` \| `in_preparation` \| `ready` |
| UpdatedAt | DateTimeOffset | |

### Transições de parte (FR-025)

```
received ──► in_preparation ──► ready
    └───────────────────────────►┘
```

`OrderPart.MoveTo(target)`:

- alvo igual ao status atual: no-op (idempotente);
- avanço válido: aplica;
- retrocesso: `DomainException("invalid_status_transition")`;
- qualquer mudança em pedido `picked_up`: `invalid_status_transition`.

`OrderPart` e `OrderItem` não implementam `ITenantOwned`. Só são acessados via `Order`, que já é
filtrado.

## Índices

| Tabela | Índice |
|---|---|
| establishments | unique(slug) → `ix_establishments_slug` (23505 → 409 `slug_taken`) |
| users | unique(normalized_email) → `ix_users_normalized_email` (23505 → 409 `email_taken`); (establishment_id) |
| categories | (establishment_id, position) |
| menu_items | (establishment_id, category_id, position) |
| orders | unique(public_token) → `ix_orders_public_token`; unique parcial (establishment_id, pickup_code) where status <> 'picked_up' → `ix_orders_establishment_id_pickup_code` (23505 → retry no checkout); (establishment_id, status, created_at desc) |
| order_parts | unique(order_id, station) |

## Abstrações da Application (interfaces)

| Interface | Implementação | Camada |
|---|---|---|
| `IAppDbContext` | `AppDbContext` | Infrastructure |
| `ITenantContext` | `HttpTenantContext` (claims) | Api |
| `IPasswordHasher` | `IdentityPasswordHasher` | Infrastructure |
| `ITokenService` | `JwtTokenService` | Infrastructure |
| `IImageStorage` | `LocalDiskImageStorage` | Infrastructure |
| `IPaymentGateway` | `FakePaymentGateway` | Infrastructure |
| `IPickupCodeGenerator` | `RandomPickupCodeGenerator` (usa `PickupCode.Generate()` do domínio; substituível nos testes) | Application |
| `IOrderNotifier` | `SignalROrderNotifier` | Api |
| `ISessionTerminator` | `HubConnectionTracker` (aborta as conexões do usuário) | Api |
| `TimeProvider` | `TimeProvider.System` (`FakeTimeProvider` nos testes) | .NET |
