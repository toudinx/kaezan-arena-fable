# FABLE TRACK — Features complexas para o Claude Fable 5

> **O que é este documento.** A fila de trabalho de **features grandes e arquiteturalmente
> densas** — reservadas para um modelo forte (Claude Fable 5). Cada uma é cross-cutting (toca
> engine + meta + frontend), sensível a determinismo, e exige **decisões de design e
> balanceamento**, não só implementação mecânica.
>
> **Diferença para o [ROADMAP.md](ROADMAP.md).** O ROADMAP é o track do Codex: tasks pequenas,
> bem especificadas, de baixa ambiguidade (adicionar monstros, preços, tooltips, juice). O
> FABLE_TRACK é o oposto: poucas features, cada uma com alto valor, alto risco e alta
> complexidade — onde vale pagar por um modelo premium porque o custo de fazer errado
> (quebrar determinismo, trivializar o balanceamento, gerar dungeons ruins) é alto.
>
> **Antes de qualquer feature daqui:** leia `README.md`, `CLAUDE.md`, [DESIGN_NOTES.md](DESIGN_NOTES.md)
> (a base de design — cada feature aqui referencia uma seção lá) e a task inteira. Respeite os
> invariantes (backend autoritativo, determinismo do engine, constantes em `GameConfig`, IDs
> estáveis). O repo `C:\Kaezan\kaezan` é somente leitura.

---

## Critérios para uma feature ser "Fable-tier"

Uma feature merece o modelo premium se atende **3+** destes:

- **Cross-cutting:** toca engine + meta + frontend + contratos (DTOs) ao mesmo tempo.
- **Determinismo-crítica:** roda dentro do `GameWorld` e precisa ser bit-reproduzível por seed.
- **Algoritmicamente difícil:** IA, pathfinding, geração procedural, simulação.
- **Design-ambígua:** exige inventar regras boas e **balancear** (sem spec fechada).
- **Alto raio de explosão:** feita errado, quebra replay, balance ou performance.
- **Composicional:** seu valor vem de interagir com outros sistemas (postura × time × maestria).

Se uma ideia **não** atende isso, ela pertence ao ROADMAP, não aqui.

Ordem sugerida de execução: **F-A → F-E → F-B → F-D → F-C** (valor de produto vs. dependências;
F-C se beneficia de F-A/F-E já existirem para ter o que simular).

---

## F-A — Echo Team: seus waifus lutam juntos (companions IA)

**Owner: Fable 5.** IA de aliado determinística no hot path + anti-bodyblock + balanceamento de
um time de 3 — determinismo-crítico e algoritmicamente difícil. Depende de T-52 (modelo de
classe) e se beneficia de T-53 (IA fiel) já existirem.

