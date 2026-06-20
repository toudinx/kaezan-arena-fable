# Roadmap — Refatoração da Gameplay Base

> **Como usar este arquivo.** Cada `G-NN` abaixo é uma unidade de trabalho **auto-contida**: o
> agente que executa começa "frio", então o prompt já traz o contexto que ele precisa. Você dispara
> com **"implemente o prompt G-NN do `docs/roadmap_refactor_gameplay.md`"** e o agente faz o resto.
>
> Cada prompt declara: **Modelo · Effort · Skill · Depende de · Aceite · Verificação.** Execute em
> ordem — há dependências reais (o sistema de cartas precede reroll, cadência e baú-altar).
>
> **Não confundir com:** `docs/ROADMAP.md` (Codex, tasks pequenas), `docs/FABLE_TRACK.md` (Fable,
> features grandes do engine), `docs/FRONTEND_REMAP.md` (remap visual) e
> `docs/roadmap_refactor_kaelis.md` (refundação do roster — **já concluído**). Este arquivo é a
> refundação da **gameplay base**: combate watchable, cartas, cadência, mapas, mobs autorais, baús e
> automode. Toca **engine + frontend de combate** (`Engine`, `Domain`, `Hubs`, `core/`, `pages/game`).

---

## Origem desta trilha

O dono do projeto gravou a gameplay/UI e levou ao Opus 4.8 (via site). A tese que voltou — e que
amarra todas as decisões abaixo:

> **A graça não é controlar a Kaeli, é ser o autor da build e assistir ela executar.**

Jogador de gacha quer evoluir conta, não apertar botão. Logo o **automode é o modo padrão e o pilar
de design**, não muleta. A "gameplay" precisa ser **gostosa de assistir e legível**, e a agência do
jogador migra para três lugares:

1. **Montar a build** (cartas).
2. **Configurar a tática** (painel HELPER → gambit).
3. **Otimizar a conta** entre runs (Kaeli, gear, gacha, farm).

O combate **não precisa ficar difícil de jogar** — precisa ficar **legível, suculento e expressivo**.
A decisão mora em **cartas + táticas + bifurcações**. É um autobattler de build gostoso de assistir,
não um ARPG de skill.

---

## Modelos & quando usar

| Modelo | Papel | Effort típico | Por quê |
|---|---|---|---|
| **Claude Code Opus 4.8** | Feel do combate, sistema de cartas/keywords, arquétipos de mob, gambit, balance, design de bioma/mapa | `high` / `medium` | É onde mora a decisão de game design ("gosto") + os invariantes de engine (determinismo). Errar aqui cascateia. Vale o modelo premium. |
| **GPT-5.5 (Codex)** | Conversões bounded com regra já fechada: docs, reroll/ban com UI especificada, farm/auto-repeat, limpeza de texto | `low` / `medium` | Tarefas com regra explícita e padrão a seguir. Barato e rápido. |

- Use **`use context7`** ao consultar API de biblioteca (ASP.NET Core, SignalR, EF Core, Angular
  signals) nos prompts que tocam essas APIs.
- Nenhum prompt desta trilha depende de skill instalada. As decisões de design vivem no próprio
  prompt e neste documento — tanto Opus quanto Codex leem daqui.

---

## Invariantes inegociáveis (todo prompt respeita)

- **Backend autoritativo.** Frontend nunca simula combate/movimento — só interpola e renderiza. Todo
  o "juice" é **EventDto + interpolação client-side**, nunca simulação no front.
- **Determinismo do engine.** `GameWorld` usa só o `Rng` da run (xorshift seedado). Nunca `Random`,
  `DateTime.Now`, ou iteração de coleção instável dentro do tick — isso vale para **geração de sala,
  escolha de carta, seleção de alvo do gambit e salto de keyword** (desempate por id estável).
- **Todas as constantes de simulação em `Domain/GameConfig.cs`.** Nada de hardcode no tick.
- **Skills e mobs data-driven por shape/arquétipo.** Skills por *shape*
  (`single|beam|nova|area|cone|chain|ring|field|barrage|summon|buff`); mobs por
  `MonsterBehaviorProfile`. Para algo novo, **parametrize/estenda o existente** — não crie dispatch
  paralelo no engine.
- **IDs estáveis** persistidos merecem respeito: `card:*`, `waifu:*`, nomes de espécies do Tibia, e
  os novos `echo:*` / `tactic:*`. Não renomear o que persiste; mudança de ID exige migração no
  `AccountSanitizer`.
- `dotnet build` (backend) e `npx ng build` (frontend) passam sem erro ao fim de cada prompt que
  tocar o respectivo lado.

---

## O que já existe (corrige suposições do feedback)

O feedback assumiu "do zero" em vários pontos onde **já há seam pronto**. Saber disso muda o escopo:

