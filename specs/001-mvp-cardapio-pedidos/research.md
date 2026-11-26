# Research — 001 MVP Cardápio e Pedidos

Decisões técnicas da Fase 0. Não restou nenhum `NEEDS CLARIFICATION` no Technical Context.

## R1. Autenticação e papéis

- **Decision**: JWT Bearer (HS256) emitido pela própria API, com validade de 8 h e sem refresh token.
  Claims: `sub` (UserId), `role` (`Owner` | `Kitchen`), `establishment_id` e `sstamp` (security stamp).
  O hash de senha usa `PasswordHasher<T>` de `Microsoft.Extensions.Identity.Core` (PBKDF2), sem
  ASP.NET Identity completo.
  Policies: `OwnerOnly` (role Owner) e `KitchenAccess` (role Owner **ou** Kitchen, FR-033a).
- **Rationale**: a constituição define JWT com papéis Owner e Kitchen. Oito horas cobrem um turno,
  e sem refresh token o MVP fica mais simples (YAGNI). O `PasswordHasher` traz hash seguro sem as
  tabelas do Identity.
- **Alternatives**: ASP.NET Identity completo (tabelas e fluxos que o MVP não usa); cookies de
  sessão (atrapalham o SignalR cross-origin e o client gerado); refresh tokens (fora do escopo, já
  que a spec também exclui recuperação de senha).

## R2. Corte imediato de acesso ao desativar conta da Cozinha (FR-034)

- **Decision**: validar o **security stamp** a cada request. Em `JwtBearerEvents.OnTokenValidated`,
  a API carrega o usuário pelo `sub` (consulta por PK, `AsNoTracking`) e rejeita o token se o usuário
  não existir, estiver `IsActive = false` ou se o `SecurityStamp` atual diferir da claim `sstamp`.
  Desativar a conta grava `IsActive = false` e gera um novo `SecurityStamp`.
  No tempo real, o `KitchenHub` registra as conexões por `UserId` (`HubConnectionTracker`, em
  memória). Ao desativar, o serviço chama `Abort()` nas conexões daquele usuário, e a reconexão
  falha porque o handshake passa pela mesma validação.
- **Rationale**: o corte é imediato, tanto em HTTP quanto em WebSocket, ao custo de uma consulta
  por PK por request autenticado, o que é desprezível na escala do MVP. O security stamp também
  permite invalidar sessões no futuro (troca de senha).
- **Alternatives**: token de vida curta (o corte não seria imediato); lista de revogação (mais
  estado para manter); cache do status (atrasaria o corte). O tracker em memória vale para uma
  única instância da API, que é o caso do MVP. Escalar horizontalmente vai exigir backplane e está
  registrado em Complexity/Riscos.

## R3. Isolamento multi-tenant

- **Decision**: (1) `ITenantContext` scoped, preenchido a partir da claim `establishment_id`.
  (2) Toda entidade de negócio implementa `ITenantOwned { Guid EstablishmentId }` e recebe um
  **global query filter** do EF Core: `e.EstablishmentId == tenant.EstablishmentId`. Sem tenant
  (anônimo), o filtro não retorna nada.
  (3) No `SaveChanges`, um interceptor preenche `EstablishmentId` nas entidades novas e lança erro
  se alguma entidade modificada ou removida pertencer a outro tenant.
  (4) Os endpoints públicos (cardápio por slug, pedido por token) usam `IgnoreQueryFilters()` de
  forma explícita e restringem a consulta pelo estabelecimento resolvido no slug ou pelo token.
  (5) Recurso de outro tenant responde **404** (não revela que existe). Papel errado responde **403**.
- **Rationale**: é a defesa em profundidade que a constituição III pede. O filtro é aplicado no
  backend e o cliente nunca informa o tenant.
- **Alternatives**: schema por tenant (complexo demais para o MVP); filtrar manualmente em cada
  query (fácil de esquecer).

## R4. Armazenamento de fotos

