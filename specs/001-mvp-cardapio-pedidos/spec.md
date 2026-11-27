# Feature Specification: EasyMenu MVP — Cardápio Digital e Pedidos

**Feature Branch**: `001-mvp-cardapio-pedidos`

**Created**: 2026-11-25

**Status**: Draft

**Input**: User description: "EasyMenu MVP: cardápio digital para bares, boates e restaurantes. Dono (Owner) cadastra-se com e-mail e senha criando o seu estabelecimento (nome, slug público único), gerencia o cardápio (categorias ordenáveis; itens com nome, descrição, foto, preço, preço promocional opcional menor que o preço, disponibilidade e setor de preparo Bar ou Cozinha), cadastra contas da equipe de Cozinha e vê a lista de pedidos. Consumidor convidado acessa o cardápio público pelo link/QR (/m/{slug}), monta carrinho, informa apenas o nome (opcional) e clica em Comprar; o pagamento é simulado (qualquer clique conta como pago); recebe um código de retirada curto e acompanha o status em tempo real. Cozinha tem painel único em tempo real com seções Bar e Cozinha; marca cada parte como Em preparo e Pronto; quando todas as partes estão prontas o pedido fica Pronto e o consumidor é avisado; a equipe marca Retirado ao entregar. Regras: multi-tenant estrito; dinheiro em centavos BRL; pedido guarda snapshot de nome e preço; total calculado no servidor; itens esgotados não podem ser comprados."

## Visão geral

O EasyMenu permite que um estabelecimento (bar, boate ou restaurante) publique seu cardápio
digital, receba pedidos já pagos de consumidores sem conta e coordene o preparo entre Bar e
Cozinha até a retirada no balcão. O MVP entrega esse fluxo ponta a ponta com pagamento
simulado.

**Perfis**:

- **Dono (Owner)**: cria e administra um estabelecimento; tem login próprio.
- **Equipe da Cozinha (Kitchen)**: opera o painel de preparo do estabelecimento; tem login
  próprio criado pelo Dono.
- **Consumidor (convidado)**: acessa o cardápio público e compra sem criar conta.

## Clarifications

### Session 2026-11-25

> Perguntas geradas pelo `/speckit-clarify` e respondidas pelo agente de Produto com a opção
> recomendada para o MVP, conforme autorização do dono do projeto.

- Q: Como os membros da Cozinha fazem login: com e-mail ou com um nome de usuário? → A: Com
  e-mail e senha, e o e-mail é único em todo o sistema (mesma tela de login do Dono; o papel
  define o que cada um acessa).
- Q: Qual o formato e a regra de unicidade do código de retirada? → A: 4 caracteres aleatórios
  com letras maiúsculas e dígitos, sem caracteres ambíguos (0, O, 1, I, L), únicos entre os
  pedidos ainda não retirados do mesmo estabelecimento.
- Q: Como a página do pedido é protegida, já que o consumidor não tem conta? → A: Pelo próprio
  link do pedido, que contém um identificador longo e impossível de adivinhar; o aparelho guarda
  esse link para o consumidor voltar; o código de retirada sozinho não dá acesso à página.
- Q: O Dono também pode abrir e operar o painel da Cozinha? → A: Sim; o Dono tem todos os
  acessos da Cozinha no próprio estabelecimento, além dos administrativos.
- Q: O slug do estabelecimento pode ser alterado depois do cadastro? → A: Não; o slug é
  definitivo no MVP (para não invalidar QR codes impressos); o nome do estabelecimento pode ser
  editado.

### Session 2026-11-27

> Ajustes pedidos pelo dono do projeto após testar o MVP implementado; detalhes em aberto
> decididos pelo agente de Produto com bom senso de MVP.

- Q: A navegação do painel do Dono pode gerar rolagem horizontal em telas estreitas? → A: Não.
  As abas (Cardápio, Equipe, Pedidos, Estabelecimento, Painel da cozinha, Sair) MUST caber na
  largura da tela a partir de 360 px, sem rolagem horizontal da página nem da barra de abas;
  podem ocupar mais de uma linha, mas todas ficam visíveis e acionáveis (FR-039, SC-008, US5
  cenário 6).
