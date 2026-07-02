# Roadmap — Dash por Classe & Sala de Treino

> **Como usar este arquivo.** Cada `DT-NN` abaixo é uma unidade de trabalho **auto-contida**: o
> agente que executa começa "frio", então o prompt já traz o contexto que ele precisa. Você dispara
> com **"implemente o prompt DT-NN do `docs/roadmap/ongoing/roadmap_dash_training.md`"** e o agente
> faz o resto.
>
> Cada prompt declara: **Modelo · Effort · Skill · Depende de · Aceite · Verificação.**
>
> **Estado.** Trilha **fechada**. `DT-01..DT-05` (fundação) e `DT-10` (tuning final) estão
> **implementados** (`[x]`). Os refinos intermediários `DT-06..DT-09` foram **descartados** por
> decisão de design (a assinatura por papel entregue em DT-01..05 já leu distinta e justa o
> bastante — não valeram a complexidade extra de marca-do-archer / spread-condicional /
> cooldown-por-papel / slide-do-knight). Ficam aqui só como registro da discussão.
>
> **Não confundir com:** `docs/roadmap/ongoing/roadmap_dungeons.md` (geração/conteúdo de dungeon),
> `docs/roadmap/done/roadmap_refactor_kaelis.md` (roster/kits/gacha) e `docs/roadmap/not started/
> roadmap_hunts.md` (modos de caçada). Este arquivo é **só** a habilidade Dash (esquiva por papel) e o
> modo **Training Room**, e toca **principalmente o backend** (`Domain/GameConfig.cs`, `Engine/`),
> mais o plumbing de modo no frontend.

---

## Modelos & quando usar

| Modelo | Papel | Effort típico | Por quê |
|---|---|---|---|
| **Claude Code Opus 4.8** | Feeling do dash, identidade por papel, comportamento de campo (spread), tuning de cooldown/i-frames, balance | `high` / `medium` | É decisão de "gosto" de game design + invariantes de engine (determinismo, campos limitados). Errar cascateia em todas as Kaelis daquele papel. |
| **GPT-5.5 (Codex)** | Mudança bounded com regra já fechada: ajuste de constante, texto de README, plumbing de UI copiando padrão existente | `low` / `medium` | Tarefa com regra explícita e padrão a seguir. Barato e rápido. |

- Use **`use context7`** ao consultar API de biblioteca (SignalR, Angular) nos prompts que tocarem o canal de jogo / componentes.
- Nenhum prompt desta trilha depende de skill instalada — todo o contexto vive no próprio prompt.

---

## Invariantes inegociáveis (todo prompt respeita)

- **Backend autoritativo.** Frontend nunca simula combate/movimento/dash — só interpola e renderiza o snapshot.
- **Determinismo do engine.** `GameWorld` usa só o `Rng` da run. Nunca `Random`, `DateTime.Now`, ou
  iteração de coleção instável dentro do tick. O dash e o dummy de treino seguem essa regra.
- **Todas as constantes de simulação em `Domain/GameConfig.cs`.** Nada de número mágico de dash/treino no tick.
- **Campos limitados.** Terreno que se alastra respeita `GameConfig.FieldMaxTilesPerFloor` (a trilha do
  mago não pode explodir a simulação).
- **IDs estáveis** (`waifu:*`, etc.) não renomeiam. O numeral de `GameMode` faz parte do contrato
  cliente↔hub (`Dungeon=0`, `Arena=1`, `Training=2`) — **não reordenar**.
- `dotnet build` (backend) e `npx ng build` (frontend) passam sem erro ao fim de cada prompt que tocar o respectivo lado.

---

## Tese

O dash deixou de ser um botão genérico: a **assinatura muda por papel** (movimento + payoff) e cada
um precisa **ler diferente** — pelo movimento e pelo FX. Um mago de morte que esquiva deixando fogo
está errado; três papéis que esquivam com o mesmo poof azul também. Além disso, refinar mecânica de
combate sem um lugar pra testar é caro: a **Training Room** dá um sandbox (e palco de debug) onde se
bate num boneco passivo de muita vida/regen pra sentir kit, dash e reações sem pressão de run.

## Decisões Fechadas

- Há **três assinaturas de dash**, mapeadas por papel em `GameWorld` (`DashSignature`): **Knight =
  Cleave** (blink curto + nova de impacto no pouso), **Archer = Sprint** (atravessa mobs, ganha haste,
  sem dano), **Mage = Trail** (desliza e semeia trilha fraca que se alastra — Contágio).
- **Cardinal-only.** Não existe dash diagonal.
- O **mesmo** botão/cooldown é usado pelo input e pelo auto-helper (sair da quina ao kitar).
- A trilha do mago usa o **elemento da própria Kaeli** (`GameConfig.ElementFieldFx`), não fogo fixo.
- Cada papel tem **FX próprio** de animação (não o mesmo poof): Knight poff de fumaça → burst de
  impacto; Archer rastro ciano de haste; Mage rastro do elemento.