- **Posture/Break já existe** como *Echo Break*. `Actor.Posture/PostureMax/PostureCycle/Stagger*` em
  `Engine/GameWorld.cs`; `AddPosture()`, `TriggerEchoBreak()` (emite effect 35 + texto "QUEBRADO!"),
  `TickPostureDecay()`; HUD em `pages/game/game.ts` (barra de postura + stagger). → **G-03 eleva isso
  a clímax, não recria.**
- **8 arquétipos de mob data-driven** já existem: `MonsterBehaviorProfile` em `Domain/GameConfig.cs`
  (bruiser, skirmisher, ranger, artillery, breather, controller, support, juggernaut), resolvidos por
  `Domain/MonsterAuthoring.cs`. → **G-08 adiciona arquétipos novos + criaturas autorais reskinadas;
  não reescreve o sistema.**
- **Roles de sala já existem** (`Engine/DungeonGenerator.cs:85-108`: entry/mob/treasure/ladder/boss)
  e **5 biomas** em `Domain/Biomes.cs`. → **G-06/G-07 expandem** (santuário de Eco, bifurcação,
  ícones, color-grade por bioma); o grafo de salas já é a base.
- **Helper/AutoHelper já é o seam do gambit.** `AutoHelperSettingsDto` (targeting/skills/ultimate/
  targetPreference/movementMode), `TickAutoHelper()` em `GameWorld.cs`, comando `SetAutoHelper` no
  `Hubs/GameHub.cs`. → **G-10 transforma em regras condição→ação.**
- **Cartas são planas.** `Domain/Cards.cs` → `CardDef(Id, Name, Description, Stat, Value)`, 13 cartas,
  empilháveis até 3 via `CardValue()` em `GameWorld.cs`; oferecidas **só no level-up** via
  `OfferCards()` dentro de `GainXp()`. **Sem raridade, sem tags/sinergia, sem reroll/ban.** → maior
  trabalho novo (G-04).
- **Baús são loot solto.** `GameWorld.TryInteract()` (~`:2686`) dá ouro + 2 itens equipáveis, 25% de
  emboscada. Não conectado a cartas/gear. → **G-09 vira altar de Eco / loja da run.**
- **Equipamento existe mas é frouxo no loop.** `Domain/Equipment.cs` agrega stats passados ao
  `GameWorld` no init via `GameHub.JoinRun`; **não há conceito de "material de gear"**. A tela por
  tier em `pages/kaelis/kaelis.ts` é onde G-09 pluga os drops de material.
- **Sem farm/auto-repeat nem progressão offline** hoje (cada run exige seleção manual de tier).

---

## Tese & Decisões Fechadas

- **Automode é o modo padrão.** Controle manual é opt-in para momentos de alto valor (escolher
  carta, escolher bifurcação, soltar a ult no break).