- Q: Como a equipe confirma que entregou o pedido à pessoa certa? → A: Para marcar um pedido
  Pronto como Retirado, a equipe digita o código de retirada que o consumidor apresenta no
  balcão; o sistema confere o código no servidor e só então marca como Retirado (FR-027,
  FR-027a, US3 cenários 5, 7 e 8).
- Q: O código é digitado no cartão do pedido ou num campo geral que localiza o pedido? → A: No
  cartão do pedido Pronto (ação "Confirmar retirada"); o código é conferido contra aquele
  pedido específico. Um código válido de outro pedido conta como código errado.
- Q: Como o código digitado é comparado? → A: Maiúsculas/minúsculas são ignoradas e espaços no
  início e no fim são removidos; qualquer outra diferença (inclusive espaços no meio) torna o
  código incorreto.
- Q: O que acontece quando o código está errado? → A: O pedido continua Pronto e o painel
  mostra a mensagem "Código de retirada incorreto"; a equipe pode tentar de novo, sem limite de
  tentativas no MVP. Campo vazio também é recusado com mensagem clara.
- Q: O painel continua mostrando o código de retirada dos pedidos? → A: Sim; o painel segue
  exibindo o código (FR-028) para a equipe identificar os pedidos entre as seções Bar e
  Cozinha. A digitação serve como conferência contra entrega trocada, não como proteção
  antifraude; ocultar o código no painel fica fora do MVP.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Dono cadastra estabelecimento e monta o cardápio (Priority: P1)

Um dono de bar cria sua conta com e-mail e senha e, no mesmo cadastro, cria seu estabelecimento
informando o nome e o slug público (o endereço que vai no link/QR). Em seguida cria categorias
(ex.: "Drinks", "Porções"), define a ordem em que aparecem e cadastra itens em cada categoria
com nome, descrição, foto, preço, preço promocional opcional, disponibilidade e setor de
preparo (Bar ou Cozinha). Pode editar, reordenar, marcar como esgotado e remover itens e
categorias a qualquer momento.

**Why this priority**: sem estabelecimento e cardápio cadastrados nenhum outro fluxo existe;
é a base de todo o produto e já entrega valor (o dono passa a ter seu cardápio organizado).

**Independent Test**: criar uma conta de Dono, cadastrar 2 categorias e 3 itens (um com
promoção, um esgotado), reordenar as categorias e verificar que tudo foi salvo e é exibido na
área administrativa na ordem definida.

**Acceptance Scenarios**:

1. **Given** um visitante sem conta, **When** ele se cadastra com e-mail válido, senha válida,
   nome do estabelecimento e slug ainda não usado, **Then** a conta de Dono e o estabelecimento
   são criados e ele entra autenticado na área administrativa do seu estabelecimento.
2. **Given** um slug já usado por outro estabelecimento, **When** um visitante tenta se
   cadastrar com esse slug, **Then** o cadastro é recusado com mensagem informando que o slug
   está indisponível.
3. **Given** um e-mail já cadastrado, **When** alguém tenta se cadastrar com ele, **Then** o
   cadastro é recusado com mensagem clara.
4. **Given** um Dono cadastrado, **When** ele faz login com e-mail e senha corretos, **Then**
   acessa a área administrativa; com credenciais incorretas, o acesso é negado sem revelar qual
   dos dois campos está errado.
5. **Given** um Dono autenticado, **When** ele cria as categorias "Drinks" e "Porções" e move
   "Porções" para antes de "Drinks", **Then** as categorias passam a ser exibidas nessa nova
   ordem (na área administrativa e no cardápio público).
6. **Given** uma categoria existente, **When** o Dono cadastra um item com nome, descrição,
   foto, preço de R$ 25,00, preço promocional de R$ 19,90 e setor Bar, **Then** o item é salvo
   com esses dados.
7. **Given** o formulário de item, **When** o Dono informa preço promocional igual ou maior
   que o preço, ou menor ou igual a zero, **Then** o item não é salvo e a mensagem explica a
   regra.
8. **Given** um item ativo, **When** o Dono o marca como esgotado, **Then** o item continua na
   área administrativa marcado como esgotado e deixa de poder ser comprado.
9. **Given** um item cadastrado, **When** o Dono remove o item, **Then** ele some do cardápio,
   e pedidos já feitos com ele permanecem inalterados.

---

### User Story 2 - Consumidor vê o cardápio e compra (pagamento simulado) (Priority: P2)