- **Decision**: `IImageStorage` (Application), implementado por `LocalDiskImageStorage`
  (Infrastructure), que grava em `Storage:ImagesPath` (padrão `./data/uploads`) no caminho
  `{establishmentId}/{guid}.{ext}`. A API serve os arquivos como estáticos em `/media/**`, por
  `UseStaticFiles` com `PhysicalFileProvider`.
  O upload é `PUT /api/menu-items/{id}/photo` (multipart, campo `file`).
  Validações:
  - tipos aceitos: JPEG, PNG e WebP, checados pelos *magic bytes* e não só pelo `Content-Type`;
  - tamanho máximo de 5 MB (o limite do request fica em 6 MB);
  - tipo inválido → 400 `invalid_image_type`;
  - grande demais → **413 `image_too_large`** nos dois casos: arquivo entre 5 e 6 MB (validação
    da aplicação) e corpo acima de 6 MB (`BadHttpRequestException` 413 do servidor, convertida
    pelo `ProblemDetailsExceptionHandler`). Um 413 em outra rota vira `payload_too_large`.
    *Decisão pós-revisão (Coruja)*: 413 descreve corretamente o "payload grande demais" e deixa
    as duas origens com a mesma resposta, sem depender de onde o limite foi atingido.
  - `/media` endurecido: `ContentTypeProvider` só com `.jpg/.jpeg/.png/.webp`,
    `ServeUnknownFileTypes=false`, sem `UseDirectoryBrowser` e com `X-Content-Type-Options:
    nosniff` e `Cache-Control: public, max-age=31536000, immutable` (nomes são GUIDs).
  A API devolve `photoUrl` relativo (`/media/...`), e o front resolve contra a URL da API.
  Trocar ou remover a foto apaga o arquivo anterior.
- **Rationale**: é o pedido do Maestro. A interface permite trocar por S3/R2 depois sem mexer na
  Application.
- **Alternatives**: guardar a imagem no banco (blob) pesa no Postgres; ir direto para S3 é YAGNI.

## R5. Tempo real (SignalR)

- **Decision**: dois hubs.
  - `/hubs/kitchen` (autenticado, policy `KitchenAccess`): no `OnConnectedAsync` o servidor
    coloca a conexão no grupo `establishment:{id}`, pegando o id da claim (nunca do cliente).
    Eventos: `OrderCreated`, `OrderUpdated` e `OrderPickedUp`.
  - `/hubs/orders` (anônimo): o cliente chama `SubscribeToOrder(publicToken)`. O servidor valida
    o token e coloca a conexão no grupo `order:{publicToken}`. Evento: `OrderUpdated` com o
    snapshot público do pedido.
  O JWT vai em `access_token` na query string (padrão do SignalR para WebSocket).
  Ao (re)conectar, o cliente sempre recarrega o estado via REST (`GET /api/kitchen/orders` ou
  `GET /api/public/orders/{token}`), o que garante não perder pedidos no intervalo.
  A Application publica por `IOrderNotifier`, implementado na Api com `IHubContext`.
  Os eventos estão documentados em `contracts/realtime.md`.
- **Rationale**: grupos por estabelecimento atendem o isolamento, e grupos por token atendem a
  página pública sem expor outros pedidos. Mandar o snapshot inteiro evita aplicar deltas fora de
  ordem.
- **Alternatives**: Server-Sent Events (não está na stack da constituição); polling (não garante
  os 5 s com folga e custa mais).

## R6. Pagamento simulado

- **Decision**: `IPaymentGateway.ChargeAsync(PaymentRequest) → PaymentResult(Approved,
  TransactionId)`. A implementação é `FakePaymentGateway` (Infrastructure), que sempre aprova e
  gera `fake_{guid}` como transação. O checkout ignora quaisquer dados de pagamento enviados.
  Se algum dia o gateway recusar, a API responde 402 `payment_declined` (já está no contrato).
- **Rationale**: atende a constituição V.

## R7. Código de retirada