- **Cartas em 3 tiers:**
  - **Comum (status):** os atuais (CDR, dano, life steal, atk speed). "Chatos" de propósito,
    stackáveis, suavizam a curva.
  - **Raro (mecânica/skill):** alteram uma skill ou o auto ("a cada 3 hits, golpe extra", "Maldição
    vira em área", "Pesadelo reduz postura").
  - **Eco / Lendária (define a run):** mudam a win-condition.
- **Sinergia por keywords/tags** (Maldição, Postura, Eco, Espectro…), não cartas isoladas. Uma
  lendária "duplique todos os efeitos de Maldição" só importa se você vinha empilhando Maldição — é o
  que faz a build parecer autoral e sobrevive ao automode (a escolha é a gameplay).
- **Ecos lendários são por Kaeli** (decisão do dono): puxar uma Kaeli nova = espaço de build novo =
  motivo pra gastar no gacha. Liga combate ao loop de conta diretamente.
- **Reroll + banir** estilo Hades: reroll dá decisão mesmo no auto; banir tira do pool a carta que
  você nunca quer.
- **Cadência:** parar de dar carta a cada level-up. Level-up dá **status pequeno automático** (mantém
  o gotejar de dopamina); escolhas pesadas reservadas a **beats fixos** (fim de elite, fim de boss,
  **sala Santuário de Eco** no minimapa). Alvo **~6-9 escolhas/run**, não 20, **escalando a raridade
  dentro da run** (começo monta a engine, fim define).
- **Mapas:** corredor → **grafo de salas** com **tipo por sala** (combate, elite, tesouro, eco,
  evento/risco, miniboss, boss) + **ícones no minimapa** + **bifurcação risco/recompensa** +
  **bioma por estrato** com color-grade/luz/partículas/névoa. Legibilidade > labirinto (é assistido).
- **Mobs:** **taxonomia (arquétipo) primeiro, skin autoral Kaezan depois**; roster desenhado para
  **interagir com as keywords** das cartas (ex. mob resistente a Maldição força variedade de build).
- **Baús = altar de Eco / loja da run** (oferece carta, ou reroll, ou comprar com ouro) + baús
  **amaldiçoados** + **mimics** + **material de gear** ligado à tela de equipamento.
- **Automode = feature-título:** painel HELPER vira **editor de táticas estilo gambit (FF12)**; +
  **farm/auto-repeat de tier** + **progressão offline** (o "evoluir conta" literal).
- **Echo Break já existe no engine** — é **elevado a clímax** da run, não recriado.

---

## Mapa de prompts (escopo)

| Prompt | Tema | Modelo | Effort | Depende de | Onda |
|---|---|---|---|---|---|
| G-01 | Congelar a tese (README + DESIGN_NOTES) | GPT-5.5 (Codex) | low | — | 1 |
| G-02 | Juice do combate (hit-stop, shake, números c/ peso, proc text, flash, dissolve) | Opus 4.8 | high | — | 1 |
| G-03 | Legibilidade do helper + Echo Break como clímax | Opus 4.8 | medium | G-02 | 2 |
| G-04 | Tiers de carta + keywords/sinergia + pool de Eco por Kaeli ⭐ fundação | Opus 4.8 | high | — | 2 |
| G-05 | Reroll + banir (Hades-style) | GPT-5.5 (Codex) | medium | G-04 | 3 |
| G-06 | Cadência: beats fixos, level-up = status auto, ~6-9 escolhas/run | Opus 4.8 | high | G-04 | 4 |
| G-07 | Grafo de salas + tipos + minimapa + bifurcação + color-grade por bioma | Opus 4.8 | high | G-06 (⚠ DungeonGenerator) | 5 |
| G-08 | Arquétipos novos de mob + criaturas autorais Kaezan | Opus 4.8 | high | G-04* | 3 |
| G-09 | Baú = altar de Eco / loja da run + amaldiçoados + mimics + material de gear | Opus 4.8 | high | G-04, G-05, G-07 | 6 |
| G-10 | Painel HELPER → editor de táticas gambit (FF12), presets por Kaeli | Opus 4.8 | high | — | 4 |
| G-11 | Farm/auto-repeat de tier + progressão offline | GPT-5.5 (Codex) | medium | — | 5 |
| G-12 | Balance e verificação ponta-a-ponta | Opus 4.8 | medium | G-02–G-11 | 7 |

> Pratos cheios (G-02, G-04, G-06, G-07, G-08, G-09, G-10) no **Opus 4.8 high** — feel do combate +
> design de cartas/mobs/mapas + invariantes de engine. Conversões bounded (G-01, G-05, G-11) no
> **GPT-5.5**. `*` G-08 *soft-depende* de G-04: pode rodar antes referenciando as tags planejadas.

---

## Execução paralela (ondas) + conflitos

**Regra de ouro** (igual à trilha das Kaelis): dois prompts só rodam em paralelo se (a) as
dependências dos dois já fecharam **e** (b) eles **não editam o mesmo arquivo**. O gargalo real desta
trilha são três arquivos: `Engine/GameWorld.cs` (G-02/03/04/05/06/09/10), `Engine/DungeonGenerator.cs`
(G-06/07/09) e `pages/game/game.ts` (G-02/03/05/07/10) — boa parte do backend **serializa**.
Casamento natural: **1 Opus + 1 Codex por onda**, em regiões disjuntas.

```
Onda 1  G-02 (Opus · juice: renderer.ts + Emit)         ‖  G-01 (Codex · docs)
Onda 2  G-04 (Opus · cards backend: Cards.cs/GameWorld) ‖  G-03 (Opus · frontend: renderer/game.ts)
Onda 3  G-08 (Opus · mobs: config/MonsterAuthoring)     ‖  G-05 (Codex · reroll/ban: GameWorld cmd + overlay)
Onda 4  G-06 (Opus · cadência: GainXp/DungeonGenerator) ‖  G-10 (Opus · gambit: TickAutoHelper/types)
Onda 5  G-07 (Opus · mapas: DungeonGenerator/Biomes)    ‖  G-11 (Codex · farm/offline: RunManager/Meta)
Onda 6  G-09 (Opus · baú-altar; precisa G-04/05/07)     — solo
Onda 7  G-12 (verificação final)                        — solo
```

**Pares que forçam sequencial (não paralelize estes):**
- **G-02 × G-03** — ambos em `core/renderer.ts`. G-03 assenta sobre a camada de juice.
- **G-06 × G-07 × G-09** — todos em `DungeonGenerator.cs`. Ordem: **G-06 → G-07 → G-09**.
- **G-04 × G-05 × G-09** — cadeia de cartas: tiers → reroll → o altar reusa as duas.
- **G-06 / G-10 em `game.ts`** — regiões diferentes (overlay de carta vs painel helper); se rodarem
  juntos, atenção ao merge no template.

**Ganho:** 12 passos sequenciais → **7 ondas**. Caminho crítico: G-04 → (G-05/G-06) → G-07 → G-09 →
G-12.

---

# G-01 — Congelar a Tese

Resumo: _(preencher ao concluir)_

- **Modelo:** GPT-5.5 (Codex) · **Effort:** low · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** G-02 (Onda 1)

**Objetivo:** registrar nos docs a tese "autor da build assiste a execução", os 3 pilares, os 3 tiers
de carta e o automode como padrão. Tarefa só de texto/documentação — **não muda gameplay**.

**Arquivos prováveis:** `README.md`, `docs/DESIGN_NOTES.md`, este documento.

**Tarefas:**
- Descrever no README a direção: autobattler de build gostoso de assistir; automode é o modo padrão;
  agência migra para build (cartas) + tática (gambit) + conta (gear/gacha/farm).
- Registrar em `DESIGN_NOTES.md` os 3 tiers de carta (Comum/status · Raro/mecânica · Eco/lendária por
  Kaeli) e a ideia de keywords/sinergia.
- Deixar claro que combate quer ser **legível, suculento e expressivo**, não difícil de jogar.

**Aceite:**
- README e DESIGN_NOTES descrevem o norte novo.
- Nenhuma mudança de gameplay.

**Verificação:** revisão de texto; nenhum build necessário (só docs).

---

# G-02 — Juice do Combate  ⭐ maior ROI imediato

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** G-01 (Onda 1)

**Objetivo:** o combate hoje é estático — dano sai, mob morre, sem peso. O maior ROI imediato não é
mecânica nova, é **feel**. Dar peso e feedback ao impacto **sem mudar nenhuma regra** — só EventDto
novo + interpolação client-side.

**Arquivos prováveis:** `frontend/src/app/core/renderer.ts` (camada de FX, textos flutuantes,
corpos), `frontend/src/app/pages/game/game.ts` (canvas/câmera), e — só se precisar de kinds de evento
novos — `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (`Emit()` ~`:2894`) +
`Engine/GameDtos.cs` (`EventDto`).

**Seam existente (REUTILIZE):** o engine já emite `damage` (com `crit`), `effect`, `projectile`,
`text`, `heal`, `death`, `loot`, `levelup`, `voice` por tick; o `renderer.ts` já tem array de
`texts`, `effects`, `projectiles`, `corpses` e já faz fade de corpo e arco de loot. Crit já emite
effect 173. Estenda essa máquina; não a substitua.

**Tarefas (todas client-side ou EventDto novo, nada de simular regra no front):**
- **Hit-stop** no impacto (micro-pausa/scale no alvo ao receber dano grande).
- **Screen-shake** proporcional nos golpes grandes (câmera oscila por poucos frames).
- **Números de dano com peso:** crítico maior e dourado; escala/pop por magnitude; outline.
- **Texto flutuante de proc** ("MALDIÇÃO!", "QUEBRA!", "EXECUÇÃO!") — pode reusar o kind `text` ou um
  kind novo `proc` com cor/tamanho próprios.
- **Flash de hit** no sprite ao receber dano.
- **Dissolve na morte** (hoje só faz fade linear do corpo) — dissolução por pixels/alpha animada.
- Se introduzir kinds novos (`shake`/`proc`/`dissolve`), documentar em `EventDto` e manter o engine
  **determinístico** (o front só reage; a intensidade vem do dado, não de `Math.random` que afete
  regra).

**Aceite:**
- Combate tem peso visível: impacto, crítico e morte "sentem".
- Determinismo intacto (backend inalterado em regra; só novos eventos cosméticos).
- `npx ng build` (e `dotnet build` se tocar o backend) limpos.

**Verificação:** builds limpos + preview de uma run mostrando hit-stop, shake, números dourados de
crit, proc text e dissolve. Console limpo.

---

# G-03 — Legibilidade do Helper + Echo Break como Clímax

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** G-02 · **Paraleliza com:** G-04 (Onda 2) — ⚠ **não** com G-02 (conflito em `renderer.ts`)

**Objetivo:** transformar "o bot tá jogando" (opaco) em "minha build está executando a lógica que eu
montei" (satisfatório). Para isso, **legibilidade do helper** + elevar o **Echo Break existente** a
momento da run.

**Arquivos prováveis:** `frontend/src/app/core/renderer.ts`, `frontend/src/app/pages/game/game.ts`.
O backend já emite o break (effect 35 + texto "QUEBRADO!" em `TriggerEchoBreak`) e já expõe
`bossStaggered`/`bossPostureCycle` no `RunStateDto` — **reusar**, não reimplementar.

**Tarefas:**
- **Destacar o alvo atual** do helper (já existe red box em `renderer.ts:360` — torná-lo mais legível/
  animado).
- **Linha de intenção:** linha do player até o alvo aimed quando uma skill vai sair.
- **Telegraph de skill:** preview do shape que vai disparar (cone/círculo/beam) piscando antes do
  cast.
- **Echo Break = clímax:** ao `bossStaggered` virar true, disparar slow-mo breve + flash + sinalizar
  a **janela de dano** (a barra de postura já pulsa; somar FX dramático + indicação de "BREAK ×N").
- Tudo client-side reagindo ao snapshot; nenhuma regra nova no engine.

**Aceite:**
- Dá pra "ler" o que o helper vai fazer (alvo, intenção, skill).
- O break é um momento visível e dramático, não uma mudança silenciosa de número.
- `npx ng build` limpo (e `dotnet build` se tocar o backend).

**Verificação:** builds + preview de uma run até o boss, confirmando linha de intenção, telegraph e o
break com slow-mo/flash. Console limpo.

---

# G-04 — Tiers de Carta + Keywords + Eco por Kaeli  ⭐ fundação de build

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ APIs) · **Depende de:** — · **Paraleliza com:** G-03 (Onda 2)

**Objetivo:** formalizar a intuição dos 3 tiers e fazer a build parecer **autoral** via keywords. Esta
é a fundação — G-05 (reroll), G-06 (cadência) e G-09 (baú-altar) assentam sobre a forma que esta
define.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Domain/Cards.cs`
(`CardDef` ganha `Rarity` + `Tags[]` + efeito mais rico que um único `Stat`),
`backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (`CardValue()` ~`:684`, aplicação dos efeitos,
`OfferCards()`), `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (constantes),
`frontend/src/app/pages/game/game.ts` + `core/types.ts` (exibir tier/cor/tag no overlay de carta).

**Seam existente (REUTILIZE):** `CardDef(Id, Name, Description, Stat, Value)` e a aplicação
multiplicativa via `CardValue(stat)` já existem; as 13 cartas atuais viram o tier **Comum**. Estenda
o record (rarity + tags + efeito), não troque o pipeline.

**Modelo de tiers (mapeado de Hades/Slay the Spire/ARAM):**
- **Comum (status):** os atuais (CDR, dano, life steal, atk speed). Stackáveis, suavizam a curva.
- **Raro (mecânica/skill):** alteram uma skill ou o auto. Ex.: "a cada 3 hits, golpe extra",
  "Maldição vira em área", "Pesadelo reduz postura".
- **Eco / Lendária (define a run), por Kaeli:** mudam a win-condition. Ex. perfil Necromante/Morte da
  Velvet: "cada inimigo morto invoca um espectro (máx 5)", "você não cura — ganha escudo igual ao dano
  da Maldição", "skills sem cooldown, mas custam vida".

**Princípios obrigatórios:**
- **Keywords/sinergia, não cartas isoladas.** Cartas referenciam **tags** (Maldição, Postura, Eco,
  Espectro…). Uma lendária "duplique todos os efeitos de Maldição" só importa se você vinha
  empilhando Maldição. Tags são a cola entre cartas, traits (K-04 das Kaelis) e mobs (G-08).
- **Eco por Kaeli.** Os Ecos lendários são temáticos da Kaeli ativa; o pool de Eco é filtrado pela
  Kaeli da run. IDs `echo:<kaeli>:*` estáveis. Curar ~3-5 Ecos por Kaeli (7 Kaelis).
- **Determinístico.** Seleção/oferta de carta só via `Rng` da run; desempate por id estável.
- **Constantes em `GameConfig.cs`** (pesos por raridade, máx de stacks por tier, etc.).

**Tarefas:**
- Estender `CardDef` com `Rarity` (`common|rare|echo`) e `Tags[]`; suportar efeito além de um único
  `Stat` (mecânica/gatilho), mantendo retrocompatível com as comuns atuais.
- Implementar o pool de Eco por Kaeli e a filtragem na oferta.
- Wire das tags no engine para que cartas/traits/mobs possam referenciá-las.
- Exibir tier (cor/badge) e tags no overlay de carta no front.

**Aceite:**
- 3 tiers funcionais; comuns preservadas.
- Tags referenciáveis e com sinergia real (ao menos um par carta-comum × eco que só vale junto).
- Eco filtra pela Kaeli ativa.
- Determinístico; constantes em `GameConfig.cs`.
- `dotnet build` + `npx ng build` limpos.

**Verificação:** builds + run montando uma build de tag (ex. empilhar Maldição com a Velvet e pegar o
Eco que a duplica), confirmando o efeito e a oferta filtrada por Kaeli.

---

# G-05 — Reroll + Banir (Hades-style)

Resumo: _(preencher ao concluir)_

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** G-04 · **Paraleliza com:** G-08 (Onda 3) — ⚠ **não** com G-09 (cadeia de cartas em `recruit/game.ts`/`GameWorld`)

**Objetivo:** dar decisão mesmo no automode. Reroll re-sorteia a oferta; banir remove do pool a carta
que você nunca quer. Regra fechada — tarefa bounded.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (comando
`RerollCards`, `_bannedCards`), `backend/src/KaezanArenaFable.Api/Hubs/GameHub.cs`,
`frontend/src/app/core/game-client.service.ts`, `frontend/src/app/pages/game/game.ts` (overlay de
carta), `core/types.ts`.

**Regras:**
- **Reroll:** re-sorteia a oferta atual respeitando bans e a raridade do beat (G-06). Quantidade de
  rerolls vem de `GameConfig.cs` (ex. N por run, ou ganhos via baú).
- **Banir:** remove a carta do pool pelo resto da run; persiste no estado da run.
- Determinístico via `Rng` da run.
- UI: botões de reroll (com contador) e banir no overlay; estados claros.

**Aceite:**
- Reroll embaralha respeitando bans e raridade do beat.
- Banir persiste na run e tira a carta de ofertas futuras.
- `dotnet build` + `npx ng build` limpos; console limpo.

**Verificação:** builds + preview do fluxo de oferta: rerolar, banir, confirmar que a banida não
volta.

---

# G-06 — Cadência: Beats Fixos

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ APIs) · **Depende de:** G-04 · **Paraleliza com:** G-10 (Onda 4) — ⚠ **não** com G-07/G-09 (conflito em `DungeonGenerator.cs`)