Um cliente na mesa escaneia o QR code (ou abre o link `/m/{slug}`), vê o cardápio do
estabelecimento organizado por categorias, com foto e preço de cada item e promoções
destacadas, adiciona itens ao carrinho ajustando quantidades, informa opcionalmente seu nome e
clica em Comprar. O pagamento é simulado: qualquer clique em Comprar, com qualquer dado ou com
os campos vazios, é considerado pago. O cliente recebe na hora um código de retirada curto.

**Why this priority**: é a jornada que gera receita para o estabelecimento e o motivo de o
produto existir; depende apenas do cardápio (P1).

**Independent Test**: com um estabelecimento e itens cadastrados, abrir o link público sem
estar logado, adicionar 2 itens (um em promoção) com quantidades diferentes, comprar sem
informar nome e verificar que um pedido pago foi criado com o total correto e que um código de
retirada foi exibido.

**Acceptance Scenarios**:

1. **Given** um estabelecimento com categorias e itens, **When** um visitante abre o link
   público do estabelecimento, **Then** vê o nome do estabelecimento e as categorias na ordem
   definida pelo Dono, cada uma com seus itens disponíveis mostrando nome, descrição, foto e
   preço.
2. **Given** um item com preço promocional, **When** ele é exibido no cardápio público,
   **Then** o preço promocional aparece em destaque e o preço original aparece riscado ao lado.
3. **Given** um item esgotado, **When** o cardápio público é exibido, **Then** o item não pode
   ser adicionado ao carrinho.
4. **Given** um slug inexistente, **When** um visitante abre o link, **Then** vê uma página de
   "estabelecimento não encontrado".
5. **Given** itens no carrinho, **When** o consumidor altera quantidades ou remove itens,
   **Then** o subtotal de cada linha e o total do carrinho são atualizados.
6. **Given** um carrinho com itens disponíveis, **When** o consumidor clica em Comprar com o
   nome preenchido, vazio ou com qualquer texto, **Then** o pedido é registrado como pago, o
   total é calculado pelo sistema com os preços vigentes (promocional quando houver) e o
   consumidor é levado à página do pedido com o código de retirada.
7. **Given** um item que ficou esgotado depois de ir para o carrinho, **When** o consumidor
   clica em Comprar, **Then** a compra é recusada, o consumidor é informado de quais itens
   ficaram indisponíveis e pode ajustar o carrinho.
8. **Given** um carrinho vazio, **When** o consumidor tenta comprar, **Then** a compra não é
   permitida.

---

### User Story 3 - Cozinha recebe pedidos em tempo real e sinaliza o preparo (Priority: P3)

A equipe da Cozinha faz login e abre o painel do estabelecimento, dividido em duas seções:
Bar e Cozinha. Cada pedido pago aparece automaticamente, sem recarregar a página, nas seções
correspondentes aos setores dos seus itens (um pedido com cerveja e porção aparece nas duas
seções, cada uma mostrando só os seus itens). A equipe marca cada parte como Em preparo e
depois Pronto. Quando todas as partes de um pedido estão prontas, o pedido fica Pronto. Quando
o consumidor vai ao balcão, ele apresenta o código de retirada; a equipe digita esse código no
pedido e, se conferir, o pedido é marcado como Retirado.

**Why this priority**: fecha o ciclo operacional (pedido pago → preparo → entrega); sem ele os
pedidos não chegam a quem prepara.

**Independent Test**: logado como Cozinha, com o painel aberto, criar um pedido pago contendo
um item de Bar e um de Cozinha; verificar que ele aparece nas duas seções sem recarregar,
avançar cada parte até Pronto, confirmar que o pedido fica Pronto apenas depois da segunda
parte, tentar a retirada com um código errado (recusada) e então confirmá-la digitando o
código correto.

**Acceptance Scenarios**:

1. **Given** o painel aberto pela equipe, **When** um consumidor conclui uma compra com itens
   de Bar e de Cozinha, **Then** o pedido aparece em até 5 segundos nas duas seções, cada uma
   listando apenas os itens do seu setor com quantidades, o código de retirada e o nome do
   consumidor (quando informado).
2. **Given** um pedido apenas com itens de Bar, **When** ele é pago, **Then** aparece somente
   na seção Bar.