- **Training Room** é um **modo** (`GameMode.Training`), não um tier — encaixa no seam de modos
  (`GameModeStrategy`) sem tocar o pipeline de combate. Mapa pequeno **fixo** (não procedural).
- O boneco de treino é **passivo** (não ataca/persegue), tem **muita vida + regen**, **respawna** se
  morrer, **não dropa** nada e a run de treino **não dá recompensa** (anti-AFK-farm).

## Referência técnica (estado final, pós DT-10)

| Constante (`GameConfig`) | Valor | Papel |
|---|---|---|
| `DashTiles` | 3 | distância base (Mage/Archer) |
| `DashKnightBlinkTiles` | 2 | blink do Knight |
| `DashCooldownMs` / `DashIFramesMs` | 2500 / 300 | **compartilhados** p/ os 3 (DT-08 descartado: diferença vem do movimento+payoff) |
| `DashKnightVanishFx` / `DashCleaveFx` | 3 / 35 | poff de fumaça + burst |
| `DashArcherTrailFx` | 13 | rastro ciano de haste |
| `DashArcherHasteMs` / `DashArcherHasteFactor` | 1800 / 1.5 | haste pós-sprint (DT-10: 1500 → 1800) |
| `DashTrailFieldAtkScale` | 0.18 | dano/tick fraco da trilha do mago |
| `DashTrailFieldSpreadChance` / `…Generations` | 20 / 1 | spread da trilha (DT-07 descartado: já mal rasteja em qualquer elemento) |
| `ElementFieldFx(element)` | 7/44/41/46/40/18/10 | FX de campo por elemento |
| `TrainingRoomSize` | 18 | lado da caixa fixa |
| `TrainingDummyHp` / `…RegenPctPerSec` | 200000 / 0.04 | vida/regen do boneco |

Métodos-âncora em `Engine/GameWorld.cs`: `PerformMageDash`, `PerformArcherSprint`,
`PerformKnightBlink`, `SlideDash(…, trailFx)`, `DashScorchTrail`, `DashCleave`, `SpendDash`,
`SpawnTrainingDummy`, `TickTrainingDummy`, `KillMonster` (branch do dummy), `EndRun` (guard de modo).
Seam de modo em `Engine/GameMode.cs` (`TrainingModeStrategy`) e `Engine/DungeonGenerator.cs`
(`GenerateTrainingRoom`).

---

## Mapa de prompts (escopo)

| Prompt | Tema | Modelo | Effort | Depende de | Onda |
|---|---|---|---|---|---|
| DT-01 | [x] Trilha do mago por elemento (`ElementFieldFx`) | Opus 4.8 | medium | — | 0 (feito) |
| DT-02 | [x] FX de animação distinto por papel | Opus 4.8 | medium | DT-01 | 0 (feito) |
| DT-03 | [x] Training Room — backend (modo + gerador + dummy) | Opus 4.8 | high | — | 0 (feito) |
| DT-04 | [x] Training Room — frontend (card + prerun + plumbing de modo) | GPT-5.5 (Codex) | medium | DT-03 | 0 (feito) |
| DT-05 | [x] README/docs do dash + treino | GPT-5.5 (Codex) | low | DT-01..04 | 0 (feito) |
| ~~DT-06~~ | ~~Identidade de dano/utilidade do Archer sprint~~ | — | — | — | descartado |
| ~~DT-07~~ | ~~Spread da trilha do mago por elemento (só fogo alastra)~~ | — | — | — | descartado |
| ~~DT-08~~ | ~~Cooldown / i-frames por papel~~ | — | — | — | descartado |
| ~~DT-09~~ | ~~Feeling do Knight blink (instantâneo vs slide curtíssimo)~~ | — | — | — | descartado |
| DT-10 | [x] Tuning final dos 3 dashes na Training Room | Opus 4.8 | medium | DT-01..05 | 1 (feito) |

---

## Execução (histórico)

Trilha fechada e sequencial. A fundação `DT-01..DT-05` rodou primeiro; os refinos intermediários
`DT-06..DT-09` foram **descartados** (ver *Estado*) e o tuning final `DT-10` rodou direto sobre a
fundação. Não houve paralelismo real — quase tudo tocava `Engine/GameWorld.cs` **e**
`Domain/GameConfig.cs`.

```
Onda 0   DT-01 → DT-02 → DT-03 → DT-04 → DT-05   (FEITO — fundação)
              │
              │   DT-06..DT-09  (DESCARTADOS)
              ▼
Onda 1   DT-10 (tuning final, solo, sobre a fundação; A/B na Training Room)   (FEITO)
```

---

# [x] DT-01 — Trilha do Mago por Elemento