**Objetivo:** tem carta demais. Parar de dar carta a cada level-up. Level-up dá **status pequeno
automático** (mantém o gotejar); escolhas pesadas ficam em **beats fixos e antecipados**. Alvo
**~6-9 escolhas/run**, escalando raridade ao longo da run (começo monta a engine, fim define).

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (`GainXp()` /
`OfferCards()` ~`:2090`), `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (novo role
"santuário de eco" + posição no minimapa), `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`.

**Tarefas:**
- Level-up → aplicar **status pequeno automático** (sem abrir tela de escolha).
- Reservar a **escolha pesada** a beats: **fim de elite**, **fim de boss**, e **sala Santuário de
  Eco** (novo role de sala, sinalizado no minimapa).
- **Escalar raridade dentro da run:** começo favorece comum/raro; fim favorece raro/eco (pesos em
  `GameConfig.cs`).
- Garantir o alvo de **~6-9 escolhas** numa run típica de tier 1.
- Determinístico (geração da sala santuário e sorteio respeitam o `Rng` da run).

**Aceite:**
- Número de escolhas cai e vira antecipável (o jogador sabe onde a próxima vem).
- Level-up dá status automático; nenhuma tela de escolha por level.
- Raridade escala ao longo da run.
- `dotnet build` + `npx ng build` limpos.

**Verificação:** builds + run de tier 1 contando as escolhas (~6-9) e confirmando o santuário no
minimapa + a escalada de raridade.

---

# G-07 — Mapas: Grafo de Salas + Bioma por Estrato

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ APIs) · **Depende de:** G-06 · **Paraleliza com:** G-11 (Onda 5) — ⚠ **não** com G-06/G-09 (conflito em `DungeonGenerator.cs`)

**Objetivo:** hoje é corredor de tile vermelho. Virar **grafo de salas** com **tipo por sala** e
minimapa legível, bifurcação risco/recompensa, e cada estrato visualmente distinto — barato, reusando
tiles. Legibilidade > labirinto (é assistido).

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs`,
`backend/src/KaezanArenaFable.Api/Domain/Biomes.cs`, `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`,
`frontend/src/app/core/renderer.ts` (minimapa + camada de luz), `frontend/src/app/pages/game/game.ts`.