**Tier-justificativa:** cross-cutting (engine IA + snapshot + meta de seleção + frontend
render/HUD) · determinismo-crítica · algoritmicamente difícil (IA de aliado boa) · design-ambígua
(balanceamento de um time de 3) · composicional (interage com postura, cards, maestria).
**Origem de design:** [DESIGN_NOTES §4](DESIGN_NOTES.md#4) (Echo Team / companions do Kaezan World).

### Por que é a feature mais importante do projeto

Hoje, depois de puxar 19 waifus no gacha, o jogador usa **uma** por run; o resto fica num menu.
Isto mata o motor do gênero: o desejo de **ver sua coleção em ação**. O Echo Team faz o jogador
levar **1 waifu ativa + 2 companions** (outras waifus suas, controladas pela IA), transformando
cada pull num impacto direto de gameplay. É o elo que liga o **lado gacha** ao **lado roguelike**.

### Design

- **Composição:** o jogador escolhe na Arena Prep / Hunt até 2 companions dentre as waifus que
  **possui** (não a ativa). Time = 1 controlada + 2 IA.
- **Companions são personagens reais:** usam o outfit, elemento, kit e stats (com ascensão) da
  waifu correspondente — não são genéricos. Velvet companion lança Void Chain de verdade.
- **Eficiência de companion:** para não trivializar o conteúdo, companions operam com um
  multiplicador (`GameConfig.CompanionEfficiency`, ~0.6 de dano/HP) — presença + identidade sem
  dobrar a dificuldade. Este número é a alavanca principal de balanceamento.
- **IA de aliado (utility AI, determinística):**
  - **Alvo:** foca o alvo travado do jogador se houver; senão, o mob vivo mais perigoso/próximo
    por uma função de utilidade (distância, HP, se é elite/boss).
  - **Skills:** usa o kit com cooldowns próprios; prioriza ultimate na janela de Boss Posture
    (composição com F-E); usa AoE quando ≥2 alvos agrupados.
  - **Posicionamento:** mantém distância de arma (melee cola, ranged kita para ~range-1), e
    **nunca ocupa o tile para onde o jogador está se movendo** (anti-bodyblock — o atrito #1 de
    companions em ARPG). Espalha-se (não empilha com o outro companion).
  - **Sobrevivência:** companions têm HP; se caem, ficam "down" e revivem ao fim da sala/andar
    (ou após N segundos) — derrota só acontece se a **waifu ativa** cai.
- **Recompensa de coleção:** kills de companion contam para bestiário; XP de run é compartilhado.

### Arquitetura / pontos de toque

- **Engine (`GameWorld`):** `Actor` ganha `Allegiance` (player/ally/enemy) e companions entram
  na mesma lista de atores que hoje só tem player+monstros. Reusar o pipeline de skills
  (`TryCastSkill` precisa generalizar de "o player" para "um ator-aliado"). A IA de aliado é um
  novo `TickAllies()` espelhando `TickMonsters()` mas com alvo = inimigos.
- **Determinismo:** toda decisão de IA usa **apenas** o `Rng` da run e ordem estável de iteração.
  Empates resolvidos por `ActorId`. **Risco alto aqui** — qualquer `Random`/iteração instável
  quebra replay (F-C).
- **Snapshot/DTO:** `SnapshotDto` ganha `allies: List<AllyDto>` (posição, HP, outfit, skills,
  facing). O frontend renderiza aliados como criaturas (reusar `drawOutfit`) + barras de HP em
  cor distinta (azul-aliado), e um mini-roster no HUD com HP dos 3.
- **Meta:** seleção de companions persiste na conta (`AccountState.TeamCompanionIds`); validar
  posse + que não inclui a ativa. Endpoint REST para salvar o time.
- **Frontend:** UI de seleção de time na Arena Prep (3 slots: ativa + 2), render de aliados,
  HUD de time.

### Balanceamento (decisões a tomar e documentar)

- `CompanionEfficiency` inicial ~0.6; ajuste jogando tiers 1-5 com time cheio vs. solo.
- Companions consomem parte do "orçamento de poder" — considere subir levemente o spawn budget
  por tier quando o time está cheio (`GameConfig`), senão o conteúdo fica fácil demais.
- Cards de run aplicam-se só à waifu ativa (decisão sugerida: mantém a ativa como o foco de build).

### Aceite

- Levar Sylwen + Velvet como companions: ambas aparecem, lutam com seus kits reais, não
  bloqueiam o movimento do jogador, e revivem entre salas; só a morte da ativa encerra a run.
- Determinismo: mesma seed + mesmos comandos + mesmo time ⇒ snapshot final idêntico (provar com
  o harness de F-C, ou um teste temporário).
- `dotnet build` + `npx ng build` verdes; run de cada tier jogada com time cheio.

### Riscos

- **Determinismo da IA** é o maior risco — trate como invariante desde a primeira linha.
- **Performance:** 3 atores-aliados + 10 mobs todos rodando utility AI a 10Hz. Medir; a IA de
  aliado pode rodar a cada 2 ticks se necessário (decisão a documentar).
- **Anti-bodyblock** é sutil; reserve tempo para acertar o feel (testar atravessar corredor
  estreito com o time atrás).

---

## F-B — Árvore de Maestria (Eco): progressão persistente por waifu

**Owner: Opus 4.8.** Design-heavy (desenhar e balancear árvores) e cross-cutting, mas fora do
caminho determinístico do tick — não exige o modelo de topo. Assume o modelo de classe (T-52).

**Tier-justificativa:** composicional (entra na fórmula de dano junto com cards+ascensão) ·
design-ambígua (desenhar uma árvore boa e balanceada) · cross-cutting (meta + engine + frontend
de árvore) · alto raio (mexe na fórmula de poder).
**Origem de design:** [DESIGN_NOTES §6](DESIGN_NOTES.md#6) (Mastery / Wheel of Destiny).

### Design

Cada waifu tem uma **árvore de maestria própria**, com **pontos de Eco** ganhos jogando runs com
ela (ativa **ou** como companion). Pontos compram **nodes**; nodes dão bônus **permanentes e
com escolha** — diferente da ascensão (linear) e dos cards (efêmeros).

- **Três camadas de progressão, complementares:**
  - **Run cards** — efêmero, aleatório, dentro da run.
  - **Maestria** — permanente, **com escolha** (build de longo prazo por waifu).
  - **Ascensão** — permanente, linear (poder bruto + addons visuais).
- **Estrutura da árvore:** ~3 ramos por waifu, temáticos ao kit (ex.: Ofensivo / Defensivo /
  Utilitário-elemental). Nodes pequenos (+stats) levam a nodes-chave (modificam uma skill: "Void
  Chain salta +1 alvo", "Whisper Shot aplica slow"). Cap de pontos por waifu (ex.: 30) para a
  árvore ser uma série de **escolhas**, não "pegar tudo".
- **Respec:** permitido pagando uma moeda meta (sem punir experimentação — pilar "less grinding").
- **Fonte de pontos:** X por boss derrotado + Y por nível de run alcançado, com a waifu no time.

### Arquitetura / pontos de toque

- **Meta:** `AccountState.Mastery[waifuId] = { points, spent, nodes[] }`. Catálogo de árvores em
  `Domain/MasteryTrees.cs` (data-driven, por waifu; nodes com id estável `mastery:<waifu>:<node>`,
  efeito tipado: stat flat/percent, skill-mod, posture-bonus).
- **Engine:** a aplicação dos bônus de stat entra no cálculo de poder do `GameWorld` ao iniciar a
  run (junto com ascensão). Os **skill-mods** são o ponto difícil: precisam compor com os shapes
  existentes sem virar switch paralelo — prefira flags/parâmetros lidos pelo dispatch de skill.
- **Frontend:** página/aba "Maestria" na Kaelis com um **renderer de árvore** (nodes, conexões,
  estado comprado/disponível/bloqueado), preview do efeito, gasto/respec de pontos.

### Balanceamento

- O poder total de uma waifu maxada (ascensão + maestria) define o teto de dificuldade dos tiers
  altos — desenhe a árvore **depois** de F-A, porque o time muda a equação de poder.
- Skill-mods devem ser **interessantes** (mudam como você joga), não só `+10% dano` — senão é
  ascensão repintada.

### Aceite

- Cada waifu tem árvore navegável; comprar nodes muda os números na run de forma verificável;
  respec devolve pontos; persiste na conta; skill-mods alteram comportamento observável
  (ex.: Void Chain visivelmente salta mais).

### Riscos

- **Sprawl de design:** 19 árvores é muito conteúdo. Comece com um **template de árvore
  parametrizado por arquétipo** (melee/ranged/caster) e especialize só os nodes-chave por waifu.
- Skill-mods que viram switch paralelo — violam o `CLAUDE.md`; force a passar por parâmetros.

---

## F-C — Determinismo de ouro + Desafio Diário + harness de simulação

**Owner: Fable 5.** É literalmente sobre o invariante de determinismo — caçar fontes sutis de
não-reprodutibilidade e endurecer o coração do engine. Máximo cuidado exigido.

**Tier-justificativa:** determinismo-crítica por definição · alto raio (mexe no coração do
engine) · algoritmicamente sutil (achar fontes de não-determinismo) · habilita produto novo
(desafio diário/leaderboard) e tooling de balance.
**Origem de design:** [DESIGN_NOTES §2](DESIGN_NOTES.md#2) (Dojo + level sync + desafio) e §9
(elemento do dia). Estende ROADMAP T-33 (replay MVP) e T-40 (testes).

### Três entregas que compõem

1. **Hardening de determinismo.** Auditar o `GameWorld` inteiro para garantir reprodutibilidade
   bit-a-bit: streams de RNG separados e nomeados (movimento/IA/loot/spawn — nunca um RNG global
   compartilhado cuja ordem de consumo dependa de detalhes de iteração), **ordem de iteração
   estável** em toda coleção que afeta simulação (listas ordenadas por `ActorId`, nunca
   `Dictionary`/`HashSet` em caminho quente), e zero tempo real no tick (já migrado para
   `SimulationMs`/`TickCount` — verificar que nada regrediu). Documentar as regras num comentário
   de cabeçalho do `GameWorld`.
2. **Replay + verificação.** Gravar `(tick, Command)` por run; persistir
   (`seed, tier, waifu, ascension, team, mastery, commands[]`) + **hash do estado final**.
   Endpoint headless que re-simula e confirma hash idêntico. Isto é o **teste de regressão** que
   protege F-A/F-B/F-E de quebrarem o determinismo.
3. **Desafio Diário + harness.** 
   - **Desafio Diário:** seed derivada da data UTC → **todos jogam a mesma dungeon + mesmos
     modificadores** (ex.: "elemento do dia" buffado, ver §9). Score por tempo/kills/dano;
     leaderboard local agora, base para online depois. Casa com o conceito de **Dojo** (entrar
     direto numa luta padronizada e comparável).
   - **Harness de simulação:** um runner headless que executa **milhares de runs sintéticas**
     (políticas simples de bot: "anda até o inimigo mais próximo e usa skills") variando waifu/
     tier/time, e **emite estatísticas** (win rate, tempo médio, dano por elemento) para tunar
     `GameConfig`. Transforma balanceamento de "achismo" em dados.

### Arquitetura / pontos de toque

- **Engine:** introduzir uma classe `RngStreams` (named streams derivados do seed) e refatorar
  os usos atuais do `Rng` único. Cuidado cirúrgico — qualquer mudança na ordem de consumo muda
  todas as seeds históricas (aceitável agora, mas documente o "reset de seeds").
- **Headless runner:** projeto/console separado ou modo no backend que instancia `GameWorld`
  sem SignalR e roda o tick em loop fechado.
- **Meta/Frontend:** modo "Desafio Diário" na Hunt; tela de score/leaderboard.

### Aceite

- Replay de uma run real reproduz hash idêntico; mudar 1 comando muda o hash.
- Introduzir um `Random` no engine **faz o teste de determinismo falhar** (prova de que pega).
- Desafio Diário: a mesma seed do dia gera a mesma dungeon para duas execuções; score registrado.
- Harness roda ≥1000 runs e emite um CSV/JSON de win rate por tier/waifu.

### Riscos

- **Reset de seeds:** ao reorganizar streams de RNG, todas as seeds antigas mudam de resultado.
  Faça num único commit e documente.
- **Ponto flutuante:** se houver não-determinismo de FP entre máquinas, prefira inteiros/fixed
  em caminhos de simulação críticos (avaliar; provavelmente ok em .NET numa só arquitetura).

---

## F-D — Geração procedural v2: prefabs, pacing e set-pieces

**Owner: Opus 4.8 → Fable 5.** Algorítmico com garantias (conectividade, pacing) e rodando no
seed (determinístico). Opus dá conta do desenho; escale para Fable se o encaixe de prefabs +
garantias formais ficarem espinhosos.

**Tier-justificativa:** algoritmicamente difícil (geração com garantias + intenção de design) ·
determinismo-crítica (roda no seed) · design-ambígua (o que é uma dungeon "divertida"?) · alto
valor de variedade.
**Origem de design:** dungeons clássicas do Tibia + pilar "conteúdo em camadas"
([DESIGN_NOTES §1](DESIGN_NOTES.md#1)).

### O problema com o gerador atual

`DungeonGenerator` faz salas-retângulo + corredores-L com papéis simples. Funciona, mas as
dungeons são **uniformes e sem intenção**: toda sala é igual, o spawn é budget plano, não há
set-pieces nem ritmo. Falta o que torna roguelikes rejogáveis: **layouts com propósito**.

### Design

- **Prefabs (salas-template):** uma biblioteca de salas desenhadas (data-driven), cada uma com
  marcadores de spawn, POIs e conexões — ex.: "vault do tesouro" (muito loot, mobs guardiões),
  "antessala do boss", "den de elites", "sala-armadilha de emboscada", "santuário" (cura/buff).
  O gerador escolhe e encaixa prefabs garantindo conectividade.
- **Pacing de dificuldade:** o orçamento de spawn **varia ao longo do andar** — leve perto da
  entrada, picos nas salas-chave, alívio antes do boss. Curva, não constante.
- **Set-pieces garantidos:** todo andar tem ≥1 momento memorável (vault OU elite den OU
  mini-evento). O fundo do andar 2 é sempre uma antessala → arena de boss.
- **Theming por bioma:** compõe com ROADMAP T-12 (tilesets por tier) — o gerador escolhe prefabs
  e decoração coerentes com o bioma.
- **Garantias verificáveis:** sempre conexo (entrada alcança escada/boss), sempre transitável
  (set-pieces nunca bloqueiam o caminho crítico), dificuldade dentro de uma faixa.

### Arquitetura / pontos de toque

- **Engine:** reescrever/estender `DungeonGenerator` com um passo de **layout** (grafo de salas
  por prefab) + passo de **encaixe** (resolver conexões) + passo de **povoamento** (spawn por
  pacing) + passo de **pintura** (tiles/bioma). Tudo no `Rng` do seed.
- **Dados:** `Domain/RoomPrefabs.cs` (ou JSON) — templates como grades de tags
  (`floor/wall/spawn/poi/connector`). Ferramenta opcional de preview (capturar via hook do
  renderer) para inspecionar prefabs.
- **Frontend:** nenhuma mudança grande — o `MapDto` já carrega ground/wall/decor/pois; talvez
  ícones distintos de POI por tipo de sala.

### Aceite

- 100 seeds geram dungeons **conexas**, com pelo menos 1 set-piece por andar, pacing perceptível
  (entrada calma → clímax), e nenhum caminho crítico bloqueado (BFS prova).
- Jogar 5 runs seguidas "sente" variedade (salas diferentes, momentos diferentes), não repetição.

### Riscos

- **Encaixe de prefabs** pode falhar (sem conexão válida) — precisa de fallback (degradar para o
  gerador atual de retângulos numa região) e retry com budget de tentativas.
- Não exagerar: o objetivo é variedade com garantias, não um Spelunky. Escopo apertado.

---

## F-E — Postura completa + sistema de reações elementais

**Owner: Opus 4.8** (Fable 5 se feita junto com T-52). Composicional e ancorada pelo MVP do
ROADMAP T-31; Opus dá conta. Se for implementada na mesma leva que a refundação de classes
(T-52), o acoplamento elemento×stance×postura justifica subir para Fable 5.

**Tier-justificativa:** composicional (amarra elemento × postura × cards × ultimate × time) ·
design-ambígua (matriz de reações + tuning de janelas) · cross-cutting (engine + DTO + HUD/FX).
**Origem de design:** [DESIGN_NOTES §3](DESIGN_NOTES.md#3) (Boss Posture / Echo Break). Estende o
MVP do ROADMAP T-31 para o sistema completo + camada de reações.

### Duas metades

1. **Postura completa (além do T-31 MVP).** Ciclos com multiplicador crescente
   (`2.5x → 3.5x → 5x → 6.5x`), bônus de `% maxHP` por hit com cooldown interno por ator,
   **fraqueza elemental quebra postura mais rápido**, e decaimento quando sem pressão. A janela
   de Echo Break vira o **momento de payoff de burst** da luta (guardar ultimate para a janela).
   Compõe com F-A: a IA dos companions prioriza ultimate na janela.
2. **Reações elementais (novo).** Uma **matriz de reações** entre o elemento do dano e o estado
   elemental do alvo, inspirada no que faz combate elemental ser interessante: aplicar um
   elemento "marca" o alvo; um segundo elemento dispara uma **reação** (ex.: Fogo sobre alvo
   Gelado = "Estilhaço" com dano em área; Energia sobre Molhado = stun; Terra sobre Queimado =
   detona DoT). Faz a **escolha de elemento do time** (e do elemento do dia) ter peso tático real,
   e dá sinergia entre waifus de elementos diferentes no Echo Team.

### Arquitetura / pontos de toque

- **Engine:** estado por ator (`PostureCurrent/Max/Cycle/StaggerUntil` no boss; `ElementMark`
  nos alvos). Aplicação no caminho de dano (`DealDamageToMonster`), respeitando determinismo
  (ordem estável, sem alocar no caminho quente). Matriz de reações data-driven em
  `Domain/ElementReactions.cs`.
- **DTO/Snapshot:** `bossPosture/Max/Cycle/staggered` (já previsto em T-31) + marca elemental por
  mob para o HUD/FX.
- **Frontend:** barra de postura sob o HP do boss (pisca perto do break; "QUEBRADO!" no stagger);
  FX de reação (reusar effects do Tibia já extraídos — explosão de gelo, etc.); ícone de marca
  elemental sobre o mob.

### Balanceamento

- Janela de stagger e multiplicadores definem quanto a luta de boss premia burst — tune com o
  harness de F-C.
- Reações não podem virar o único caminho viável; devem ser **bônus por jogar elementos**, não
  obrigatórias. Tune o dano de reação como uma fração do hit, não um multiplicador explosivo.

### Aceite

- Boss tem barra de postura; pressão quebra; quebra abre janela de burst com multiplicador por
  ciclo; decaimento impede encher de graça.
- Aplicar dois elementos em sequência dispara uma reação visível com FX; a matriz é data-driven.
- Composição: levar um time de elementos complementares (F-A) cria reações de forma observável.

### Riscos

- **Complexidade emergente:** postura × reações × time × cards pode ficar incontrolável de
  balancear. Entregue postura completa primeiro, **meça** (F-C), depois ligue reações.
- FX-spam: muitas reações podem poluir a tela — limite e priorize feedback legível.

---

## Como propor uma nova feature Fable-tier

1. Confirme que atende **3+** dos critérios no topo deste doc (senão é ROADMAP).
2. Ancore numa seção do [DESIGN_NOTES.md](DESIGN_NOTES.md) (ou adicione uma, se for ideia nova).
3. Escreva no formato acima: tier-justificativa · origem · design · arquitetura/toques ·
   balanceamento · aceite · riscos.
4. Identifique dependências (ex.: balanceamento depende de F-A existir; tuning depende de F-C).