3. **Given** uma parte de pedido recém-recebida, **When** a equipe a marca como Em preparo e
   depois como Pronto, **Then** o status da parte muda e a mudança aparece para todos os
   painéis abertos do estabelecimento.
4. **Given** um pedido com partes de Bar e de Cozinha, **When** apenas a parte de Bar está
   Pronta, **Then** o pedido ainda não está Pronto; **When** a parte da Cozinha também fica
   Pronta, **Then** o pedido passa a Pronto.
5. **Given** um pedido Pronto com código de retirada `K7P4`, **When** a equipe aciona
   "Confirmar retirada" nesse pedido e digita `K7P4`, **Then** o pedido passa a Retirado e sai
   da lista de pedidos em andamento de todos os painéis abertos do estabelecimento.
6. **Given** usuários de dois estabelecimentos diferentes, **When** um pedido é feito no
   estabelecimento A, **Then** ele nunca aparece no painel do estabelecimento B.
7. **Given** um pedido Pronto com código `K7P4`, **When** a equipe digita ` k7p4 ` (minúsculas
   e com espaços nas pontas), **Then** o código é aceito e o pedido passa a Retirado.
8. **Given** um pedido Pronto com código `K7P4`, **When** a equipe digita um código diferente
   (ex.: `K7P5`, o código de outro pedido, `K7 P4`) ou deixa o campo vazio, **Then** o pedido
   continua Pronto, permanece no painel e é exibida a mensagem "Código de retirada incorreto"
   (ou, para campo vazio, "Informe o código de retirada"); a equipe pode tentar novamente.

---

### User Story 4 - Consumidor acompanha o status e é avisado para retirar (Priority: P4)

Após comprar, o consumidor permanece (ou volta) à página do seu pedido, que mostra o código de
retirada, os itens, o total e o status atual, atualizado automaticamente. Quando o pedido fica
Pronto, a página avisa de forma destacada que ele deve ir ao balcão retirar, informando o
código.

**Why this priority**: melhora muito a experiência e reduz filas no balcão, mas o fluxo
funciona sem ele (a equipe pode chamar pelo código).

**Independent Test**: abrir a página de um pedido pago, alterar o status das partes pelo
painel da Cozinha e verificar que a página do pedido reflete cada mudança sem recarregar e
exibe o aviso de retirada quando o pedido fica Pronto.

**Acceptance Scenarios**:

1. **Given** um pedido recém-pago, **When** o consumidor está na página do pedido, **Then** vê
   o código de retirada, os itens com quantidades e preços, o total pago e o status "Pago /
   aguardando preparo".
2. **Given** a página do pedido aberta, **When** alguma parte entra em preparo, **Then** o
   status exibido muda para "Em preparo" em até 5 segundos, sem recarregar.
3. **Given** a página do pedido aberta, **When** o pedido fica Pronto, **Then** a página exibe
   em até 5 segundos um aviso destacado "Seu pedido está pronto — retire no balcão" com o código.
4. **Given** a equipe confirmou a retirada digitando o código correto, **When** o consumidor
   está com a página do pedido aberta ou a abre depois, **Then** vê o status "Retirado" (em até
   5 segundos, se a página estiver aberta).
5. **Given** o consumidor fechou e reabriu o navegador no mesmo aparelho, **When** ele volta à
   página do pedido, **Then** continua vendo o status atualizado.

---

### User Story 5 - Dono gerencia a equipe da Cozinha e vê os pedidos (Priority: P5)

O Dono cadastra contas para os funcionários da Cozinha (cada um com login próprio), pode
desativá-las quando alguém sai, e consulta a lista de pedidos do seu estabelecimento com
código, nome do consumidor, horário, total e status.

**Why this priority**: necessário para operar com uma equipe real e acompanhar o movimento,
mas os fluxos principais podem ser demonstrados com uma conta de Cozinha criada inicialmente.

**Independent Test**: logado como Dono, criar uma conta de Cozinha, fazer login com ela e
acessar o painel; desativá-la e confirmar que o login passa a ser negado; abrir a lista de
pedidos e verificar que mostra apenas os pedidos do próprio estabelecimento.

**Acceptance Scenarios**:

1. **Given** um Dono autenticado, **When** ele cadastra um membro da Cozinha com nome,
   e-mail e senha, **Then** essa pessoa consegue entrar e acessa somente o
   painel da Cozinha daquele estabelecimento.