- **Decision**: 4 caracteres de `23456789ABCDEFGHJKMNPQRSTUVWXYZ` (31 símbolos, sem 0/O/1/I/L),
  gerados com `RandomNumberGenerator`. A unicidade vem de um índice único parcial
  `(EstablishmentId, PickupCode) WHERE Status <> 'picked_up'`. Na violação, gera outro código
  (até 5 tentativas).
  O identificador público do pedido (`PublicToken`) tem 32 bytes aleatórios em base64url
  (43 caracteres) e índice único.
- **Rationale**: o índice parcial implementa exatamente a regra da spec e resiste a concorrência.
  Com 31⁴ ≈ 923 mil combinações, colisão é rara.

## R8. Status do pedido e concorrência

- **Decision**: o `Order` guarda `Status` como coluna, recalculado pelo domínio a partir das
  partes (FR-026), o que permite filtrar por status na lista admin.
  As transições de parte são monotônicas: `received → in_preparation → ready`, e
  `received → ready` também é permitido. Pedir o status atual de novo é **idempotente** (200 sem
  mudança); retroceder dá 409 `invalid_status_transition`. Marcar `picked_up` só vale a partir de
  `ready` (senão 409 `order_not_ready`).
  `Order` usa `xmin` como token de concorrência. Em `DbUpdateConcurrencyException`, o serviço
  recarrega e reaplica a operação uma vez; como as transições são monotônicas, o estado final é
  consistente.

## R9. Representação de dinheiro e enums na API

- **Decision**: valores monetários vão como `integer` (`int64`) com sufixo `Cents`
  (`priceCents`, `totalCents`). Enums vão como strings em snake_case minúsculo: station
  `bar`/`kitchen`, order status `paid`/`in_preparation`/`ready`/`picked_up`, part status
  `received`/`in_preparation`/`ready`, role `owner`/`kitchen`.
  O backend usa `JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)`. A formatação em BRL
  acontece apenas no front (pipe `brl`, `Intl.NumberFormat('pt-BR')`).

## R10. Erros

- **Decision**: os erros seguem RFC 7807 (`application/problem+json`), com a extensão `code`
  (string estável usada pelo front e pelos testes). Validação dá 400 com `errors`
  (campo → mensagens). Exceções de domínio (`DomainException(code)`) viram 400/409 num handler
  global (`IExceptionHandler`). A lista de códigos fica em `contracts/openapi.yaml`
  (`components.schemas.ErrorCode`).

## R11. Organização do código da Application

- **Decision**: serviços de caso de uso por área (`AuthService`, `CategoryService`,
  `MenuItemService`, `PublicMenuService`, `CheckoutService`, `KitchenService`,
  `OrderQueryService`, `StaffService`, `EstablishmentService`), sem MediatR.
  A Application referencia o EF Core e acessa os dados por `IAppDbContext` (os `DbSet`s), sem
  repositórios genéricos. Os DTOs são `record`s na Application, e os controllers só traduzem HTTP.
- **Rationale**: é a "Clean Architecture leve" da constituição com o mínimo de camadas (YAGNI).
- **Alternatives**: MediatR/CQRS e repositórios por agregado (abstração sem necessidade
  concreta).

## R12. Frontend

- **Decision**: estado em signals: `AuthStore` (token em `localStorage`), `CartStore` (por slug, em
  `localStorage`) e stores de página. Um `authInterceptor` funcional injeta o Bearer, e um 401
  faz logout e redireciona para `/login`. Os guards são `ownerGuard`, `kitchenGuard` e
  `guestGuard`.
  O tempo real usa `@microsoft/signalr`, encapsulado em `KitchenRealtimeService` e
  `OrderRealtimeService`. A página do pedido grava o token dos últimos pedidos em `localStorage`
  (FR-031), e o próprio link `/pedido/{publicToken}` já é suficiente.
  Toda chamada HTTP usa o client gerado (`Api.invoke(fn, params)`).
  Mobile-first, com SCSS puro e sem biblioteca de componentes (YAGNI).
