---

description: "Tasks do MVP EasyMenu: cardápio digital e pedidos"
---

# Tasks: EasyMenu MVP — Cardápio Digital e Pedidos

**Input**: Documentos de design em `/specs/001-mvp-cardapio-pedidos/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/README.md](contracts/README.md) →
`/contracts/openapi.yaml` e `/contracts/realtime.md`.

**Tests**: OBRIGATÓRIOS (constituição I, TDD inegociável). Toda tarefa `[IMPL-*]` depende de uma
tarefa `[TEST-*]` anterior.

## Formato: `- [ ] T### [P?] [US?] [PAPEL] Descrição (depende de: …)`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente).
- **[US1..US5]**: user story da spec. Setup, Foundational e Polish não têm story.
- **Papel**:
  - `[TEST-BACK]`: Testador Back. Escreve os testes e faz commit `test:` com a suíte **vermelha**.
  - `[IMPL-BACK]`: Dev Back. Deixa os testes verdes sem editar testes; commit `feat:`/`refactor:`.
  - `[TEST-FRONT]`: Testador Front. Escreve specs Vitest/Playwright vermelhas; commit `test:`.
  - `[IMPL-FRONT]`: Dev Front. Deixa os testes verdes; commit `feat:`.
- **(depende de: …)**: dependências explícitas. Sem a anotação, a tarefa depende apenas do fim
  da fase anterior.

## Regras para todos os agentes

- Rotas, nomes de parâmetro de rota, schemas e códigos de erro seguem **exatamente**
  `/contracts/openapi.yaml`. Precisa mudar o contrato? Peça ao Arquiteto, nunca edite.
- `x-pending` (constituição 1.0.1): as operações ainda não implementadas estão marcadas com
  `x-pending: true` em `/contracts/openapi.yaml`. A **única** edição de contrato permitida ao Dev
  Back é remover essa marca das operações que a tarefa `[IMPL-BACK]` implementa, no mesmo commit.
  O `OpenApiContractTests` compara rotas, status codes e schemas e acusa operação implementada
  ainda marcada ou operação não marcada sem implementação.
- Testes de integração herdam de `IntegrationTestBase` e montam cenários pelo `ApiDriver`
  (`backend/tests/EasyMenu.IntegrationTests/Infrastructure/ApiDriver.cs`). Cada tarefa de teste
  amplia o `ApiDriver` com os helpers de que precisa.
- Todo endpoint autenticado tem teste de **isolamento** (token do estabelecimento B em recurso
  de A → 404, ou ausência na listagem) e de **papel** (403 quando aplicável). Essas tarefas
  aparecem marcadas com "isolamento".
- Front: toda chamada HTTP usa o client gerado (`Api.invoke(fn, params)` de
  `src/app/api/generated`). Nos testes, use `provideHttpClientTesting()` +
  `HttpTestingController`. Os helpers de E2E em `frontend/e2e/support/` podem chamar a API
  diretamente (`request` do Playwright) para preparar dados; a regra do client gerado vale para o
  código da aplicação.
- Textos da UI em pt-BR. Dinheiro sempre em centavos (`number` inteiro) e formatado pelo
  `BrlPipe`.

---

## Phase 1: Setup (infraestrutura compartilhada)

**Purpose**: pacotes e configuração, sem lógica de negócio (não exigem teste).