2. **Given** um usuário da Cozinha autenticado, **When** ele tenta acessar a gestão de
   cardápio, de equipe ou a lista administrativa de pedidos, **Then** o acesso é negado.
3. **Given** uma conta de Cozinha ativa, **When** o Dono a desativa, **Then** novos logins com
   ela são recusados e a sessão em uso deixa de ter acesso.
4. **Given** pedidos no estabelecimento, **When** o Dono abre a lista de pedidos, **Then** vê
   os pedidos do mais recente para o mais antigo, com código, nome do consumidor, data/hora,
   total e status, podendo filtrar por status, e pode abrir um pedido para ver seus itens.
5. **Given** dois estabelecimentos, **When** o Dono de A abre a lista de pedidos ou tenta
   acessar um pedido de B diretamente, **Then** não vê nem acessa nada de B.
6. **Given** um Dono autenticado num celular com tela de 360 px de largura, **When** ele abre
   o painel do Dono, **Then** todas as abas de navegação (Cardápio, Equipe, Pedidos,
   Estabelecimento, Painel da cozinha, Sair) ficam visíveis e acionáveis, sem rolagem
   horizontal da página nem da barra de abas.

---

### Edge Cases

- Dono altera preço ou nome de um item depois de um pedido: o pedido mantém o nome e o preço
  unitário da hora da compra.
- Preço do item muda entre o momento em que foi colocado no carrinho e o clique em Comprar: o
  pedido é cobrado pelo preço vigente no momento da compra, e a página do pedido mostra os
  valores efetivamente registrados.
- Dono remove a promoção ou o item fica esgotado enquanto está no carrinho: a compra usa o
  preço vigente; item esgotado bloqueia a compra (US2, cenário 7).
- Cardápio sem nenhuma categoria ou sem itens disponíveis: a página pública informa que o
  cardápio está indisponível no momento.
- Categoria sem itens disponíveis: não é exibida no cardápio público.
- Consumidor manipula preço ou total enviado na compra: valores enviados pelo cliente são
  ignorados; o total é sempre calculado pelo sistema.
- Consumidor informa nome muito longo ou com caracteres especiais: aceito até o limite de
  tamanho e exibido de forma segura (sem interpretar como conteúdo executável).
- Quantidade de um item no carrinho menor que 1 ou absurdamente alta: limitada a um intervalo
  válido (1 a 99 por linha).
- Foto em formato não suportado ou grande demais: o upload é recusado com mensagem clara; item
  pode ser salvo sem foto e exibe uma imagem padrão.
- Painel da Cozinha perde conexão: ao reconectar, exibe o estado atual de todos os pedidos em
  andamento, sem perder pedidos recebidos no intervalo.
- Duas pessoas da equipe alteram a mesma parte ao mesmo tempo: o status final é consistente e
  todos os painéis exibem o mesmo valor.
- Tentativa de voltar status (ex.: de Pronto para Em preparo) ou pular etapas indevidas: não
  permitida no MVP, exceto marcar Pronto direto de "recebido".
- Tentativa de marcar como Retirado um pedido que ainda não está Pronto: recusada, mesmo com o
  código correto.
- Código de retirada digitado errado, de outro pedido ou vazio: retirada recusada com mensagem
  clara; o pedido continua Pronto.
- Duas pessoas confirmam a retirada do mesmo pedido ao mesmo tempo: o pedido é marcado como
  Retirado uma única vez e a segunda confirmação informa que o pedido já foi retirado.
- Alguém tenta abrir a página de um pedido informando apenas o código de retirada ou um
  identificador inventado: recebe "pedido não encontrado".
- Dono tenta cadastrar um membro da Cozinha com e-mail já usado por qualquer conta: cadastro
  recusado com mensagem clara.
- Dono tenta remover uma categoria que ainda tem itens: remoção recusada até que os itens sejam
  movidos ou removidos.
- Usuário autenticado de um estabelecimento tenta acessar, por identificador direto, recursos
  de outro estabelecimento: recebe resposta de "não encontrado"/"acesso negado".

## Requirements *(mandatory)*

### Functional Requirements

**Conta e estabelecimento**

- **FR-001**: O sistema MUST permitir que um visitante se cadastre como Dono informando e-mail,
  senha, nome do estabelecimento e slug, criando a conta e o estabelecimento numa única
  operação.