**Seam existente (REUTILIZE):** `DungeonGenerator` já gera salas com roles (entry/mob/treasure/
ladder/boss) e corredores L; `Biomes.cs` já tem os 5 estratos (Caverna/Forte/Cripta/Covil/Abismo).
Expanda; não reescreva o gerador.

**Tarefas:**
- **Tipo por sala** explícito: combate, elite, tesouro, **eco** (santuário, de G-06), evento/risco,
  miniboss, boss — com **ícones no minimapa**, virando rota antecipável.
- **Bifurcação risco/recompensa** nos cruzamentos ("sala de elite, mais loot" vs "segura") — decisão
  que sobrevive ao auto: o jogador escolhe o caminho, o helper anda.
- **Bioma por estrato:** color-grade forte + camada de luz + partículas + névoa distintas por
  estrato, mesmo reusando tiles. Um color grade + luz por bioma já transforma o visual.
- Determinismo do gerador preservado (só `Rng` da run).

**Aceite:**
- Minimapa mostra tipos de sala como ícones; rota é antecipável.
- Há bifurcação real risco/recompensa.
- Cada estrato é visualmente distinto (color-grade/luz/partículas/névoa).
- `dotnet build` + `npx ng build` limpos.

**Verificação:** builds + screenshots dos estratos confirmando paletas distintas + minimapa com
ícones + um cruzamento com bifurcação.