- [X] T001 [P] [IMPL-BACK] Adicionar os pacotes: `Microsoft.EntityFrameworkCore` 10.0.x em `backend/src/EasyMenu.Application/EasyMenu.Application.csproj`; `EFCore.NamingConventions` 10.0.1 e `Microsoft.Extensions.Identity.Core` 10.0.x em `backend/src/EasyMenu.Infrastructure/EasyMenu.Infrastructure.csproj`; `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.x em `backend/src/EasyMenu.Api/EasyMenu.Api.csproj`. Criar o tool manifest `backend/.config/dotnet-tools.json` com `dotnet-ef` 10.0.x. `dotnet build` verde.
- [X] T002 [P] [IMPL-BACK] Configuração em `backend/src/EasyMenu.Api/appsettings.json` e `appsettings.Development.json`:
  - `Jwt` (`Issuer`, `Audience`, `SigningKey` ≥ 32 bytes, com valor apenas de dev em Development, e `ExpiresMinutes: 480`);
  - `Storage:ImagesPath` (`data/uploads`) e `Storage:MaxImageBytes` (5242880).
  Adicionar `backend/src/EasyMenu.Api/data/` ao `.gitignore` da raiz.
- [X] T003 [P] [TEST-BACK] Adicionar `Microsoft.AspNetCore.SignalR.Client` 10.0.x e `Microsoft.Extensions.TimeProvider.Testing` 10.0.x em `backend/tests/EasyMenu.IntegrationTests/EasyMenu.IntegrationTests.csproj`, e `Microsoft.Extensions.TimeProvider.Testing` em `backend/tests/EasyMenu.UnitTests/EasyMenu.UnitTests.csproj`. `dotnet test` verde.
- [X] T004 [P] [IMPL-FRONT] Estrutura e rotas do front:
  - instalar `@microsoft/signalr`;
  - mover as páginas-esqueleto de `frontend/src/app/pages/*` para `frontend/src/app/features/{public-menu,order-tracking,admin,kitchen,auth}/`, com `login.page.ts` em `features/auth`;
  - criar o esqueleto `features/auth/register.page.ts` e as rotas `/cadastro`, `/admin/cardapio`, `/admin/equipe`, `/admin/pedidos`, `/admin/pedidos/:orderId` e `/admin/estabelecimento` (componentes vazios) em `frontend/src/app/app.routes.ts`, com `/pedido/:publicToken` no lugar de `/pedido/:id`;
  - atualizar `frontend/src/app/app.spec.ts` apenas na lista de rotas.

  `npm test` e `npm run build` verdes.

**Checkpoint**: builds e suítes existentes verdes.

---

## Phase 2: Foundational (pré-requisitos bloqueantes)

**Purpose**: entidades base, DbContext/migrations, auth JWT, tenant filter, tratamento de erros,
infraestrutura de testes e núcleo do front. ⚠️ Nenhuma story começa antes deste checkpoint.

### Backend — testes (vermelhos)

- [ ] T005 [P] [TEST-BACK] Testes de unidade do domínio base:
  - `backend/tests/EasyMenu.UnitTests/Domain/Establishments/SlugTests.cs`: válidos `abc`, `bar-do-ze`, `a1b`; inválidos `ab`, `-abc`, `abc-`, `Bar`, `bár`, `a_b`, 51 caracteres; normaliza trim e minúsculas → `invalid_slug`.
  - `EstablishmentTests.cs`: nome 2–100 caracteres com trim; `Rename`; o slug não tem setter público.
  - `backend/tests/EasyMenu.UnitTests/Domain/Users/UserTests.cs`: normaliza o e-mail; `Deactivate` gera um novo `SecurityStamp` e é idempotente; `Deactivate` em Owner lança `cannot_deactivate_owner`.
- [ ] T006 [TEST-BACK] Infraestrutura de testes de integração (depende de: T003):
  - Em `backend/tests/EasyMenu.IntegrationTests/Infrastructure/EasyMenuApiFactory.cs`: configurar `Jwt:*` de teste, `Storage:ImagesPath` num diretório temporário por execução (apagado no dispose) e um `FakeTimeProvider` substituível.
  - Criar `ApiDriver.cs` com:
    - `RegisterOwnerAsync(slug?, email?)` → `(token, userId, establishmentId)` via `POST /api/auth/register-owner`;
    - `LoginAsync`;
    - `CreateClient(token)` (HttpClient com Bearer);
    - `CreateKitchenUserAsync(establishmentId)`, que insere o usuário direto no banco com `IPasswordHasher` e devolve as credenciais (isso desacopla as stories da US5);
    - `ReadProblemAsync(response)` → `(status, code, errors)`.
- [ ] T007 [TEST-BACK] Testes de integração de auth em `backend/tests/EasyMenu.IntegrationTests/Auth/RegisterOwnerTests.cs`, `LoginTests.cs` e `CurrentUserTests.cs` (depende de: T006):
  - **Cadastro**: 201 com `AuthResponse` cujo token funciona em `GET /api/auth/me`; 409 `slug_taken`; 409 `email_taken`, inclusive com e-mail em outra caixa; 400 para slug inválido, senha com menos de 8 caracteres ou e-mail inválido.
  - **Login**: 200 com `role=owner`; senha errada e e-mail inexistente dão **o mesmo** 401 `invalid_credentials`.
  - **Me**: 401 sem token; 200 com o estabelecimento.
  - Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): `POST /api/auth/register-owner`, `POST /api/auth/login` e `GET /api/auth/me`.
- [ ] T008 [TEST-BACK] Testes do pipeline de token em `backend/tests/EasyMenu.IntegrationTests/Auth/TokenValidationTests.cs` (depende de: T006). Todos os casos abaixo dão 401 em `GET /api/auth/me`:
  - token adulterado;
  - token expirado (avançar o `FakeTimeProvider`);
  - token de usuário cujo `SecurityStamp` mudou no banco;
  - token de usuário com `IsActive=false` no banco.
- [ ] T009 [P] [TEST-BACK] Testes do tratamento de erros em `backend/tests/EasyMenu.IntegrationTests/Infrastructure/ErrorHandlingTests.cs` (depende de: T006). Usar um controller só de teste, registrado via `ConfigureTestServices`/`ApplicationParts`. Verificar que:
  - `DomainException("x")` vira `application/problem+json` com `code`;
  - um model inválido vira 400 `validation_failed` com `errors` em camelCase;
  - `NotFoundException` vira 404 `not_found`;
  - enums são serializados em snake_case e campos `null` são omitidos.

### Backend — implementação

- [ ] T010 [IMPL-BACK] Domínio base em `backend/src/EasyMenu.Domain/` (depende de: T005). Criar `Common/DomainException.cs` (com `Code`), `Common/ITenantOwned.cs`, `Establishments/Establishment.cs`, `Establishments/Slug.cs`, `Users/User.cs` e `Users/Role.cs`, conforme o data-model. T005 verde.
- [ ] T011 [IMPL-BACK] Abstrações e erros da Application (depende de: T010):
  - `backend/src/EasyMenu.Application/Abstractions/`: `IAppDbContext`, `ITenantContext`, `IPasswordHasher`, `ITokenService`;
  - `Common/`: `ErrorCodes`, `NotFoundException`, `ConflictException`, `ValidationException`;
  - `DependencyInjection.cs` (`AddApplication`).
- [ ] T012 [IMPL-BACK] Persistência e tenant (depende de: T011):
  - `backend/src/EasyMenu.Infrastructure/Persistence/AppDbContext.cs` implementa `IAppDbContext`, com `UseSnakeCaseNamingConvention`, conversão de enums para snake_case e **global query filter** aplicado em loop a todo `ITenantOwned`, usando `ITenantContext`;
  - `Configurations/EstablishmentConfiguration.cs` e `UserConfiguration.cs`, com os índices do data-model;
  - `TenantSaveChangesInterceptor.cs`;
  - migration `Initial` em `Persistence/Migrations/`.
- [ ] T013 [IMPL-BACK] JWT e tenant HTTP (depende de: T012):
  - `backend/src/EasyMenu.Infrastructure/Auth/JwtTokenService.cs`, `JwtOptions.cs` e `IdentityPasswordHasher.cs`;
  - `backend/src/EasyMenu.Api/Auth/`:
    - `SecurityStampValidator.cs` (`OnTokenValidated`: usuário ativo e stamp igual; senão `Fail`);
    - `AuthPolicies.cs` (`OwnerOnly`, `KitchenAccess`);
    - `HttpTenantContext.cs`;
  - registro de `AddAuthentication().AddJwtBearer` em `Program.cs`, lendo `access_token` da query apenas para `/hubs/*`.
- [ ] T014 [IMPL-BACK] Infra transversal da Api (depende de: T012):
  - `backend/src/EasyMenu.Api/ErrorHandling/ProblemDetailsExceptionHandler.cs`, com a tabela código → status:
    - 409: `*_taken`, `category_not_empty`, `items_unavailable`, `invalid_status_transition`, `order_not_ready`;
    - 404: `not_found`, `*_not_found`;
    - 402: `payment_declined`;
    - 400: demais códigos;
  - `InvalidModelStateResponseFactory` → 400 `validation_failed`;
  - opções JSON (`JsonStringEnumConverter(SnakeCaseLower)`, `WhenWritingNull`);
  - `UseStaticFiles` em `/media` a partir de `Storage:ImagesPath`;
  - `Database.Migrate()` na subida apenas em `Development`;
  - `NumberHandling = Strict` no JSON;
  - `HealthResponse.Status` vira o enum `HealthStatus { Healthy, Unhealthy }` (snake_case), o que corrige a divergência de schema do `GET /health` acusada pelo `OpenApiContractTests`;
  - DTO `ApiProblem` (`backend/src/EasyMenu.Api/ErrorHandling/ApiProblem.cs`) idêntico ao schema `ProblemDetails` do contrato, usado em `[ProducesResponseType<ApiProblem>(status, "application/problem+json")]`.

  T009 verde.
- [ ] T015 [IMPL-BACK] `AuthService` (`backend/src/EasyMenu.Application/Auth/`) e `AuthController` (`backend/src/EasyMenu.Api/Controllers/AuthController.cs`), com register-owner, login e me (depende de: T007, T008, T013, T014). T007 e T008 verdes, e `OpenApiContractTests` verde.

### Frontend — testes (vermelhos)

- [ ] T016 [P] [TEST-FRONT] Specs do núcleo (depende de: T004):
  - `frontend/src/app/core/auth/auth.store.spec.ts`:
    - `setSession` grava token e usuário no `localStorage`;
    - `restore()` reidrata, e token expirado (`expiresAt` passado) é tratado como deslogado;
    - `logout()` limpa;
    - `isOwner`/`isKitchen` são computed.
  - `auth.interceptor.spec.ts`:
    - adiciona `Authorization: Bearer` só para URLs da API (`environment.apiUrl`);
    - resposta 401 → `logout()` + navegação para `/login`.
  - `guards.spec.ts`:
    - `ownerGuard` só deixa passar owner;
    - `kitchenGuard` deixa passar owner e kitchen;
    - `guestGuard` redireciona owner para `/admin` e kitchen para `/cozinha`;
    - deslogado vai para `/login`.
  - `frontend/src/app/shared/brl.pipe.spec.ts`: 1990 → `R$ 19,90`, 0 → `R$ 0,00`, 1000000 → `R$ 10.000,00`.
  - `media-url.pipe.spec.ts`: URL relativa prefixada com `apiUrl`; `undefined` → `/images/placeholder-item.svg`.
  - `frontend/src/app/core/errors/problem.spec.ts`: `problemCode(HttpErrorResponse)` extrai `code`; `problemMessage(code)` devolve a mensagem pt-BR de cada `ErrorCode` do contrato.
- [ ] T017 [TEST-FRONT] Infra de E2E (depende de: T004):
  - `frontend/playwright.config.ts` com `webServer` duplo: a API (`dotnet run --project ../backend/src/EasyMenu.Api --launch-profile http`, url `http://localhost:5080/health`) e o `ng serve`;
  - `frontend/e2e/support/api.ts` com `registerOwner`, `createCategory`, `createItem`, `placeOrder` e `login` via `request` do Playwright, gerando slug/e-mail únicos por teste;
  - job `e2e` em `.github/workflows/ci.yml` com service `postgres:17` + .NET + Node;
  - o smoke `frontend/e2e/smoke.spec.ts` continua verde.

### Frontend — implementação

- [ ] T018 [IMPL-FRONT] Núcleo do front (depende de: T016; usa apenas o client gerado, sem esperar o back):
  - `frontend/src/app/core/auth/{auth.store.ts,auth.interceptor.ts,guards.ts}`;
  - `frontend/src/app/shared/{brl.pipe.ts,media-url.pipe.ts}` e `frontend/public/images/placeholder-item.svg`;
  - `frontend/src/app/core/errors/problem.ts`;
  - registrar o interceptor (`withInterceptors`) em `app.config.ts` e os guards em `app.routes.ts`.

  T016 verde.

**Checkpoint**: cadastro, login e me funcionam via API; front com sessão, guards e pipes.
`dotnet test`, `npm test` e `npm run build` verdes.

---

## Phase 3: User Story 1 — Dono cadastra estabelecimento e monta o cardápio (P1) 🎯 MVP

**Goal**: o Dono cria conta e estabelecimento, gerencia categorias e itens (com foto, promoção,
esgotado e setor) e reordena tudo.

**Independent Test**: criar um Dono, 2 categorias e 3 itens (um com promoção, um esgotado),
reordenar as categorias e verificar ordem e dados no admin (E2E T042).

### Fatia 1a: Estabelecimento (back)

- [ ] T019 [P] [US1] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Establishment/EstablishmentEndpointsTests.cs`:
  - `GET /api/establishment` devolve o próprio estabelecimento;
  - `PUT` renomeia, e o slug não muda nem com `slug` no body;
  - nome inválido dá 400;
  - kitchen dá 403 e anônimo dá 401;
  - isolamento: o token de B nunca vê dados de A.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): `GET /api/establishment` e `PUT /api/establishment`.
- [ ] T020 [US1] [IMPL-BACK] `backend/src/EasyMenu.Application/Establishments/EstablishmentService.cs` e `backend/src/EasyMenu.Api/Controllers/EstablishmentController.cs` (policy `OwnerOnly`) (depende de: T019). T019 verde.

### Fatia 1b: Categorias (back)

- [ ] T021 [P] [US1] [TEST-BACK] `backend/tests/EasyMenu.UnitTests/Domain/Menu/CategoryTests.cs`: nome 1–60 caracteres com trim; `Rename`; `MoveTo(position)`.
- [ ] T022 [P] [US1] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Menu/CategoriesEndpointsTests.cs`:
  - criar dá 201 e a nova categoria vai para o fim (`position` = n);
  - a listagem respeita a ordem;
  - renomear;
  - `PUT /api/categories/order`: reordena com a lista completa; lista incompleta, duplicada ou com id de outro tenant dá 400 `invalid_order_list`;
  - remover categoria vazia dá 204;
  - kitchen dá 403 e anônimo dá 401;
  - **isolamento**: B renomear ou apagar categoria de A dá 404, e a listagem de B não traz as de A.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): `GET /api/categories`, `POST /api/categories`, `PUT /api/categories/order`, `PUT /api/categories/{id}` e `DELETE /api/categories/{id}`. Ampliar o `ApiDriver` com `CreateCategoryAsync`.
- [ ] T023 [US1] [IMPL-BACK] Categorias de ponta a ponta (depende de: T021, T022). T021 e T022 verdes.
  - `backend/src/EasyMenu.Domain/Menu/Category.cs`;
  - `backend/src/EasyMenu.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`;
  - migration `AddCategories`;
  - `backend/src/EasyMenu.Application/Menu/CategoryService.cs`;
  - `backend/src/EasyMenu.Api/Controllers/CategoriesController.cs`, com rotas por id usando `{id:guid}` para não colidir com `/api/categories/order`.

### Fatia 1c: Itens (back)

- [ ] T024 [P] [US1] [TEST-BACK] `backend/tests/EasyMenu.UnitTests/Domain/Menu/MenuItemTests.cs`:
  - preço ≤ 0 ou acima do teto dá `invalid_price`;
  - promoção = preço, > preço, 0 ou negativa dá `invalid_promotional_price`;
  - promoção null é válida;
  - `EffectivePriceCents`;
  - `Update` reaplica os invariantes;
  - `SetAvailability`;
  - nome 1–100 e descrição ≤ 500 caracteres.
- [ ] T025 [P] [US1] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Menu/MenuItemsEndpointsTests.cs`:
  - criar dá 201, com `effectivePriceCents` e `position` no fim da categoria;
  - `GET` por id;
  - listagem ordenada por categoria e posição, com filtro `categoryId`;
  - `PUT` atualiza, e trocar a categoria move o item para o fim da nova;
  - promoção inválida dá 400 `invalid_promotional_price`;
  - `PUT /availability` alterna;
  - `DELETE` dá 204;
  - `DELETE /api/categories/{id}` com itens dá 409 `category_not_empty`;
  - `PUT /api/categories/{id}/items/order` reordena, e lista inválida dá 400 `invalid_order_list`;
  - categoria de outro tenant no create dá 404 `category_not_found`;
  - kitchen dá 403;
  - **isolamento**: todas as operações por id com token de B em item de A dão 404.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): `GET/POST /api/menu-items`, `GET/PUT/DELETE /api/menu-items/{id}`, `PUT /api/menu-items/{id}/availability` e `PUT /api/categories/{id}/items/order`. Ampliar o `ApiDriver` com `CreateMenuItemAsync` e `CreateMenuAsync` (cardápio padrão: categorias Drinks e Porções; itens Caipirinha 2500/1990 bar, Chopp 1200 bar esgotado, Batata 3000 kitchen).
- [ ] T026 [US1] [IMPL-BACK] Itens de ponta a ponta (depende de: T023, T024, T025). T024 e T025 verdes.
  - `backend/src/EasyMenu.Domain/Menu/{MenuItem.cs,Station.cs}`;
  - `Configurations/MenuItemConfiguration.cs`;
  - migration `AddMenuItems`;
  - `backend/src/EasyMenu.Application/Menu/MenuItemService.cs`;
  - `backend/src/EasyMenu.Api/Controllers/MenuItemsController.cs`;
  - endpoint de reordenação de itens em `CategoriesController`.

### Fatia 1d: Foto (back)

- [ ] T027 [P] [US1] [TEST-BACK] `backend/tests/EasyMenu.UnitTests/Application/Menu/ImageFormatTests.cs`: detecta JPEG (`FF D8 FF`), PNG (`89 50 4E 47 0D 0A 1A 0A`) e WebP (`RIFF....WEBP`) pelos bytes; GIF, texto e arquivo vazio dão `null`.
- [ ] T028 [P] [US1] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Menu/MenuItemPhotoTests.cs` (depende de: T025):
  - upload de JPEG dá 200 com `photoUrl` `/media/{establishmentId}/…jpg`, e `GET photoUrl` dá 200 `image/jpeg`;
  - um novo upload substitui a foto e o arquivo antigo some do disco;
  - arquivo > 5 MB dá 400 `image_too_large`;
  - texto com `Content-Type: image/png` dá 400 `invalid_image_type`;
  - sem `file` dá 400;
  - `DELETE /photo` dá 204, remove `photoUrl` e apaga o arquivo;
  - `DELETE` do item apaga o arquivo;
  - **isolamento**: upload no item de A com token de B dá 404.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): `PUT/DELETE /api/menu-items/{id}/photo`.
- [ ] T029 [US1] [IMPL-BACK] Fotos (depende de: T026, T027, T028). T027 e T028 verdes.
  - `backend/src/EasyMenu.Application/Abstractions/IImageStorage.cs`;
  - `backend/src/EasyMenu.Application/Menu/ImageFormat.cs`;
  - `backend/src/EasyMenu.Infrastructure/Storage/{LocalDiskImageStorage.cs,StorageOptions.cs}`;
  - endpoints de foto em `MenuItemsController` com `[RequestSizeLimit(6MB)]`;
  - remoção do arquivo em `MenuItemService.Delete`.

### Fatia 1e: Auth e admin (front)

- [ ] T030 [P] [US1] [TEST-FRONT] `frontend/src/app/features/auth/login.page.spec.ts`:
  - envia pela função gerada `login`;
  - chama `AuthStore.setSession`;
  - redireciona owner para `/admin/cardapio` e kitchen para `/cozinha`;
  - 401 mostra "E-mail ou senha inválidos", sem indicar o campo;
  - campos obrigatórios;
  - botão desabilitado durante o envio.
- [ ] T031 [P] [US1] [TEST-FRONT] `frontend/src/app/features/auth/register.page.spec.ts`:
  - validação de e-mail, senha ≥ 8, nome do estabelecimento 2–100 e slug com o pattern do contrato;
  - sugere o slug a partir do nome (minúsculas, sem acento, hífens) enquanto o usuário não editar o slug;
  - 409 `slug_taken` mostra "Este endereço já está em uso" no campo slug;
  - 409 `email_taken` mostra a mensagem no campo e-mail;
  - sucesso faz `setSession` e navega para `/admin/cardapio`.
- [ ] T032 [US1] [IMPL-FRONT] `frontend/src/app/features/auth/{login.page.ts,register.page.ts}` (+ html/scss) (depende de: T030, T031). T030 e T031 verdes.
- [ ] T033 [P] [US1] [TEST-FRONT] Shell do admin e configurações:
  - `frontend/src/app/features/admin/admin-shell.component.spec.ts`: navegação Cardápio/Equipe/Pedidos/Estabelecimento, botão Sair (`logout` → `/login`) e link "Painel da cozinha";
  - `frontend/src/app/features/admin/settings/settings.page.spec.ts`: mostra o slug só leitura e o link público `/m/{slug}`, renomeia via `updateEstablishment` e mostra os erros de validação.
- [ ] T034 [US1] [IMPL-FRONT] `frontend/src/app/features/admin/admin-shell.component.ts` (rota pai `/admin` com `<router-outlet>`) e `frontend/src/app/features/admin/settings/settings.page.ts` (depende de: T033). T033 verde.

### Fatia 1f: Cardápio admin (front)

- [ ] T035 [P] [US1] [TEST-FRONT] `frontend/src/app/features/admin/menu/categories-panel.component.spec.ts`:
  - lista na ordem de `position`;
  - cria, com o campo vazio desabilitando o envio;
  - renomeia inline;
  - "subir/descer" chama `reorderCategories` com a lista **completa** de ids na nova ordem;
  - remove com confirmação, e 409 `category_not_empty` mostra "Remova ou mova os itens antes de excluir a categoria".
- [ ] T036 [US1] [IMPL-FRONT] `frontend/src/app/features/admin/menu/{menu.page.ts,categories-panel.component.ts}` (depende de: T035). T035 verde.
- [ ] T037 [P] [US1] [TEST-FRONT] `frontend/src/app/features/admin/menu/price-input.spec.ts`: `parseBrlToCents('19,90')` = 1990, `'25'` = 2500, `'1.234,5'` = 123450; inválido dá `null`; `formatCentsInput(1990)` = `'19,90'`.
- [ ] T038 [P] [US1] [TEST-FRONT] `frontend/src/app/features/admin/menu/menu-item-form.component.spec.ts`:
  - campos obrigatórios (nome, preço, setor, categoria);
  - promoção ≥ preço ou ≤ 0 mostra "O preço promocional deve ser maior que zero e menor que o preço" e bloqueia o envio;
  - envia `priceCents`/`promotionalPriceCents` inteiros;
  - create (`createMenuItem`) e edit (`updateMenuItem`);
  - com arquivo selecionado, chama `uploadMenuItemPhoto` depois de salvar;
  - 400 `invalid_image_type`/`image_too_large` mostra a mensagem e mantém o item salvo;
  - "remover foto" chama `deleteMenuItemPhoto`.
- [ ] T039 [P] [US1] [TEST-FRONT] `frontend/src/app/features/admin/menu/menu-items-list.component.spec.ts`:
  - itens da categoria na ordem;
  - badge "Esgotado";
  - toggle chama `setMenuItemAvailability`;
  - promoção exibida com o preço original riscado;
  - excluir com confirmação chama `deleteMenuItem`;
  - subir/descer chama `reorderCategoryItems` com a lista completa.
- [ ] T040 [US1] [IMPL-FRONT] `frontend/src/app/features/admin/menu/{price-input.ts,menu-item-form.component.ts,menu-items-list.component.ts}` e integração em `menu.page.ts` (depende de: T036, T037, T038, T039). T037–T039 verdes.

### Fatia 1g: E2E

- [ ] T041 [US1] [TEST-FRONT] Fixture de imagem `frontend/e2e/fixtures/item.jpg` (JPEG pequeno) e `frontend/e2e/fixtures/not-image.txt` (depende de: T017).
- [ ] T042 [US1] [TEST-FRONT] `frontend/e2e/us1-owner-menu.spec.ts` (depende de: T041). Fluxo:
  - cadastro pela UI;
  - criar "Drinks" e "Porções" e mover Porções para cima;
  - criar Caipirinha 25,00/19,90 Bar com foto;
  - tentar promoção 30,00 e ver a mensagem;
  - marcar Chopp como esgotado;
  - recarregar e conferir a ordem e os dados;
  - logout e login de novo.

  Commit vermelho; fica verde quando T029 e T040 estiverem prontos.

**Checkpoint US1**: T042 verde, e a fatia back inteira verde.

---

## Phase 4: User Story 2 — Consumidor vê o cardápio e compra (P2)

**Goal**: cardápio público `/m/{slug}`, carrinho, compra com pagamento simulado e página do
pedido com código de retirada.

**Independent Test**: com cardápio cadastrado, comprar 2 itens (um em promoção) sem nome, ver o
código e conferir o total calculado pelo servidor (E2E T057).

### Fatia 2a: Cardápio público (back)

- [ ] T043 [P] [US2] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/PublicMenu/PublicMenuEndpointTests.cs` (depende de: T025, para o `CreateMenuAsync`):
  - sem token dá 200;
  - categorias e itens na ordem do Dono;
  - a categoria só com itens esgotados **não** aparece;
  - o item esgotado aparece com `isAvailable=false` numa categoria que tem outros disponíveis;
  - campos de promoção e `effectivePriceCents`;
  - o JSON **não** contém `station`, `position` nem `categoryId` (FR-038);
  - slug inexistente dá 404 `establishment_not_found`;
  - cardápio sem categorias dá `categories: []`;
  - dados de B não aparecem no cardápio de A.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): `GET /api/public/establishments/{slug}/menu`.
- [ ] T044 [US2] [IMPL-BACK] `backend/src/EasyMenu.Application/Menu/PublicMenuService.cs` (com `IgnoreQueryFilters` + filtro por slug) e `backend/src/EasyMenu.Api/Controllers/PublicMenuController.cs` (`[AllowAnonymous]`) (depende de: T026, T043). T043 verde.

### Fatia 2b: Compra (back)

- [ ] T045 [P] [US2] [TEST-BACK] `backend/tests/EasyMenu.UnitTests/Domain/Orders/OrderPlaceTests.cs`:
  - o snapshot de nome, preço efetivo e setor por linha;
  - `SubtotalCents` e `TotalCents` (2×1990 + 1×3000 = 6980);
  - linhas repetidas são agrupadas;
  - quantidade 0 ou 100, ou soma agrupada > 99, dá `invalid_quantity`;
  - lista vazia dá `empty_cart`;
  - uma `OrderPart` `received` por setor presente (só bar → 1 parte);
  - `Status=paid`;
  - nome vazio ou só espaços vira null, e nome > 60 caracteres é recusado.
- [ ] T046 [P] [US2] [TEST-BACK] `backend/tests/EasyMenu.UnitTests/Domain/Orders/PickupCodeTests.cs`: `PickupCode.Generate()` sempre bate com `^[2-9A-HJKMNP-Z]{4}$` (10.000 amostras) e nunca contém 0, O, 1, I ou L. `PublicToken.Generate()` tem 43 caracteres base64url.
- [ ] T047 [US2] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Checkout/PlaceOrderTests.cs` (depende de: T043):
  - 201 com `PublicOrder`: `status=paid`, total calculado com a promoção vigente, `pickupCode` no formato e `publicToken` com 43 caracteres;
  - campos extras no body (`totalCents`, `unitPriceCents`, `payment`) são ignorados;
  - preço alterado ou promoção removida no admin antes da compra: o pedido usa o preço vigente na hora da compra;
  - `customerName` > 60 caracteres dá 400;
  - itens vazios dão 400 `empty_cart`;
  - quantidade fora de 1..99 dá 400;
  - item esgotado dá 409 `items_unavailable`, com `unavailableItems` (id + nome), e **nenhum pedido criado**;
  - item removido dá 409 sem `name`;
  - item de outro estabelecimento dá 409;
  - slug inexistente dá 404;
  - colisão de código: substituir `IPickupCodeGenerator` via `ConfigureTestServices` por um gerador que repete um código já ativo e depois devolve outro; o pedido é criado com o segundo código.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): `POST /api/public/establishments/{slug}/orders`.
- [ ] T048 [US2] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Checkout/PublicOrderEndpointTests.cs` (depende de: T047):
  - `GET /api/public/orders/{publicToken}` devolve itens (nome, preço unitário, quantidade, subtotal), total, status e estabelecimento;
  - **snapshot**: depois de renomear, mudar o preço e apagar o item no admin, o pedido continua igual (SC-006);
  - token inventado dá 404 `order_not_found`;
  - o `pickupCode` usado como token dá 404.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): `GET /api/public/orders/{publicToken}`.
- [ ] T049 [US2] [IMPL-BACK] Pedido e checkout (depende de: T044, T045, T046, T047, T048). T045–T048 verdes.
  - `backend/src/EasyMenu.Domain/Orders/{Order.cs,OrderItem.cs,OrderPart.cs,OrderStatus.cs,PartStatus.cs,PickupCode.cs,PublicToken.cs}`;
  - `Configurations/OrderConfiguration.cs`, com o índice único parcial do código, o índice único do token e `xmin`;
  - migration `AddOrders`;
  - `backend/src/EasyMenu.Application/Abstractions/{IPaymentGateway.cs,IPickupCodeGenerator.cs}`;
  - `backend/src/EasyMenu.Application/Orders/{CheckoutService.cs,RandomPickupCodeGenerator.cs,OrderMappings.cs}` (o retry de até 5 tentativas na violação do índice do código);
  - `backend/src/EasyMenu.Infrastructure/Payments/FakePaymentGateway.cs`;
  - `backend/src/EasyMenu.Api/Controllers/PublicOrdersController.cs` (`[AllowAnonymous]`).

### Fatia 2c: Cardápio público e carrinho (front)

- [ ] T050 [P] [US2] [TEST-FRONT] `frontend/src/app/features/public-menu/cart.store.spec.ts`:
  - `add` com quantidade 1 e incremento;
  - `increment`/`decrement` limitados a 1..99, e `decrement` em 1 mantém 1;
  - `remove`;
  - `lineSubtotal` e `total` em centavos;
  - não adiciona item com `isAvailable=false`;
  - persiste por slug no `localStorage` e isola os slugs;
  - `clear()`;
  - `syncWithMenu(menu)` atualiza preço e nome e marca as linhas indisponíveis.
- [ ] T051 [US2] [IMPL-FRONT] `frontend/src/app/features/public-menu/cart.store.ts` (depende de: T050). T050 verde.
- [ ] T052 [P] [US2] [TEST-FRONT] `frontend/src/app/features/public-menu/public-menu.page.spec.ts`:
  - carrega `getPublicMenu(slug)` pelo input de rota;
  - mostra o nome do estabelecimento e as categorias e itens na ordem;
  - promoção mostra o preço efetivo em destaque e o original em `<s>`;
  - esgotado mostra "Esgotado" com o botão Adicionar desabilitado;
  - sem `photoUrl` mostra o placeholder;
  - 404 mostra "Estabelecimento não encontrado";
  - `categories: []` mostra "Cardápio indisponível no momento";
  - nome e descrição com `<script>` aparecem como texto.
- [ ] T053 [US2] [IMPL-FRONT] `frontend/src/app/features/public-menu/{public-menu.page.ts,menu-item-card.component.ts}` (mobile-first) (depende de: T051, T052). T052 verde.

### Fatia 2d: Checkout e página do pedido (front)

- [ ] T054 [P] [US2] [TEST-FRONT] Carrinho e histórico:
  - `frontend/src/app/features/public-menu/cart-sheet.component.spec.ts`:
    - linhas com subtotal e o total;
    - controles de quantidade;
    - campo nome opcional com `maxlength` 60;
    - "Comprar" desabilitado com o carrinho vazio ou durante o envio;
    - chama `placeOrder` só com `customerName` e `items[{menuItemId, quantity}]`;
    - 201 grava em `OrderHistoryStore`, faz `cart.clear()` e navega para `/pedido/{publicToken}`;
    - 409 `items_unavailable` marca as linhas indicadas e mostra "Itens indisponíveis: …" sem limpar o carrinho.
  - `frontend/src/app/core/storage/order-history.store.spec.ts`:
    - `remember({publicToken, slug, pickupCode, createdAt})` mantém os 10 mais recentes;
    - `lastForSlug(slug)`.
- [ ] T055 [US2] [IMPL-FRONT] `frontend/src/app/features/public-menu/cart-sheet.component.ts` e `frontend/src/app/core/storage/order-history.store.ts` (depende de: T053, T054). T054 verde.
- [ ] T056 [P] [US2] [TEST-FRONT] `frontend/src/app/features/order-tracking/order-tracking.page.spec.ts`, parte estática:
  - carrega `getPublicOrder(publicToken)`;
  - mostra o código de retirada em destaque, os itens com quantidade, preço unitário e subtotal, e o total;
  - status `paid` mostra "Pago / aguardando preparo";
  - 404 mostra "Pedido não encontrado".
- [ ] T057 [US2] [TEST-FRONT] `frontend/e2e/us2-checkout.spec.ts` (depende de: T017). Fluxo:
  - semear o cardápio via `support/api.ts`;
  - abrir `/m/{slug}` anônimo;
  - Chopp não pode ser adicionado;
  - adicionar Caipirinha ×2 e Batata ×1, conferindo o total R$ 69,80;
  - comprar sem nome;
  - a URL vira `/pedido/…` com o código no formato;
  - `/m/inexistente` mostra a mensagem;
  - um item que fica esgotado via API com o carrinho aberto faz a compra ser recusada com o nome do item.
- [ ] T058 [US2] [IMPL-FRONT] `frontend/src/app/features/order-tracking/order-tracking.page.ts`, versão estática (depende de: T056). T056 verde, e T057 verde junto com T049 e T055.

**Checkpoint US2**: T057 verde.

---

## Phase 5: User Story 3 — Cozinha em tempo real (P3)

**Goal**: painel único com seções Bar e Cozinha, atualizado por SignalR; avanço das partes,
status derivado e retirada.

**Independent Test**: E2E T070. Com o painel aberto, uma compra com itens de Bar e de Cozinha
aparece nas duas seções; avança cada parte até Pronto e marca Retirado.

### Fatia 3a: Transições (back)

- [ ] T059 [P] [US3] [TEST-BACK] `backend/tests/EasyMenu.UnitTests/Domain/Orders/OrderStatusTests.cs`:
  - transições permitidas: received→in_preparation, received→ready, in_preparation→ready;
  - repetir o status atual é no-op;
  - retrocesso dá `invalid_status_transition`;
  - `RecalculateStatus` cobre a tabela do data-model (bar ready + kitchen received = `in_preparation`; ambas ready = `ready`);
  - `MarkPickedUp` fora de ready dá `order_not_ready`;
  - `MarkPickedUp` em ready grava `PickedUpAt`, e chamá-lo de novo é idempotente;
  - qualquer mudança de parte após `picked_up` dá `invalid_status_transition`.
- [ ] T060 [US3] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Kitchen/KitchenEndpointsTests.cs` (depende de: T047):
  - `GET /api/kitchen/orders` lista só os pedidos não retirados, do mais antigo ao mais novo, e cada parte só com os itens do seu setor;
  - owner também acessa (FR-033a);
  - anônimo dá 401;
  - `PUT …/parts/bar/status` com `in_preparation` dá 200 com o `KitchenOrder`, e o pedido vira `in_preparation`;
  - repetir dá 200;
  - `ready` → `in_preparation` dá 409 `invalid_status_transition`;
  - parte de setor ausente no pedido dá 404;
  - `PUT …/pickup` antes de ready dá 409 `order_not_ready`;
  - depois de ready dá 200 e o pedido sai da lista;
  - **reuso do código (FR-021)**: depois da retirada, um gerador forçado a repetir o mesmo código cria um novo pedido com 201;
  - **concorrência**: duas requisições `ready` simultâneas na mesma parte dão ambas 200 e o estado final é `ready`;
  - **isolamento**: cozinha de B nas rotas de pedido de A dá 404, e a lista de B não traz pedidos de A.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): as 3 operações `/api/kitchen/...`.
- [ ] T061 [US3] [IMPL-BACK] Transições e painel (depende de: T049, T059, T060). T059 e T060 verdes.
  - transições em `OrderPart.MoveTo`, `Order.RecalculateStatus` e `Order.MarkPickedUp`;
  - `backend/src/EasyMenu.Application/Orders/KitchenService.cs` (retry único em `DbUpdateConcurrencyException`);
  - `backend/src/EasyMenu.Api/Controllers/KitchenController.cs` (policy `KitchenAccess`).

### Fatia 3b: Hub da cozinha (back)

- [ ] T062 [US3] [TEST-BACK] Helpers e testes do hub da cozinha (depende de: T060):
  - helpers `ConnectKitchenHubAsync(token)` (LongPolling + `HttpMessageHandlerFactory` do TestServer) e `WaitForEventAsync<T>(connection, name, timeout 5s)` em `backend/tests/EasyMenu.IntegrationTests/Infrastructure/HubTestHelpers.cs`;
  - `backend/tests/EasyMenu.IntegrationTests/Realtime/KitchenHubTests.cs`:
    - conectar sem token falha (401 no negotiate);
    - kitchen e owner conectam;
    - um checkout gera `OrderCreated` com `KitchenOrder`;
    - mudança de parte gera `OrderUpdated`;
    - retirada gera `OrderPickedUp {id, pickupCode}`;
    - **isolamento**: a conexão de B não recebe nenhum evento de A em 2 s.
- [ ] T063 [US3] [IMPL-BACK] Tempo real da cozinha (depende de: T061, T062). T062 verde.
  - `backend/src/EasyMenu.Application/Abstractions/IOrderNotifier.cs`;
  - `backend/src/EasyMenu.Api/Hubs/KitchenHub.cs` (`[Authorize(Policy=KitchenAccess)]`, grupo `establishment:{id}` vindo da claim);
  - `backend/src/EasyMenu.Api/Realtime/SignalROrderNotifier.cs`;
  - chamadas ao notifier **após o commit** em `CheckoutService` e `KitchenService`;
  - `app.MapHub<KitchenHub>("/hubs/kitchen")`.

### Fatia 3c: Painel (front)

- [ ] T064 [P] [US3] [TEST-FRONT] `frontend/src/app/core/realtime/kitchen-realtime.service.spec.ts`, com fake de `HubConnection` via token `HUB_CONNECTION_FACTORY`:
  - `connect()` usa `/hubs/kitchen` e `accessTokenFactory` do `AuthStore`;
  - registra `OrderCreated`/`OrderUpdated`/`OrderPickedUp` e os expõe como Observable;
  - emite `resync$` no start e em `onreconnected`;
  - `disconnect()` faz stop;
  - quando a conexão fecha com erro, expõe o estado `disconnected`.
- [ ] T065 [US3] [IMPL-FRONT] `frontend/src/app/core/realtime/{hub-connection.factory.ts,kitchen-realtime.service.ts}` (`withAutomaticReconnect`) (depende de: T064). T064 verde.
- [ ] T066 [P] [US3] [TEST-FRONT] `frontend/src/app/features/kitchen/kitchen.page.spec.ts`:
  - no `resync$` chama `listKitchenOrders`;
  - a seção "Bar" mostra só as partes `bar` e a seção "Cozinha" só as `kitchen`, do mais antigo ao mais novo;
  - o cartão mostra o código, o nome do cliente como texto, o horário e os itens × quantidade;
  - parte `received` mostra os botões "Em preparo" e "Pronto", e `in_preparation` só "Pronto";
  - os botões chamam `updateOrderPartStatus`;
  - "Retirado" só aparece com o pedido `ready` e chama `markOrderPickedUp`;
  - os eventos aplicam o upsert ou a remoção sem recarregar;
  - 409 mostra um aviso e chama resync;
  - indicador "Reconectando…" quando `disconnected`.
- [ ] T067 [US3] [IMPL-FRONT] `frontend/src/app/features/kitchen/{kitchen.page.ts,kitchen-order-card.component.ts}` (depende de: T065, T066). T066 verde.
- [ ] T068 [US3] [TEST-FRONT] `frontend/e2e/support/api.ts`: adicionar `loginAs(page, session)`, que grava a sessão no `localStorage` antes da navegação (depende de: T017).
- [ ] T069 [US3] [TEST-FRONT] `frontend/e2e/support/fixtures.ts` (depende de: T068): `test` estendido do Playwright com as fixtures `seeded`, que cadastra o Dono e o cardápio padrão e devolve slug, token e ids, e `ownerPage`, uma página já autenticada como Dono. O painel usa o **Dono** (FR-033a), para a US3 não depender da US5. É usado por T070, T078 e T088.
- [ ] T070 [US3] [TEST-FRONT] `frontend/e2e/us3-kitchen.spec.ts` (depende de: T069). Fluxo:
  - contexto A (Dono em `/cozinha`) e contexto B (consumidor);
  - B compra Caipirinha + Batata;
  - em ≤ 5 s, A vê o pedido em Bar (só Caipirinha) e em Cozinha (só Batata);
  - Bar: Em preparo → Pronto; o pedido ainda não está pronto;
  - Cozinha: Pronto; o pedido mostra Retirado;
  - Retirado; o pedido some.
  - Um pedido só com itens de Bar aparece só em Bar.

**Checkpoint US3**: T070 verde.

---

## Phase 6: User Story 4 — Consumidor acompanha o status (P4)

**Goal**: a página do pedido atualiza ao vivo e avisa quando está pronto. O consumidor volta ao
pedido no mesmo aparelho.

**Independent Test**: E2E T078. As mudanças feitas na cozinha aparecem na página do pedido sem
recarregar, junto com o aviso de retirada.

- [ ] T071 [US4] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Realtime/OrdersHubTests.cs` (depende de: T062), com o helper `ConnectOrdersHubAsync()`:
  - `SubscribeToOrder(tokenValido)` retorna `true` e `SubscribeToOrder("inventado")` retorna `false`;
  - recebe `OrderUpdated` (`PublicOrder`) quando o pedido vai para `in_preparation`, `ready` e `picked_up`;
  - **não** recebe evento quando a segunda parte entra em preparo e o status do pedido não muda;
  - um inscrito no pedido X não recebe eventos de Y;
  - conexão anônima permitida.
- [ ] T072 [US4] [IMPL-BACK] `backend/src/EasyMenu.Api/Hubs/OrdersHub.cs` (`SubscribeToOrder` com `IgnoreQueryFilters` por token) e publicação de `PublicOrder` no grupo `order:{token}` em `SignalROrderNotifier` quando o status do pedido muda, com `app.MapHub<OrdersHub>("/hubs/orders")` (depende de: T063, T071). T071 verde.
- [ ] T073 [P] [US4] [TEST-FRONT] `frontend/src/app/core/realtime/order-realtime.service.spec.ts` (fake de `HubConnection`):
  - `watch(publicToken)` conecta anônimo em `/hubs/orders` e invoca `SubscribeToOrder`;
  - repete a inscrição e emite resync em `onreconnected`;
  - expõe `OrderUpdated`;
  - `stop()` ao destruir.
- [ ] T074 [US4] [IMPL-FRONT] `frontend/src/app/core/realtime/order-realtime.service.ts` (depende de: T065, T073). T073 verde.
- [ ] T075 [P] [US4] [TEST-FRONT] Ampliar `frontend/src/app/features/order-tracking/order-tracking.page.spec.ts` e `frontend/src/app/features/public-menu/public-menu.page.spec.ts` (depende de: T056):
  - rótulos por status: paid "Pago / aguardando preparo", in_preparation "Em preparo", picked_up "Retirado";
  - `ready` mostra o banner destacado "Seu pedido está pronto — retire no balcão" com o código (`role="alert"`);
  - aplica `OrderUpdated` sem recarregar;
  - resync recarrega via `getPublicOrder`;
  - abrir a página grava em `OrderHistoryStore`;
  - o cardápio público mostra o link "Acompanhar meu pedido" para o último pedido daquele slug.
- [ ] T076 [US4] [IMPL-FRONT] Tempo real em `frontend/src/app/features/order-tracking/order-tracking.page.ts` e link de histórico em `public-menu.page.ts` (depende de: T058, T074, T075). T075 verde.
- [ ] T077 [US4] [TEST-FRONT] Helper E2E `frontend/e2e/support/kitchen.ts` (`advancePart(token, orderId, station, status)` e `pickUp(token, orderId)`), que avança partes e marca a retirada via API com o token do Dono (depende de: T069).
- [ ] T078 [US4] [TEST-FRONT] `frontend/e2e/us4-tracking.spec.ts` (depende de: T077). Fluxo:
  - comprar;
  - a página mostra "Pago / aguardando preparo";
  - avançar a parte via API leva a "Em preparo" em ≤ 5 s sem reload;
  - todas prontas mostram o banner com o código;
  - retirada mostra "Retirado";
  - fechar a página, abrir `/m/{slug}` numa nova página do mesmo contexto e usar "Acompanhar meu pedido" leva ao status atual.

**Checkpoint US4**: T078 verde.

---

## Phase 7: User Story 5 — Equipe e pedidos do Dono (P5)

**Goal**: o Dono cadastra e desativa contas da Cozinha, com corte imediato, e consulta e filtra
os pedidos.

**Independent Test**: E2E T088. Cria uma conta de Cozinha, ela entra no painel, é desativada e
perde o acesso; a lista de pedidos só mostra os do próprio estabelecimento.

### Fatia 5a: Equipe REST (back), que pode começar logo após a Foundational

- [ ] T079 [P] [US5] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Staff/StaffEndpointsTests.cs` (depende de: T006):
  - `POST /api/staff` dá 201 e o login com as credenciais dá 200 com `role=kitchen`;
  - e-mail já usado por qualquer conta, inclusive o Dono e em outra caixa, dá 409 `email_taken`;
  - `GET /api/staff` lista só a Cozinha do próprio estabelecimento (ativos e inativos), sem o Dono;
  - `PUT /api/staff/{id}/deactivate` dá 200 com `isActive=false`, e repetir dá 200;
  - depois de desativar, **o token já emitido** dá 401 em `GET /api/auth/me`, e o login dá 401 `invalid_credentials`;
  - o id do Dono em `/deactivate` dá 404;
  - **papel** (US5-2): token kitchen em `/api/staff`, `/api/categories`, `/api/menu-items`, `/api/orders` e `/api/establishment` dá 403;
  - **isolamento**: B desativar a cozinha de A dá 404.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): as 3 operações `/api/staff...`.
- [ ] T080 [US5] [IMPL-BACK] `backend/src/EasyMenu.Application/Staff/StaffService.cs` e `backend/src/EasyMenu.Api/Controllers/StaffController.cs` (`OwnerOnly`) (depende de: T015, T079). T079 verde.

### Fatia 5b: Corte no tempo real (back)

- [ ] T081 [US5] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Realtime/KitchenHubDeactivationTests.cs` (depende de: T062, T079): com a conexão do hub da cozinha aberta, a desativação fecha a conexão (evento `Closed` em ≤ 5 s), e uma nova conexão com o mesmo token falha.
- [ ] T082 [US5] [IMPL-BACK] Corte das conexões (depende de: T063, T080, T081). T081 verde.
  - `backend/src/EasyMenu.Application/Abstractions/ISessionTerminator.cs`;
  - `backend/src/EasyMenu.Api/Realtime/HubConnectionTracker.cs`: registra as conexões do `KitchenHub` por `UserId` em `OnConnected`/`OnDisconnected` e aborta via `HubCallerContext.Abort()`;
  - chamada em `StaffService.Deactivate`.

### Fatia 5c: Pedidos admin (back)

- [ ] T083 [P] [US5] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Orders/OrdersEndpointsTests.cs` (depende de: T047):
  - `GET /api/orders` ordena do mais recente ao mais antigo (usar `FakeTimeProvider` para espaçar os pedidos);
  - `?status=picked_up` filtra;
  - paginação (`page`, `pageSize`, `totalCount`);
  - `pageSize` > 100 dá 400;
  - `GET /api/orders/{orderId}` devolve itens com snapshot, setor e partes;
  - kitchen dá 403;
  - **isolamento**: a lista de B não traz pedidos de A, e B em `GET /api/orders/{idDeA}` dá 404.

  Operações cobertas (o Dev Back remove o `x-pending` delas ao implementar): as 2 operações `/api/orders...`.
- [ ] T084 [US5] [IMPL-BACK] `backend/src/EasyMenu.Application/Orders/OrderQueryService.cs` e `backend/src/EasyMenu.Api/Controllers/OrdersController.cs` (`OwnerOnly`) (depende de: T049, T083). T083 verde.

### Fatia 5d: Front

- [ ] T085 [P] [US5] [TEST-FRONT] `frontend/src/app/features/admin/staff/staff.page.spec.ts`:
  - lista nome, e-mail e situação (Ativo/Desativado);
  - formulário de cadastro com validação (nome, e-mail, senha ≥ 8);
  - 409 `email_taken` mostra a mensagem;
  - "Desativar" pede confirmação, chama `deactivateStaffMember` e atualiza a linha.
- [ ] T086 [P] [US5] [TEST-FRONT] Pedidos admin:
  - `frontend/src/app/features/admin/orders/orders.page.spec.ts`:
    - colunas código, cliente ("—" quando ausente), data/hora `dd/MM/yyyy HH:mm`, total (`BrlPipe`) e status em pt-BR;
    - o select de status chama `listOrders({status})`;
    - paginação;
    - a linha leva a `/admin/pedidos/:orderId`.
  - `order-detail.page.spec.ts`: itens com o preço do snapshot e subtotal, total, e partes com o status.
- [ ] T087 [US5] [IMPL-FRONT] `frontend/src/app/features/admin/staff/staff.page.ts` e `frontend/src/app/features/admin/orders/{orders.page.ts,order-detail.page.ts}` (depende de: T034, T085, T086). T085 e T086 verdes.
- [ ] T088 [US5] [TEST-FRONT] `frontend/e2e/us5-staff.spec.ts` (depende de: T069). Fluxo:
  - Dono cria a Cozinha pela UI;
  - contexto B faz login e cai em `/cozinha`;
  - Cozinha tentando `/admin` é redirecionada;
  - Dono desativa;
  - em B, a próxima ação (ex.: reload) leva a `/login`, e o novo login mostra erro;
  - Dono em `/admin/pedidos` filtra por status e abre um pedido.

**Checkpoint US5**: T088 verde.

---

## Phase 8: Polish & cross-cutting

- [ ] T089 [P] [TEST-BACK] `backend/tests/EasyMenu.IntegrationTests/Isolation/CrossTenantSweepTests.cs` (SC-005), um teste parametrizado. Com A e B cadastrados e dados completos (cardápio, pedido, staff), percorre **todas** as operações autenticadas do contrato que recebem id, usando ids de A e token de B, e espera 404 (ou 403 para papel). Confere também que as listagens de B não contêm ids de A.
- [ ] T090 [TEST-BACK] Adicionar em `backend/tests/EasyMenu.IntegrationTests/Contract/OpenApiContractTests.cs` um teste que falha se restar qualquer `x-pending: true` em `/contracts/openapi.yaml` (fechamento do MVP) (depende de: todas as tarefas IMPL-BACK).
- [ ] T091 [P] [TEST-FRONT] `frontend/e2e/mobile-360.spec.ts` (SC-008): viewport 360×740 em `/m/{slug}`, no carrinho e em `/pedido/{token}`; sem scroll horizontal (`scrollWidth <= clientWidth`); botões com alvo ≥ 44 px de altura.
- [ ] T092 [IMPL-FRONT] Ajustes responsivos em `frontend/src/styles.scss` e nos SCSS das páginas públicas até T091 ficar verde (depende de: T091).
- [ ] T093 [IMPL-BACK] Revisar os logs: nenhum log de senha, token ou `PublicToken` completo. Configurar `Serilog`? **Não** (YAGNI): ajustar apenas os níveis em `appsettings.json` e mascarar o token no log de requests, se houver (depende de: T082).

---

## Dependencies & Execution Order

### Fases

- **Setup (T001–T004)** → **Foundational (T005–T018)** → stories.
- **US1** (T019–T042): back e front em paralelo, ambos após a Foundational.
- **US2** (T043–T058):
  - back depende de US1-back (T026, porque os testes usam `CreateMenuAsync`);
  - front depende só da Foundational front (T018);
  - o E2E T057 depende de US2 back e front.
- **US3** (T059–T070):
  - back depende de T049 (pedido);
  - front depende de T018 e da página de pedido (só para o E2E).
- **US4** (T071–T078):
  - back depende de T063 (hub e notifier);
  - front depende de T058 e T065.
- **US5** (T079–T088):
  - **equipe REST (T079–T080) depende só da Foundational** e pode rodar em paralelo com US1–US4;
  - corte no hub (T081–T082) depende de T063;
  - pedidos admin (T083–T084) dependem de T049.
- **Polish (T089–T093)**: depois de todas as stories.

### Regra TDD por fatia

Para cada fatia: o `[TEST-*]` faz commit vermelho; o `[IMPL-*]` correspondente deixa verde;
depois vem a próxima fatia. O Dev nunca começa uma fatia cujo teste não esteja commitado.

### Trilhas paralelas por agente

| Momento | Testador Back | Dev Back | Testador Front | Dev Front |
|---|---|---|---|---|
| 1 | T003, T005, T006, T007–T009 | T001, T002 → T010–T014 (após T005) | T016, T017 | T004 → T018 (após T016) |
| 2 | T019, T021, T022, T024, T025, **T079** | T015 → T020, T023, T026, **T080** | T030, T031, T033, T035, T037–T039 | T032, T034, T036, T040 |
| 3 | T027, T028, T043, T045–T048 | T029, T044, T049 | T041, T042, T050, T052, T054, T056 | T051, T053, T055, T058 |
| 4 | T059, T060, T062, **T083** | T061, T063, **T084** | T057, T064, T066, T068–T070 | T065, T067 |
| 5 | T071, T081 | T072, T082 | T073, T075, T077, T078, T085, T086 | T074, T076, T087 |
| 6 | T089, T090 | T093 | T088, T091 | T092 |

## Parallel Examples

```text
# US1: back e front ao mesmo tempo (após a Foundational)
Testador Back: T019, T021, T022, T024, T025, T027 em paralelo (arquivos distintos; T028 depende de T025)
Testador Front: T030, T031, T033, T035, T037, T038, T039 em paralelo

# US2
Testador Back: T045 e T046 (unidade) em paralelo com T043 (integração)
Testador Front: T050, T052, T054, T056 em paralelo

# US3
Testador Back: T059 (unidade) ‖ T060 (integração)
Testador Front: T064 ‖ T066

# US5: equipe pode andar desde a Foundational
Testador Back: T079 enquanto US1–US3 avançam; Dev Back pega T080 entre fatias
```

## Implementation Strategy

### MVP primeiro

1. Setup + Foundational.
2. US1 com E2E T042 verde: o Dono já tem o cardápio digital. **Parar e validar** (Revisor).
3. US2 com T057 verde: primeira venda ponta a ponta (fluxo de receita).
4. US3 (operação), US4 (experiência) e US5 (equipe/gestão), cada uma com seu checkpoint E2E e
   revisão.

### Entrega incremental

Cada checkpoint entrega valor demonstrável e passa pelo Revisor antes de integrar. As suítes do
lado alterado MUST estar verdes (constituição, fluxo com agentes).

## Resumo

| Fase | Tarefas | TEST-BACK | IMPL-BACK | TEST-FRONT | IMPL-FRONT |
|---|---|---|---|---|---|
| Setup | 4 | 1 | 2 | 0 | 1 |
| Foundational | 14 | 5 | 6 | 2 | 1 |
| US1 | 24 | 7 | 4 | 9 | 4 |
| US2 | 16 | 5 | 2 | 5 | 4 |
| US3 | 12 | 3 | 2 | 5 | 2 |
| US4 | 8 | 1 | 1 | 4 | 2 |
| US5 | 10 | 3 | 3 | 3 | 1 |
| Polish | 5 | 2 | 1 | 1 | 1 |
| **Total** | **93** | **27** | **21** | **29** | **16** |