Resumo: a trilha do dash do mago agora usa o elemento da própria Kaeli (`GameConfig.ElementFieldFx`),
não fogo fixo. Velvet trilha morte (FX 18), Eloa sagrado (40), Rin fogo (7). O dano do campo já era
`Waifu.Element`; só o FX estava hardcoded.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** — (Onda 0)

**Objetivo:** corrigir que todo mago deixava rastro de fogo (a primeira implementada foi a Rin). O
rastro deve ler como o elemento da Kaeli.

**Arquivos:** `Domain/GameConfig.cs` (`ElementFieldFx`), `Engine/GameWorld.cs` (`DashScorchTrail`).

**Tarefas (feitas):**
- Adicionar `GameConfig.ElementFieldFx(element)` espelhando o FX que cada skill de campo já usa
  (fire 7 · ice 44 · energy 41 · earth 46 · holy 40 · death 18 · physical 10).
- `DashScorchTrail` resolve o FX por `Waifu.Element`.

**Aceite:** dash da Velvet deixa rastro de morte; Rin segue fogo; nenhum elemento cai em fogo por engano.

**Verificação:** `dotnet build` limpo; na Training Room, dashar com Velvet e Rin e comparar o FX.

---

# [x] DT-02 — FX de Animação Distinto por Papel

Resumo: cada assinatura de dash tem FX próprio. Knight: poff de fumaça (`DashKnightVanishFx=3`) no
ponto de saída + burst de impacto (`DashCleaveFx=35`) no pouso. Archer: rastro ciano de haste
(`DashArcherTrailFx=13`). Mage: rastro do elemento (DT-01). `SlideDash` virou parametrizável por `trailFx`.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** DT-01 · **Paraleliza com:** — (Onda 0)

**Objetivo:** acabar com "os três dashes usam o mesmo efeito azul". O papel tem que ser legível pelo FX.

**Arquivos:** `Domain/GameConfig.cs` (FX por papel), `Engine/GameWorld.cs` (`SlideDash`,
`PerformArcherSprint`, `PerformMageDash`, `PerformKnightBlink`).

**Tarefas (feitas):**
- `SlideDash(ox,oy,dx,dy,len,trailFx)` — trail FX injetado por chamador.
- Archer passa `DashArcherTrailFx`; Mage passa `ElementFieldFx(Waifu.Element)`; Knight emite
  `DashKnightVanishFx` na saída e mantém o cleave em `DashCleaveFx`.

**Aceite:** os três dashes são visualmente distintos (cinza/impacto · ciano · elemento).

**Verificação:** `dotnet build` limpo; A/B dos 3 papéis na Training Room.

---

# [x] DT-03 — Training Room (backend)  ⭐ fundação

Resumo: novo `GameMode.Training` (=2) + `TrainingModeStrategy` no seam de modos.
`DungeonGenerator.GenerateTrainingRoom` cria uma caixa fixa 18×18 (sem procedural/POIs).
`GameWorld.SpawnTrainingDummy` coloca um boneco passivo (sprite do boss do tier) com
`TrainingDummyHp`+regen; `TickTrainingDummy` regenera e encara o jogador, sem mover/atacar.
`KillMonster` tem branch de dummy (sem xp/loot/gauge/chest/portal, respawn via strategy). `EndRun`
não dá recompensa em modo Training.

- **Modelo:** Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ SignalR) · **Depende de:** — · **Paraleliza com:** — (Onda 0)

**Objetivo:** sandbox/palco de debug pra testar features novas sem run procedural.

**Arquivos:** `Engine/GameMode.cs`, `Engine/DungeonGenerator.cs`, `Engine/GameWorld.cs`
(`Actor.IsTrainingDummy`, spawn/tick/kill/endrun), `Domain/GameConfig.cs` (constantes de treino).

**Tarefas (feitas):** ver Resumo. Gates: AI curto-circuita o dummy (DoTs ainda aplicam);
floor único role "mob" (não dispara chest/portal/boss).

**Aceite:** modo cria 1 andar com 1 boneco passivo; ele não ataca, regenera, respawna se morrer; a
run só termina no ESC; sem recompensa.

**Verificação:** `dotnet build` limpo; subir a run em modo Training e bater no boneco (HP cai e volta;
não persegue; ESC encerra sem ganhar ouro/xp).

---

# [x] DT-04 — Training Room (frontend)

Resumo: card "Training Room" (🎯, `live`) na aba Hunt; clicar pula o seletor de tier e vai direto ao
seletor de Kaeli (tier 1, `?mode=training`), com copy de sandbox e "Back to Hunt". `GameMode.Training`
adicionado ao enum do client; `mode` threadado em `prerun` → `game` → `joinRun` (incl. resume e auto-repeat).

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** DT-03 · **Paraleliza com:** — (Onda 0)

