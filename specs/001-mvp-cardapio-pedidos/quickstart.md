# Quickstart — validação do MVP

Guia para provar que a feature funciona de ponta a ponta. Os detalhes de API estão em
[`/contracts/openapi.yaml`](../../contracts/openapi.yaml) e os de tempo real em
[`/contracts/realtime.md`](../../contracts/realtime.md).

## Pré-requisitos

- .NET SDK 10, Node 24, Docker em execução.
- `docker compose up -d` na raiz (PostgreSQL 17).

## Subir

```bash
cd backend
dotnet ef database update --project src/EasyMenu.Infrastructure --startup-project src/EasyMenu.Api
dotnet run --project src/EasyMenu.Api          # http://localhost:5080

cd frontend
npm install && npm start                       # http://localhost:4200
```

As migrations também são aplicadas automaticamente na subida em `Development` (task T014).

## Suítes automatizadas (critério de aceite)

```bash
cd backend && dotnet test                      # unidade + integração + contrato + isolamento + hubs
cd frontend && npm test && npm run build
cd frontend && npm run e2e                     # Playwright contra API + front reais
```

Tudo MUST estar verde, sem `Skip` (constituição I). Ao fim do MVP, nenhuma operação de
`/contracts/openapi.yaml` pode continuar com `x-pending: true` (T090).

## Roteiro manual (um por user story)

| # | Passos | Resultado esperado |
|---|---|---|
| US1 | Abrir `/cadastro` e criar o Dono "Bar do Zé" com slug `bar-do-ze`. Criar as categorias "Drinks" e "Porções" e mover "Porções" para cima. Criar "Caipirinha" (R$ 25,00, promo R$ 19,90, Bar, com foto JPEG), "Batata" (R$ 30,00, Cozinha) e "Chopp" (R$ 12,00, Bar, esgotado). | A lista do admin mostra Porções antes de Drinks. A promoção ≥ preço é recusada com mensagem. Upload de `.gif` ou arquivo > 5 MB é recusado. |
| US2 | Em aba anônima, abrir `/m/bar-do-ze`. | Promoção em destaque com o preço original riscado. Chopp aparece como "Esgotado" e não pode ser adicionado. |
| US2 | Adicionar Caipirinha ×2 e Batata ×1 e comprar sem nome. | Vai para `/pedido/{token}` com código de 4 caracteres e total R$ 69,80. |
| US2 | Abrir `/m/nao-existe`. | Página "estabelecimento não encontrado". |
| US3 | Entrar como Cozinha em `/cozinha` (outra janela) e fazer um novo pedido. | Em ≤ 5 s ele aparece em Bar (Caipirinha) e em Cozinha (Batata), sem recarregar. |
| US3/US4 | Bar: Em preparo → Pronto. Cozinha: Pronto. | A página do pedido mostra "Em preparo" e depois o aviso "Seu pedido está pronto — retire no balcão" com o código. |
| US3 | Marcar Retirado. | O pedido some do painel; a página do pedido mostra "Retirado". |
| US4 | Fechar o navegador e reabrir `/m/bar-do-ze`. | Aparece o link "Acompanhar meu pedido" para o último pedido, e a página mostra o status atual. |
| US5 | Dono em `/admin/equipe` cria uma Cozinha, faz login com ela (outra janela) e depois a desativa. | A sessão da Cozinha perde acesso na próxima ação (401 → tela de login) e o painel desconecta. Novo login é recusado. |
| US5 | Dono em `/admin/pedidos` filtra por "Retirado" e abre um pedido. | Ordem do mais recente para o mais antigo; os itens aparecem com o preço do snapshot. |
| Isolamento | Criar um segundo Dono e tentar `GET /api/orders/{id-do-primeiro}` com o token dele. | 404. |