- **FR-002**: O e-mail MUST ser único em todo o sistema (entre Donos e membros da Cozinha); a
  senha MUST ter no mínimo 8 caracteres.
- **FR-003**: O slug MUST ser único entre todos os estabelecimentos, conter apenas letras
  minúsculas sem acento, números e hífens, ter entre 3 e 50 caracteres e não começar nem
  terminar com hífen. O slug MUST NOT poder ser alterado após o cadastro.
- **FR-004**: O sistema MUST autenticar Donos e membros da Cozinha com e-mail e senha, numa
  mesma tela de login, e MUST recusar credenciais inválidas sem indicar qual campo está errado.
- **FR-005**: No MVP, cada Dono MUST administrar exatamente um estabelecimento e MUST poder
  editar o nome dele.

**Cardápio**

- **FR-006**: O Dono MUST poder criar, renomear, reordenar e remover categorias do seu
  estabelecimento; apenas categorias sem itens podem ser removidas.
- **FR-007**: O Dono MUST poder criar, editar e remover itens, cada um pertencente a uma
  categoria, com: nome (obrigatório), descrição (opcional), foto (opcional, enviada como
  arquivo de imagem), preço (obrigatório, maior que zero), preço promocional (opcional),
  disponibilidade (ativo/esgotado) e setor de preparo (Bar ou Cozinha, obrigatório).
- **FR-008**: O preço promocional, quando informado, MUST ser maior que zero e menor que o
  preço do item.
- **FR-009**: Todos os valores monetários MUST ser representados em centavos de real (BRL),
  como números inteiros, e exibidos ao usuário no formato brasileiro (ex.: R$ 19,90).
- **FR-010**: O Dono MUST poder alternar a disponibilidade de um item entre ativo e esgotado.
- **FR-011**: O Dono MUST poder definir a ordem dos itens dentro de uma categoria.

**Cardápio público e compra**

- **FR-012**: O sistema MUST disponibilizar, sem autenticação, o cardápio público de cada
  estabelecimento em um endereço baseado no seu slug (`/m/{slug}`), adequado para ser
  transformado em QR code.
- **FR-013**: O cardápio público MUST exibir categorias e itens na ordem definida pelo Dono,
  com nome, descrição, foto e preço; itens em promoção MUST mostrar o preço promocional em
  destaque e o preço original riscado.
- **FR-014**: Itens esgotados MUST NOT poder ser adicionados ao carrinho nem comprados.
- **FR-015**: O consumidor MUST poder montar um carrinho com itens de um único
  estabelecimento, ajustar quantidades (1 a 99 por item) e remover itens, vendo subtotais e
  total.
- **FR-016**: Na finalização, o sistema MUST solicitar apenas o nome do consumidor, que é
  opcional e aceita qualquer texto até 60 caracteres.
- **FR-017**: O pagamento MUST ser simulado: todo clique em Comprar com carrinho válido MUST
  resultar em pedido pago, independentemente de quaisquer dados informados ou ausentes.
- **FR-018**: O total do pedido MUST ser calculado pelo sistema a partir dos preços vigentes
  no momento da compra (preço promocional quando existir); valores de preço ou total enviados
  pelo consumidor MUST ser ignorados.
- **FR-019**: Cada item de pedido MUST guardar uma cópia (snapshot) do nome e do preço
  unitário no momento da compra, além da quantidade e do setor de preparo; alterações ou
  remoções posteriores no cardápio MUST NOT alterar pedidos existentes.
- **FR-020**: Se algum item do carrinho estiver esgotado ou removido no momento da compra, a
  compra inteira MUST ser recusada e o consumidor MUST ser informado de quais itens estão
  indisponíveis.
- **FR-021**: Cada pedido pago MUST receber um código de retirada de 4 caracteres aleatórios
  (letras maiúsculas e dígitos, excluindo os ambíguos 0, O, 1, I e L), único entre os pedidos
  não retirados do mesmo estabelecimento.

**Painel da Cozinha**

- **FR-022**: O sistema MUST oferecer à equipe da Cozinha um painel único do estabelecimento
  com duas seções, Bar e Cozinha.