- **E2E**: o Playwright roda contra o backend real. O `webServer` sobe a API (`dotnet run`, porta
  5080) e o `ng serve`, e o Postgres vem do docker compose. O CI ganha um job E2E com o serviço
  Postgres.

## R13. Testes de tempo real no backend

- **Decision**: os testes de integração dos hubs usam `Microsoft.AspNetCore.SignalR.Client`
  ligado ao `TestServer` (`HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()`,
  transporte LongPolling), com helper na base de testes (`ConnectKitchenHubAsync(token)`,
  `ConnectOrdersHubAsync()`).

## R14. Rate limiting (pós-revisão Coruja)

- **Decision**: `Microsoft.AspNetCore.RateLimiting` nativo, com janela fixa particionada por IP
  (`RemoteIpAddress`, ou `"unknown"` sem IP). Políticas configuráveis em `RateLimiting:*`:
  - `auth`: login e register-owner, 10 req/min por IP e por endpoint;
  - `checkout`: `POST /api/public/establishments/{slug}/orders`, 20 req/min por IP.
  Na rejeição, a resposta é 429 `application/problem+json` com `code=rate_limited` e
  `Retry-After`.
  Para o `SubscribeToOrder` do `/hubs/orders`, o middleware não alcança chamadas de método de
  hub, então um `IHubFilter` (`HubMethodRateLimitFilter`) aplica um `FixedWindowRateLimiter`
  particionado por IP (`RateLimiting:OrderSubscribe`, 30/min) e lança
  `HubException("rate_limited")`.
  No ambiente `Testing`, a factory configura limites altos, e os testes de limite sobem um host
  com limites baixos (`WithWebHostBuilder`).
- **Rationale**: protege contra força bruta no login, criação em massa de contas e de pedidos
  anônimos, e varredura de tokens via hub.
- **Alternatives**: proxy/WAF (fora do escopo de infra do MVP); limite por usuário (os alvos são
  anônimos). Atrás de proxy reverso, será preciso `ForwardedHeaders`, registrado como pendência
  de deploy.

## R15. Headers de segurança do front (pós-revisão Coruja)

