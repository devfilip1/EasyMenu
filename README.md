# EasyMenu

Cardápio digital multi-tenant para bares, boates e restaurantes. Regras do projeto em
[`.specify/memory/constitution.md`](.specify/memory/constitution.md).

## Estrutura

```
backend/                     ASP.NET Core Web API (.NET 10) — solution EasyMenu.slnx
  src/EasyMenu.Domain          entidades e regras de domínio (sem dependências)
  src/EasyMenu.Application     casos de uso (depende de Domain)
  src/EasyMenu.Infrastructure  EF Core + Npgsql (AppDbContext, migrations)
  src/EasyMenu.Api             controllers, OpenAPI, SignalR, CORS
  tests/EasyMenu.UnitTests         xUnit + FluentAssertions
  tests/EasyMenu.IntegrationTests  WebApplicationFactory + Testcontainers (PostgreSQL 17)
frontend/                    Angular 22 (standalone, signals, SCSS, sem SSR)
  src/app/features/            páginas por feature (lazy loading)
  src/app/api/generated/       client gerado do contrato (NÃO versionado)
  e2e/                         testes Playwright
contracts/openapi.yaml       contrato da API (fonte da verdade; dono: Arquiteto)
specs/                       specs das features (Spec Kit)
docker-compose.yml           PostgreSQL 17 para desenvolvimento local
```

## Pré-requisitos

.NET SDK 10, Node.js 24 (npm 11), Docker.

## Subindo localmente

```bash
cp .env.example .env            # opcional; os padrões já funcionam
docker compose up -d            # PostgreSQL em localhost:5432

cd backend
dotnet run --project src/EasyMenu.Api   # http://localhost:5080  (GET /health, /openapi/v1.json)

cd frontend
npm install
npm start                       # http://localhost:4200 (gera o client da API antes de subir)
```

Rotas do front: `/m/:slug` (cardápio público), `/pedido/:publicToken`, `/admin` (`/admin/cardapio`, `/admin/equipe`, `/admin/pedidos`, `/admin/pedidos/:orderId`, `/admin/estabelecimento`), `/cozinha`, `/login`, `/cadastro`.

## Testes

```bash
cd backend && dotnet test       # unidade + integração (precisa do Docker rodando)

cd frontend
npm test                        # testes de componente/serviço (Vitest, runner padrão do Angular CLI)
npm run build
npm run e2e:install             # uma vez: baixa o Chromium do Playwright
npm run e2e                     # Playwright (sobe o `ng serve` automaticamente)
```

O CI (`.github/workflows/ci.yml`) roda backend e frontend em jobs separados.

## Decisões de scaffolding

- **Controllers** (não minimal APIs) na Api: organização por recurso, filtros/atributos de
  autorização por papel e `ProducesResponseType` alimentando o OpenAPI gerado.
- **Contrato validado no backend**: `OpenApiContractTests` compara as operações (`MÉTODO /rota`)
  de `contracts/openapi.yaml` com o documento gerado pela API (`/openapi/v1.json`). Endpoint novo
  sem contrato (ou contrato sem endpoint) quebra a suíte.
- **Testes de integração**: herde de `IntegrationTestBase` — um único container PostgreSQL por
  execução, migrations aplicadas na subida e banco truncado antes de cada teste.
- **Client do front gerado no build** (`ng-openapi-gen`, script `npm run generate:api`, executado
  automaticamente por `prestart`/`prebuild`/`pretest`). A saída não é versionada, então o front
  nunca diverge do contrato. Chamadas HTTP escritas à mão contra a API são proibidas.
- **URL da API**: `src/environments/environment*.ts` (`http://localhost:5080` em dev; mesma
  origem em produção). CORS do backend libera `http://localhost:4200` (`Cors:AllowedOrigins`).
- FluentAssertions fixado na **7.x** (licença Apache 2.0; a 8.x exige licença comercial).