---

# G-08 — Arquétipos Novos de Mob + Criaturas Autorais Kaezan

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma · **Depende de:** G-04* (soft) · **Paraleliza com:** G-05 (Onda 3)

**Objetivo:** **taxonomia primeiro, skin depois.** Documentar mobs como **arquétipos de comportamento
reutilizáveis** e criar criaturas originais do Kaezan como **instâncias temáticas** reskinadas,
desenhadas para interagir com as keywords das cartas.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`
(`MonsterBehaviorProfile` novos), `backend/src/KaezanArenaFable.Api/Domain/MonsterAuthoring.cs`,
`tools/convert-monsters/config.json` (+ re-rodar `node convert.mjs`) e os rosters por tier
(`CommonMobs[]`/`EliteMobs[]` em `GameConfig.cs`).

**Seam existente (REUTILIZE):** já há 8 arquétipos (bruiser, skirmisher, ranger, artillery, breather,
controller, support, juggernaut) e o resolver `MonsterAuthoring`. Adicione perfis novos no mesmo
formato; não crie dispatch paralelo.

**Tarefas:**
- **Arquétipos novos** (faltantes vs. o feedback): **invocador**, **chargador**, **swarm**,
  **explosivo suicida**, **tanque de postura**, **suporte/escudeiro**.
- **Criaturas autorais Kaezan** como instâncias reskinadas: ex. "Necromante Caído" (invocador
  espelhando a Velvet num miniboss); "Ecoídes" (swarm); "Portador de Eco" (suporte que escuda os
  outros e força o helper a priorizar).
- **Interação com keywords:** desenhar o roster para criar decisão de build (ex. mob resistente a
  Maldição força variedade) — usar as tags de G-04.
- Como dado/config, o mesmo comportamento **reskina entre biomas**. Arte autoral final fica em
  "Depois".

**Aceite:**
- Arquétipos novos são data-driven (perfis em `GameConfig.cs`, resolvidos por `MonsterAuthoring`).
- Há criaturas autorais por bioma instanciando os arquétipos.
- O roster cria interação com as keywords das cartas.
- `dotnet build` limpo (e `npx ng build` se algo no front mudar).

**Verificação:** builds + run encontrando invocador/swarm/escudeiro etc.; confirmar que disparam sem
exceção e respeitam o `Rng` (determinismo).

---

# G-09 — Baú = Altar de Eco / Loja da Run

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma · **Depende de:** G-04, G-05, G-07 · **Paraleliza com:** — (Onda 6, solo)

**Objetivo:** hoje o baú é loot solto sem propósito. **Baú = altar de Eco / loja da run.** Abrir um
baú = oferece uma carta, ou reroll, ou gastar ouro numa carta. Isso dobra os baús no loop de build e
ajuda a resolver a cadência (baús viram beats de carta).

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (`TryInteract()`
~`:2686`), `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (tipos de baú),
`backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`, ligação com `Domain/Equipment.cs` /
`pages/kaelis/kaelis.ts` (material de gear).