- **FR-023**: Cada pedido pago MUST ser dividido em partes por setor de preparo; cada parte
  MUST aparecer apenas na seção do seu setor, listando somente os itens daquele setor.
- **FR-024**: Novos pedidos e mudanças de status MUST aparecer no painel automaticamente, sem
  ação manual de atualização, em até 5 segundos.
- **FR-025**: Cada parte MUST seguir a sequência de status Recebida → Em preparo → Pronta,
  sendo permitido marcar Pronta diretamente a partir de Recebida; retrocessos MUST NOT ser
  permitidos no MVP.
- **FR-026**: O status do pedido MUST ser derivado das partes: Pago (nenhuma parte iniciada),
  Em preparo (alguma parte em preparo ou pronta, mas não todas prontas), Pronto (todas as
  partes prontas) e Retirado (confirmado pela equipe com o código de retirada, conforme
  FR-027).
- **FR-027**: A equipe MUST poder marcar como Retirado apenas pedidos Prontos, e somente
  informando o código de retirada daquele pedido, que o consumidor apresenta no balcão; pedidos
  Retirados MUST sair da lista de pedidos em andamento de todos os painéis do estabelecimento.
- **FR-027a**: A conferência do código de retirada MUST ser feita pelo sistema no servidor,
  comparando com o código do pedido escolhido, ignorando maiúsculas/minúsculas e espaços no
  início e no fim. Código diferente (inclusive o de outro pedido) ou vazio MUST NOT marcar o
  pedido como Retirado: o pedido continua Pronto e a equipe vê uma mensagem clara ("Código de
  retirada incorreto" / "Informe o código de retirada"), podendo tentar de novo sem limite de
  tentativas no MVP.
- **FR-028**: O painel MUST exibir os pedidos em andamento em ordem de chegada (mais antigo
  primeiro), com código de retirada, nome do consumidor (se houver), horário e itens com
  quantidades.

**Acompanhamento do pedido**

- **FR-029**: Após a compra, o consumidor MUST ser levado a uma página do pedido que exibe
  código de retirada, itens, quantidades, preços registrados, total e status atual.
- **FR-030**: A página do pedido MUST refletir mudanças de status automaticamente em até 5
  segundos e, quando o pedido ficar Pronto, MUST exibir um aviso destacado para o consumidor ir
  retirar no balcão, mostrando o código.
- **FR-031**: O consumidor MUST conseguir voltar à página do seu pedido no mesmo aparelho
  depois de fechar o navegador.
- **FR-031a**: A página do pedido MUST ser acessível apenas pelo link do pedido, que contém um
  identificador longo e não adivinhável; o código de retirada sozinho MUST NOT dar acesso à
  página nem aos dados do pedido.

**Equipe e pedidos (Dono)**

- **FR-032**: O Dono MUST poder cadastrar membros da Cozinha com nome, e-mail e senha, listar
  e desativar essas contas.
- **FR-033**: Membros da Cozinha MUST ter acesso apenas ao painel da Cozinha do seu
  estabelecimento; não podem gerenciar cardápio, equipe ou consultar a lista administrativa de
  pedidos.
- **FR-033a**: O Dono MUST ter também acesso completo ao painel da Cozinha do seu
  estabelecimento, podendo realizar as mesmas ações da equipe.
- **FR-034**: Contas da Cozinha desativadas MUST perder acesso imediatamente (novos logins e
  sessões em andamento).
- **FR-035**: O Dono MUST poder consultar a lista de pedidos do seu estabelecimento, do mais
  recente ao mais antigo, com código, nome do consumidor, data/hora, total e status, filtrar
  por status e ver os itens de cada pedido.

**Isolamento e segurança**

- **FR-036**: Todo dado de negócio (categorias, itens, pedidos, equipe) MUST pertencer a
  exatamente um estabelecimento.
- **FR-037**: Nenhum usuário autenticado MUST conseguir ler ou alterar dados de outro
  estabelecimento; tentativas de acesso cruzado MUST resultar em "não encontrado" ou "acesso
  negado".
- **FR-038**: O cardápio público e a página do pedido MUST expor apenas os dados necessários
  ao consumidor (nunca dados de equipe, de outros pedidos ou de outros estabelecimentos).

**Layout responsivo**

- **FR-039**: A navegação por abas do painel do Dono (Cardápio, Equipe, Pedidos,
  Estabelecimento, Painel da cozinha, Sair) MUST se ajustar à largura da tela a partir de
  360 px: nenhuma rolagem horizontal da página nem da barra de abas, com todas as abas
  visíveis e acionáveis sem rolar para o lado (podendo ocupar mais de uma linha).

### Key Entities

- **Estabelecimento**: o negócio (bar, boate, restaurante). Atributos: nome (editável), slug
  público único e definitivo. Possui um Dono, categorias, itens, membros da Cozinha e pedidos.
- **Usuário**: pessoa com login. Atributos: nome, e-mail (único no sistema), senha, papel
  (Dono ou Cozinha), situação (ativo/desativado). Pertence a um estabelecimento.
- **Categoria**: agrupamento do cardápio. Atributos: nome, posição de ordenação. Pertence a um
  estabelecimento.
- **Item do cardápio**: produto vendido. Atributos: nome, descrição, foto, preço em centavos,
  preço promocional em centavos (opcional), disponibilidade, setor de preparo (Bar/Cozinha),
  posição na categoria. Pertence a uma categoria.
- **Pedido**: compra paga de um consumidor convidado. Atributos: identificador público não
  adivinhável (usado no link da página do pedido), código de retirada, nome do
  consumidor (opcional), data/hora, total em centavos, status (Pago, Em preparo, Pronto,
  Retirado). Pertence a um estabelecimento.
- **Item do pedido**: linha do pedido. Atributos: snapshot do nome e do preço unitário,
  quantidade, setor de preparo, subtotal. Referencia (sem depender de) o item do cardápio
  original.
- **Parte do pedido (por setor)**: conjunto dos itens de um pedido de um mesmo setor.
  Atributos: setor (Bar/Cozinha), status (Recebida, Em preparo, Pronta).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Um novo Dono consegue criar a conta, o estabelecimento e um cardápio com 3
  categorias e 10 itens em menos de 15 minutos, sem ajuda.
- **SC-002**: Um consumidor consegue, a partir da leitura do QR code, escolher 2 itens e
  concluir a compra em menos de 2 minutos.
- **SC-003**: 100% dos pedidos pagos aparecem no painel da Cozinha em até 5 segundos após a
  compra, nas seções corretas.
- **SC-004**: 100% das mudanças de status feitas pela equipe aparecem na página do pedido do
  consumidor em até 5 segundos.
- **SC-005**: 0 casos de dados de um estabelecimento visíveis ou alteráveis por usuários de
  outro estabelecimento nos testes de isolamento.
- **SC-006**: 100% dos pedidos mantêm nome, preço unitário e total originais após qualquer
  alteração posterior no cardápio, e nenhum total diverge da soma de (preço unitário ×
  quantidade) dos seus itens.
- **SC-007**: 0 pedidos contendo itens que estavam esgotados no momento da compra.
- **SC-008**: O cardápio público, a página do pedido e o painel do Dono são plenamente
  utilizáveis em celulares com tela a partir de 360 px de largura, sem rolagem horizontal da
  página; no painel do Dono, 100% das abas de navegação ficam visíveis e acionáveis sem
  rolagem horizontal em 360 px.
- **SC-009**: 0 pedidos marcados como Retirado sem que o código de retirada correto tenha sido
  informado.

## Assumptions

- O consumidor usa o celular, com acesso à internet, dentro do estabelecimento; a retirada é
  sempre no balcão (sem entrega em mesa ou delivery no MVP).
- Pagamento real, estorno, cancelamento de pedido, gorjeta, taxa de serviço, observações por
  item, adicionais/variações de item, cupons, relatórios financeiros, notificações push/SMS/
  e-mail, recuperação de senha e múltiplos estabelecimentos por Dono estão fora do escopo do
  MVP.
- O aviso de "pedido pronto" é exibido na própria página do pedido, que precisa estar aberta
  (ou ser reaberta) pelo consumidor.
- O cardápio público é exibido apenas em português do Brasil e os preços em BRL.
- A foto do item é um arquivo de imagem enviado pelo Dono; onde e como ela é armazenada é
  decisão do plano técnico.
- Itens removidos deixam de aparecer no cardápio, mas os pedidos antigos continuam exibindo
  seus dados graças ao snapshot.
- O estabelecimento está sempre "aberto" no MVP (não há controle de horário de funcionamento).
