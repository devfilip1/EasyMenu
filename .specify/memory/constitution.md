<!--
Sync Impact Report
- Versão: (template) → 1.0.0
- Princípios definidos: I. TDD Inegociável; II. Contrato Primeiro; III. Isolamento Multi-tenant;
  IV. Integridade Monetária e Snapshots; V. Simplicidade (YAGNI)
- Seções adicionadas: Stack e Restrições Técnicas; Fluxo de Desenvolvimento com Agentes; Governança
- Seções removidas: nenhuma
- Templates: plan/spec/tasks leem a constituição em tempo de execução — nenhuma alteração necessária
- TODOs adiados: nenhum
-->
# EasyMenu Constitution

EasyMenu é um cardápio digital multi-tenant para bares, boates e restaurantes. Estabelecimentos
se cadastram, montam o próprio cardápio, consumidores pedem e pagam pelo app, e a cozinha/bar
recebe os pedidos em tempo real e sinaliza quando estão prontos para retirada.

## Core Principles

### I. TDD Inegociável

- Todo código de produção MUST nascer de um teste que falhou antes (Red → Green → Refactor).
- Quem escreve o teste NÃO é quem implementa: o agente Testador escreve os testes e faz o
  commit `test:` com a suíte falhando; o agente Dev faz os testes passarem.
- O implementador MUST NOT editar, apagar ou enfraquecer testes. Se considerar um teste errado,
  devolve ao Testador com a justificativa.
- Backend: testes de unidade de domínio + testes de integração da API (WebApplicationFactory +
  Testcontainers com PostgreSQL real). Frontend: testes de componente/serviço; fluxos críticos
  cobertos por E2E com Playwright.
- Nenhuma tarefa é concluída com teste vermelho, pulado (`Skip`) ou comentado.

**Racional**: separar autor do teste e autor do código impede que o teste seja moldado para
passar e mantém as specs como critério objetivo de aceite.

### II. Contrato Primeiro

- `contracts/openapi.yaml` é a fonte da verdade da comunicação backend ↔ frontend.
- Endpoints novos ou alterados MUST ser descritos no contrato antes de testes e código;
  mudanças de contrato são responsabilidade do agente Arquiteto.
- O frontend consome a API por um client TypeScript gerado a partir do contrato; chamadas HTTP
  escritas à mão contra endpoints da API são proibidas.
- O backend MUST ter teste que valida que a API exposta corresponde ao contrato.

**Racional**: permite que back e front sejam desenvolvidos em paralelo por agentes diferentes
sem divergência.

### III. Isolamento Multi-tenant

- Todo dado de negócio (cardápio, itens, pedidos, equipe) pertence a exatamente um
  estabelecimento (`EstablishmentId`).
- Nenhum usuário autenticado MUST conseguir ler ou alterar dados de outro estabelecimento;
  o filtro de tenant é aplicado no backend, nunca confiado ao cliente.
- Cada endpoint autenticado MUST ter ao menos um teste de integração de isolamento
  (acesso cruzado retorna 404/403).

**Racional**: vazamento entre estabelecimentos é o defeito mais grave possível numa plataforma
multi-tenant.

### IV. Integridade Monetária e Snapshots

- Valores monetários MUST ser inteiros em centavos (`long`/`number` inteiro), nunca ponto
  flutuante; a moeda é BRL.
- Preço promocional, quando existir, MUST ser maior que zero e menor que o preço normal.
- O item de pedido MUST guardar snapshot de nome e preço unitário no momento da compra;
  mudanças posteriores no cardápio não alteram pedidos existentes.
- O total do pedido é sempre calculado no backend; valores enviados pelo cliente são ignorados.

**Racional**: evita erros de arredondamento e garante que o histórico de pedidos é imutável.

### V. Simplicidade (YAGNI)

- No MVP o pagamento é SIMULADO: clicar em "comprar" confirma o pedido como pago, sem validar
  dados de pagamento. A simulação MUST ficar atrás de uma interface (`IPaymentGateway`) para
  permitir um gateway real depois.
- O consumidor compra como convidado (sem conta). Apenas Dono e Cozinha têm login.
- Nada de abstrações, camadas ou dependências sem uma necessidade concreta presente na spec.

**Racional**: entregar o fluxo ponta a ponta primeiro; complexidade só quando justificada.

## Stack e Restrições Técnicas

- **Backend** (`/backend`): ASP.NET Core Web API em .NET 10, Clean Architecture leve
  (Domain / Application / Infrastructure / Api), EF Core + PostgreSQL, SignalR para tempo real,
  autenticação JWT com papéis `Owner` e `Kitchen`. Testes: xUnit, FluentAssertions,
  Testcontainers, WebApplicationFactory.
- **Frontend** (`/frontend`): Angular (versão estável mais recente), standalone components,
  signals, mobile-first. Testes de componente com o runner padrão do Angular CLI; E2E com
  Playwright.
- **Contrato** (`/contracts/openapi.yaml`) e **specs** (`/specs`) na raiz do monorepo.
- Backend e frontend são aplicações separadas, com build, testes e deploy independentes.
- Ambiente local via Docker Compose (PostgreSQL).
- Perfis de usuário: Consumidor (convidado), Dono/Admin (`Owner`) e Cozinha (`Kitchen`).
  O painel da cozinha é único, com seções separadas para Bar e Cozinha.
- Documentação em português do Brasil; código, nomes de tipos e commits em inglês.

## Fluxo de Desenvolvimento com Agentes

- Cada feature segue o Spec Kit: `specify` → `clarify` → `plan` → `tasks` → `implement`.
- Time no Maestri: Maestro (orquestra e integra), Produto (specs), Arquiteto (plano, modelo de
  dados, contrato, tasks), Testador Back, Dev Back, Testador Front, Dev Front, Revisor/QA.
- Cada agente só altera os caminhos do seu papel e faz `git add` apenas desses caminhos.
- Toda entrega passa pelo Revisor (code review + verificação desta constituição) antes de ser
  integrada; a suíte de testes do lado alterado MUST estar verde.
- Commits seguem Conventional Commits (`test:`, `feat:`, `refactor:`, `docs:`, `chore:`) e
  NUNCA incluem linhas de coautoria ou atribuição a IA.

## Governance

- Esta constituição prevalece sobre qualquer outra prática do projeto. O Revisor MUST reprovar
  entregas que a violem; o `plan.md` de cada feature MUST conter o Constitution Check.
- Exceções precisam ser justificadas na seção "Complexity Tracking" do plano.
- Emendas: proposta documentada, aprovação do dono do projeto e atualização da versão.
- Versionamento semântico: MAJOR para remoção/redefinição de princípio, MINOR para princípio ou
  seção nova, PATCH para esclarecimentos.

**Version**: 1.0.0 | **Ratified**: 2026-11-25 | **Last Amended**: 2026-11-25