**Tarefas:**
- **Baú-altar:** abrir oferece **carta**, ou **reroll** (reusa G-05), ou **comprar carta com ouro**.
- **Baús amaldiçoados:** Eco forte + maldição (spawna mob duro / debuff na sala) — ganância vs
  segurança, a tensão que o auto sozinho não dá.
- **Mimics** reskinados como "baú-Eco corrompido" (callback Tibia; surpresa gostosa de assistir) —
  reusar arquétipo de mob de G-08.
- **Material de gear por tier:** alguns baús dropam material conectando o loot da run àquela tela de
  equipamento da Kaeli (crescimento de conta). Introduzir o conceito de material e plugar na tela de
  gear.
- Determinístico; constantes em `GameConfig.cs`.

**Aceite:**
- Baú integra cartas/loja (oferta / reroll / compra).
- Amaldiçoado e mimic funcionam e criam tensão risco/recompensa.
- Material de gear dropa e alimenta a tela de equipamento.
- `dotnet build` + `npx ng build` limpos.

**Verificação:** builds + preview dos três tipos de baú (altar, amaldiçoado, mimic) + confirmar
material chegando na Mochila/tela de gear.

---

# G-10 — Painel HELPER → Editor de Táticas Gambit (FF12)

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ APIs) · **Depende de:** — · **Paraleliza com:** G-06 (Onda 4) — ⚠ atenção a merge em `game.ts`

**Objetivo:** o pulo do gato pro perfil dev. Transformar o painel HELPER (Alvo/Skills/Ult/Pref/Stand/
Follow/Avoid) num **editor de táticas estilo gambit do FF12** — "usa Ult quando boss em break", "cura
abaixo de 40%", "prioriza suporte". Configurar a própria IA é expressão de build.