**Arquivos:** `core/game-modes.ts`, `core/game-client.service.ts` (enum), `pages/hunt/hunt.ts`,
`pages/prerun/prerun.ts`, `pages/game/game.ts`.

**Aceite:** Hunt mostra o card; o fluxo chega no jogo com `mode=training`; modo dungeon intocado.

**Verificação:** `npx ng build` limpo; clicar Training Room → escolher Kaeli → entrar.

---

# [x] DT-05 — README/Docs

Resumo: linha de controle do dash no README atualizada (FX por papel + trilha por elemento) e seção
de Training Room no loop de jogo.

- **Modelo:** GPT-5.5 (Codex) · **Effort:** low · **Skill:** nenhuma · **Depende de:** DT-01..04 · **Paraleliza com:** — (Onda 0)

**Arquivos:** `README.md` (este roadmap registra o resto).

**Aceite:** README descreve os 3 dashes distintos e a Training Room sem mencionar comportamento antigo.

**Verificação:** revisão de texto; nenhum build.

---

# ~~DT-06..DT-09~~ — Refinos descartados

**Descartados por decisão de design** (ver *Estado* no topo). A assinatura por papel entregue em
DT-01..05 — Knight blink+cleave, Archer sprint pass-through+haste, Mage slide+scorch trail por
elemento — já leu **distinta e justa** o bastante na Training Room. Os quatro refinos abaixo
adicionavam complexidade que não se pagou; ficam só como registro da discussão:

- **DT-06** — marca/tiro do Archer ao atravessar. *Veredito:* manter **kite puro** (sem dano); a
  pura mobilidade já é a identidade. Sem mudança.
- **DT-07** — spread da trilha do mago condicional ao elemento (só fogo alastra). *Veredito:* o
  spread fraco atual (`DashTrailFieldSpreadChance=20`, `Generations=1`) já mal rasteja em qualquer
  elemento; diferenciar por elemento não valeu o seletor extra.
- **DT-08** — cooldown/i-frames por papel. *Veredito:* cooldown/i-frames **compartilhados** mantêm
  o HUD simples e o helper previsível; a diferença por papel já vem do movimento+payoff, não do timing.
- **DT-09** — slide curtíssimo no Knight blink. *Veredito:* manter o **blink instantâneo** (pop); o
  poff de fumaça na saída + burst no pouso já comunicam o teleporte.

---

# [x] DT-10 — Tuning Final dos 3 Dashes na Training Room

Resumo: trilha fechada sobre a fundação DT-01..05 (DT-06..09 descartados). Revisados os 3 dashes
lado a lado na Training Room — as assinaturas já leem distintas em todos os eixos (distância 3/3/2,
payoff cleave/haste/trilha, FX poff+burst / ciano / elemento) e o dano (cleave 0.70, trilha 0.18)
está balanceado, então foi **travado**. Único ajuste de feeling: `DashArcherHasteMs` 1500 → 1800 pra
o kite do Archer continuar à frente por ~72% do cooldown (antes caía aos 60%). README confirma a
identidade final de cada papel.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** DT-01..05 · **Paraleliza com:** — (Onda 1)

**Objetivo:** com os refinos fechados, sentar na Training Room e calibrar **por feeling** os três
dashes lado a lado (distância, cooldown, dano da trilha/cleave, leitura de FX). Saída: valores finais
em `GameConfig` + 1 parágrafo no README confirmando a identidade de cada papel.

**Contexto técnico / Arquivos prováveis:** `Domain/GameConfig.cs` (ajuste fino das constantes
`Dash*`), `README.md` (linha de controle do dash). Usar a Training Room (DT-03/04) como bancada: um
boneco passivo de muita vida deixa medir dano por tick/cleave e observar FX sem ruído de run.

**Tarefas:**
- Rodar os 3 papéis no boneco; anotar o que está "longo/curto/fraco/ilegível".
- Ajustar constantes (sem hardcode fora de `GameConfig`); manter determinismo.
- Atualizar README com a identidade final de cada dash.

**Aceite:** os três dashes sentem distintos e justos; valores finais documentados; builds verdes.

**Verificação:** `dotnet build` + `npx ng build` limpos; sessão de A/B na Training Room com cada papel.

---

## Depois (fora de escopo deste roadmap)

- **Evolução de dash por card/maestria** — o `_dashSignature` é resolvido do papel na construção do
  `GameWorld`, com hook explícito pra um card/nó de maestria reatribuir ("dash evolution"). Virar
  trilha própria quando houver design de cards de mobilidade.
- **Dummy de treino configurável** — toggles de debug na Training Room (elemento do boneco,
  liga/desliga regen, spawnar N bonecos, simular boss com postura/Echo Break pra testar a janela de
  break fora de run).
- **Métricas de dash no simulador** — expor uso/efeito do dash no BalanceSim pra balance data-driven
  em vez de só feeling.