- **Decision**: a imagem de produção do front é `nginx:alpine` servindo o `dist`, com template
  `frontend/nginx/default.conf.template` (`envsubst` de `API_UPSTREAM`, ver R18). Headers:
  - `Content-Security-Policy`: `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'`;
  - `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
    `X-Frame-Options: DENY` e `Permissions-Policy: camera=(), microphone=(), geolocation=()`.
  O build Angular usa `security.autoCsp` (hash dos scripts inline), para o `script-src` dispensar
  `'unsafe-inline'`. HSTS fica na borda/TLS, fora do container.
- **Rationale**: defesa em profundidade contra XSS e clickjacking no cardápio público e no admin.

## R16. Corpo de 401/403 (decisão pós-Foundational, apontada pelo Forja)

- **Decision**: 401 e 403 sempre respondem `application/problem+json` (`ApiProblem`), com os novos
  códigos `unauthorized` e `forbidden` no `ErrorCode`.
  - 401 via `JwtBearerEvents.OnChallenge`: `HandleResponse()`, escreve o problema e mantém
    `WWW-Authenticate: Bearer` (com `error="invalid_token"` quando houver falha de token).
  - 403 via `JwtBearerEvents.OnForbidden`.
  - O login com credenciais erradas continua 401 `invalid_credentials`.
- **Rationale**: o contrato já declara `ProblemDetails` com `code` obrigatório em todo 401/403. O
  front trata todo erro pelo `code`, e um corpo vazio quebraria a regra e o comparador de schema.
- **Alternatives**: tirar o corpo dos 401/403 do contrato (o front perderia a uniformidade);
  reaproveitar `not_found` (semanticamente errado).

## R17. Violação de unicidade concorrente (decisão pós-Foundational, apontada pelo Forja)

- **Decision**: `AppDbContext.SaveChangesAsync` captura `DbUpdateException` cuja causa é
  `PostgresException` com `SqlState = 23505` e relança
  `UniqueConstraintViolationException(constraintName)`, definida em
  `EasyMenu.Application/Common`, para a Application não depender do Npgsql.
  - Mapeamento para 409, por constante de nome de índice em
    `EasyMenu.Application/Common/UniqueConstraints.cs`:
    - `ix_users_normalized_email` → `ConflictException(email_taken)`;
    - `ix_establishments_slug` → `ConflictException(slug_taken)`.
  - Onde mapear: nos serviços que gravam (`AuthService`, `StaffService`) ou de forma central
    no `ProblemDetailsExceptionHandler`. Escolha central: o handler mapeia as constraints
    conhecidas; constraint desconhecida continua 500, porque indica bug.
  - Índices de pedido (`ix_orders_public_token`, `ix_orders_establishment_id_pickup_code`) **não**
    viram 409: o `CheckoutService` captura a exceção com esses nomes e gera novo código/token
    (até 5 tentativas).
- **Rationale**: a verificação prévia (`AnyAsync`) tem janela de corrida. O índice único é a
  garantia real, e a resposta precisa ser a mesma do caminho comum.

## R18. URL da API em produção (decisão pós-revisão Coruja)

- **Decision**: **mesma origem via proxy reverso no nginx do front**. O
  `frontend/nginx/default.conf.template` faz `proxy_pass` de `/api/`, `/hubs/` e `/media/` para
  `${API_UPSTREAM}` (variável obrigatória do container, ex.: `http://api:8080`).
  - `/hubs/` repassa `Upgrade`/`Connection` para WebSocket e usa `proxy_read_timeout 1h`.
  - Todos os `location` de proxy enviam `X-Forwarded-For` (`$proxy_add_x_forwarded_for`) e
    `X-Forwarded-Proto`.
  - `environment.ts` (produção) continua com `apiUrl: ''`, o que torna verdadeiro o comentário
    "mesma origem".
  - A CSP fica só com `'self'` em `connect-src` e `img-src` (sem `API_ORIGIN`/`API_WS_ORIGIN`, que
    saem do Dockerfile).
  - O CORS da API deixa de ser necessário em produção; continua só para o `ng serve` em dev.
  - A API **MUST** confiar no nginx via `ForwardedHeaders:KnownProxies` ou `KnownNetworks` (R14,
    T112/T113). Sem isso, todos os clientes dividiriam a partição de rate limit do IP do nginx.
- **Rationale**:
  - CSP mínima;
  - sem CORS nem preflight;
  - WebSocket e `/media` na mesma origem (o `photoUrl` relativo funciona sem prefixo);
  - um único host público.
- **Alternatives**: `apiUrl` configurável em runtime (um `config.json` carregado no bootstrap,
  com CORS e CSP listando a origem da API): mais peças móveis e CSP mais larga, sem benefício no
  MVP. `apiUrl` fixado no build obrigaria a gerar uma imagem por ambiente.

## R19. Position com criação concorrente (revisão US1): risco aceito

- **Situação**: criar categoria ou item calcula `Position = max + 1` sem lock. Duas criações
  simultâneas no mesmo escopo podem gravar a mesma posição.
- **Decision**: **risco aceito no MVP**, sem lock, índice único ou retry.
- **Rationale**:
  - quem cria é um único Dono, pela UI, uma ação por vez, então a concorrência real é rara;
  - o efeito é só cosmético: dois itens empatados, com a ordem relativa entre eles indefinida;
  - não há perda de dados nem quebra de invariante de dinheiro ou tenant;
  - o Dono corrige reordenando, porque o `PUT …/order` reescreve todas as posições
    (auto-reparo);
  - um índice único em `(establishment_id, [category_id,] position)` tornaria a reordenação mais
    cara (posições temporárias ou constraint `DEFERRABLE`) e exigiria retry na criação.
- **Revisitar quando**: houver múltiplos editores por estabelecimento ou importação em lote. A
  solução seria `pg_advisory_xact_lock` por escopo na criação, ou uma constraint única
  `DEFERRABLE INITIALLY DEFERRED`.