**Arquivos prováveis:** `frontend/src/app/core/types.ts` (`AutoHelperSettingsDto` → lista de regras
condição→ação), `frontend/src/app/pages/game/game.ts` (UI builder), `core/game-client.service.ts`,
`backend/src/KaezanArenaFable.Api/Hubs/GameHub.cs` (comando novo), `Engine/GameWorld.cs`
(`TickAutoHelper()` ~`:749` avalia as regras), `Meta/AccountState.cs` (presets por Kaeli),
`Domain/GameConfig.cs`.

**Seam existente (REUTILIZE):** `AutoHelperSettingsDto` + `TickAutoHelper` + comando `SetAutoHelper`
já existem e são determinísticos. Generalizar para uma **lista ordenada de regras** (condição →
ação), preservando o default full-auto atual.

**Tarefas:**
- Modelo de **regra:** condição (HP%, alvo em break, distância, cooldown pronto, nº de inimigos,
  tag no alvo…) → ação (cast skill X, usar ult, mover toward/away, focar tipo de alvo).
- Avaliação **em ordem**, **determinística**, no `TickAutoHelper`.
- **Presets por Kaeli** persistidos em `AccountState`.
- UI builder no painel HELPER; manter os toggles atuais como preset default (full-auto).

**Aceite:**
- Regras avaliadas em ordem; determinístico (só `Rng` da run).
- Presets persistem por Kaeli; default full-auto preservado.
- IDs de tática (`tactic:*`) estáveis se persistidos.
- `dotnet build` + `npx ng build` limpos.

**Verificação:** builds + run com uma regra custom (ex. "ult quando boss em break") disparando no
momento certo; recarregar e confirmar preset persistido.

---

# G-11 — Farm/Auto-Repeat + Progressão Offline

Resumo: _(preencher ao concluir)_

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** G-07 (Onda 5)

**Objetivo:** o "evoluir conta" literal. Auto-repeat de tier (re-inicia a run ao terminar) +
progressão offline (recompensa por tempo). Fecha o loop de conta entre runs.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Engine/RunManager.cs`,
`backend/src/KaezanArenaFable.Api/Meta/AccountState.cs` (+ serviços de Meta),
`frontend/src/app/pages/mode/mode.ts` + `prerun.ts`,
`backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`.

**Tarefas:**
- **Auto-repeat de tier:** ao terminar uma run, reiniciar automaticamente o mesmo tier (toggle no
  front), sem intervenção.
- **Progressão offline:** creditar recompensa coerente baseada em tempo desde a última sessão
  (cap/taxa em `GameConfig.cs`).
- Não quebrar saves existentes (migração no `AccountSanitizer` se mudar o schema).

**Aceite:**
- Auto-repeat reinicia a run sem intervenção.
- Offline credita recompensa coerente e limitada.
- Contas antigas não quebram ao carregar.
- `dotnet build` + `npx ng build` limpos.

**Verificação:** builds + preview do farm (run reiniciando sozinha) + simular tempo offline e
conferir o crédito.

---

# G-12 — Balance e Verificação Ponta-a-Ponta

Resumo: _(preencher ao concluir)_

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** G-02–G-11 · **Paraleliza com:** — (solo, Onda 7)

**Objetivo:** garantir que a gameplay base nova passa por runs reais e que nenhuma carta/Eco/mob
domina por erro óbvio de número. Exige julgamento de balance, por isso fica no modelo premium.

**Verificação mínima:**
- `dotnet build` + `npx ng build` limpos.
- Run melee (ex. Seren) e run ranged (ex. Velvet/Eloa) até o boss.
- Conferir: juice e break aparecem (G-02/G-03); cartas têm 3 tiers e sinergia de tag (G-04); reroll/
  banir funcionam (G-05); ~6-9 escolhas em beats (G-06); mapas geram com tipos/bifurcação e biomas
  distintos (G-07); mobs novos disparam (G-08); baú-altar oferece carta + material (G-09); gambit
  dispara uma regra custom (G-10); farm/offline credita (G-11).

**Aceite:**
- Builds verdes.
- Run inicia e completa com pelo menos uma melee e uma ranged.
- Sem dominador óbvio (nenhuma carta/Eco/mob quebra o balance).
- Nenhum asset cai no placeholder; console limpo.

---

## Depois

- **Arte autoral final** das criaturas Kaezan (skins sobre os arquétipos de G-08).
- **Curadoria do pool de material de gear** / crafting real.
- **Eventos de sala** (não-combate) e salas-armadilha além da bifurcação.
- **Sinergias cross-Kaeli** e segundo Eco por elemento.
- **Telegraphs de mob** (AoE avisada) como contraparte da legibilidade do helper.
