# Map Layout — Natural Arena Shape (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Ao terminar cada task, marque as checkboxes `[x]` dos steps e da task neste arquivo.**
> Nunca conclua sem atualizar os checkboxes.

**Spec:** `docs/superpowers/specs/2026-07-07-map-layout-natural-shape-design.md`

**Goal:** Arenas com tamanho consistente e caminhável (~70% do interior) e forma orgânica arredondada
(referência `audit-shots/ref-troll-cave.png`), sem lascas de parede de 1 célula nem beiras de maciço em
escada. A composição (já correta) passa a renderizar formas limpas.

**Architecture:** recalibrar o carver de arena (`SeedArenaRock`: lobos maiores/menos, menos ruído de
borda) + piso garantido de área via dilatação (T1); um passe morfológico determinístico
`SmoothArenaShape` (open-close + de-stair + re-flood) rodando após `ErodeArena`/`CarveAmphitheater` e
antes de pockets/pilares (T2); varredura de aceite + **um rebaseline** que superpõe o pendente da
composição (T3).

**Tech Stack:** C# .NET 8 (xunit), BalanceSim (golden/replay), Map Lab (aba admin).

## Global Constraints

- **Determinismo:** lobos/piso usam o `Rng` da run em ordem de varredura fixa; CA, dilatação, open-close,
  de-stair e re-flood são **rng-free e double-buffered**. Proibido `Random`, `DateTime.Now`,
  `Guid.NewGuid()`, iteração sem ordem estável.
- **Constantes de simulação** só em `Domain/GameConfig.cs`.
- **C# novo/modificado sem `var`** — tipos explícitos sempre.
- **Código e comentários em inglês**; docs em `docs/**` podem ser PT.
- **Backend autoritativo:** só o layout do `Blocked` muda; nada migra pro cliente.
- **Rebaseline total deliberado** (T3): supera o rebaseline pendente da fatia de composição (bateria de
  replays já está `git`-deletada no working tree — regravar do zero).
- **Ao concluir cada task:** `dotnet build backend/src/KaezanArenaFable.Api` limpo; `dotnet test` verde;
  commits pequenos direto na `main`, stage seletivo; checkboxes marcadas.
- Verificação visual: `tools/run-backend.ps1` (Release, 5210) + `npx ng serve`; Map Lab (sem combate).

## Fatos já verificados (2026-07-07, não re-derivar)

- **Floors são arena única** (`RoomsFloor1/2 = 1`); `ErodeRoom` (multi-sala) está dormante — fora de
  escopo. `ErodeArena`/`CarveAmphitheater` compartilham `ApplyRockToFloor`.
- **Carver:** `SeedArenaRock` (`DungeonGenerator.cs:547–581`) = 2–4 lobos elípticos (banda 0.30–0.70,
  `ArenaLobesMin/Max`, `ArenaLobeRadiusMinFrac/MaxFrac`, `ArenaLobeCore`) + ruído de borda
  (`ArenaEdgeNoiseProb`); `ApplyRockToFloor` (`:589+`) = CA 4-5 (`OrganicCaIterations`,
  `OrganicWallThreshold/FloorThreshold`) + core central forçado + flood-fill do centro.
- **Sequência atual em `Generate`** (arena única não-boss): `ErodeArena` → `CarveSidePockets` →
  `PlacePillars` (`DungeonGenerator.cs:166–171`). `PlacePillars` marca `Blocked` — a limpeza morfológica
  precisa vir ANTES dele.
- **Medida do problema:** preview T1 seed 101 = 200/400 abertas (50%); alvo é ~70% consistente.
- **Composição já lê `Blocked`** para o brush de montanha (T3 da fatia anterior) — melhorar a forma do
  `Blocked` melhora o maciço de graça, sem tocar composição.

---

### [ ] Task 1: Carver — área consistente + forma orgânica arredondada

**Model · Effort:** Fable 5 · high

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (SeedArenaRock, ApplyRockToFloor —
  piso de dilatação), `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (constantes do carver + piso)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`

**Interfaces:**
- Produces: fração aberta do interior em [0.65, 0.78] para todo seed/tier; forma de lobos-união
  arredondada. T2 consome o `Blocked` resultante.

- [ ] **Step 1: Teste que falha (TDD).** `ArenaOpenFractionIsConsistent`: para seeds {101,202,303,404,505}
  × tiers 1–5, gerar o floor normal e medir `openInterior = (# !Blocked dentro da margem) / (interior
  total)`. Assertar `0.65 <= openInterior <= 0.78` para todos. Rodar → FAIL (variância atual).
- [ ] **Step 2: Recalibrar lobos.** Em `GameConfig.cs`, ajustar `ArenaLobesMin/Max` (menos lobos, ex.
  2–3), `ArenaLobeRadiusMinFrac/MaxFrac` (maiores e faixa mais estreita), e **baixar** `ArenaEdgeNoiseProb`
  (costa mais lisa). Iterar no Map Lab (Preview draft) até a forma ler arredondada com baías (ref
  `audit-shots/ref-troll-cave.png`). Documentar os valores escolhidos com comentário do porquê.
- [ ] **Step 3: Piso de área (dilatação).** Nova constante `ArenaMinOpenFraction` (ex. 0.68). Em
  `ApplyRockToFloor`, após o flood-fill, se `openInterior < ArenaMinOpenFraction`, aplicar dilatação
  morfológica determinística (rng-free, double-buffered): repetir "toda parede adjacente-4 a uma célula
  aberta vira aberta, respeitando a margem do floor" até bater o piso ou atingir um teto de iterações
  (`ArenaDilateMaxIters`). Re-flood não é necessário aqui (dilatação preserva conectividade).
- [ ] **Step 4:** `dotnet test --filter ArenaOpenFraction` PASS; suíte verde; `dotnet build` limpo.
- [ ] **Step 5: Verificação visual:** Map Lab 5 tiers seeds {101,202,303}: tamanhos parecidos,
  caminháveis, forma arredondada. Screenshots "depois" no doc de aceite.
- [ ] **Step 6:** Commit (`feat(engine): consistent walkable arena size + rounder lobe shape`).

---

### [ ] Task 2: Limpeza morfológica — mata lasca de 1 célula e beira em escada

**Model · Effort:** Fable 5 · high

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (novo `SmoothArenaShape`; chamá-lo
  em `Generate` após `ErodeArena` e no caminho do boss após `CarveAmphitheater`, antes de
  `CarveSidePockets`/`PlacePillars`), `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (constantes)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`

**Interfaces:**
- Consumes: `DungeonFloor.Blocked` pós-carve (T1). Produces: borda de maciço limpa (sem protrusão/enseada
  de 1 célula, sem degrau diagonal), conectividade preservada.

- [ ] **Step 1: Testes que falham (TDD).**
  - `NoSingleCellWallProtrusions`: nenhuma célula `Blocked` com ≥3 dos 4 vizinhos abertos.
  - `NoSingleCellOpenInlets`: nenhuma célula aberta com ≥3 dos 4 vizinhos `Blocked` (dentro da arena).
  - `ArenaIsFullyConnected`: todas as abertas alcançáveis do centro (BFS 4-vias).
  - `PillarsAndPocketsSurvive`: contagem de pilares/pockets > 0 quando esperado (a limpeza roda antes
    deles — guarda de sequência).
  Rodar → FAIL nos três primeiros.
- [ ] **Step 2: Implementar `SmoothArenaShape(DungeonFloor floor, Room room)`** (rng-free):
  - **Open-close** double-buffered, `GameConfig.ShapeSmoothPasses` (ex. 1–2): parede com ≥3 vizinhos-4
    abertos → aberta; aberta com ≥3 vizinhos-4 bloqueados → parede. Respeitar a margem do floor (não
    abrir na borda).
  - **De-stair diagonal:** para cada célula aberta cujo padrão de vizinhança seja um degrau (aberto só
    na diagonal com as duas edges adjacentes bloqueadas, formando escada), arredondar preenchendo/cortando
    o canto de forma consistente (definir a regra exata no código com comentário; determinística).
  - **Re-flood:** flood-fill 4-vias do centro forçado-aberto; bloquear o desconexo.
- [ ] **Step 3: Ligar em `Generate`.** Chamar `SmoothArenaShape` logo após `ErodeArena(...)` (arena
  única não-boss) e após `CarveAmphitheater(...)` (boss), **antes** de `CarveSidePockets`/`PlacePillars`.
- [ ] **Step 4:** `dotnet test` (novos + suíte) PASS; `dotnet build` limpo.
- [ ] **Step 5: Verificação visual:** Map Lab 5 tiers seeds {101,202,303} + 1 boss/tier: zero lasca de
  parede, beira de maciço arredondada (sem escada). Comparar com as screenshots do problema (imagens do
  usuário 2026-07-07). Screenshots "depois" no doc de aceite.
- [ ] **Step 6:** Commit (`feat(engine): morphological arena cleanup — kill 1-cell nubs + diagonal stairs`).

---

### [ ] Task 3: Aceite visual + rebaseline (superpõe o da composição) + push

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Create/Modify: doc de aceite (`docs/superpowers/specs/2026-07-07-map-layout-audit.md` OU seção no
  audit v2 com screenshots "depois")
- Modify: `docs/balance/golden_dungeon.txt` (rebaseline), `backend/src/KaezanArenaFable.Api/.data/replays/`
  (bateria regravada), `README.md` (se comportamento visível mudou), os dois planos (checkboxes:
  este + marcar T6/T7 da composição como superados por esta fatia)

- [ ] **Step 1:** Bateria: `dotnet test backend/tests/KaezanArenaFable.Api.Tests` PASS; `dotnet build`
  e `npx ng build` (em `frontend/`) limpos.
- [ ] **Step 2: Aceite visual** (checklist do spec, screenshot de cada no doc): tamanho consistente e
  caminhável · forma arredondada orgânica · zero lasca de 1 célula · beira de maciço sem escada · uma run
  real T2 e T4 (backend Release + frontend) confirmando que o jogo reflete o Map Lab · comparar com
  `audit-shots/ref-troll-cave.png`.
- [ ] **Step 3: REBASELINE deliberado (único; superpõe o pendente da composição):**

```powershell
dotnet run --project tools/BalanceSim -- --golden
dotnet run --project tools/BalanceSim -- --golden-check
```

Regravar a bateria de replays do zero (a antiga está `git`-deletada), depois:

```powershell
dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays
```

Esperado: 0 divergências.

- [ ] **Step 4: Docs.** Doc de aceite preenchido (antes/depois); `README.md` se mudou comportamento
  visível; TODAS as checkboxes deste plano marcadas; no plano da composição, anotar que T6/T7 foram
  absorvidos por esta fatia.
- [ ] **Step 5: Commit final + push**

```powershell
git add -A docs docs/balance backend/src/KaezanArenaFable.Api/.data/replays README.md
git commit -m "docs: natural arena shape delivered (deliberate golden rebaseline + replay battery)"
git push origin main
```

(Stage seletivo — conferir `git status` antes; o working tree tem replays deletados de propósito.)

---

## Ordem e dependências

```
T1 (carver: tamanho + forma) ─→ T2 (limpeza morfológica) ─→ T3 (aceite + rebaseline + push)
```

Estritamente sequencial: T2 opera sobre o `Blocked` que T1 produz; T3 fecha com o único rebaseline.

## Riscos conhecidos

- **Piso vs forma:** se 70% apagar as baías, baixar `ArenaMinOpenFraction` antes de subir a dilatação —
  a forma tem prioridade sobre encher.
- **Dilatação na borda:** limitar ao interior (respeitar margem) pra não colar na parede do floor.
- **Ordem com pilares/pockets:** `SmoothArenaShape` DEVE rodar antes de `PlacePillars`/`CarveSidePockets`
  (guardado pelo teste `PillarsAndPocketsSurvive`).
- **De-stair ambíguo:** definir a regra de arredondamento de canto de forma determinística e comentada;
  se ficar agressiva demais (comendo baías), restringir ao padrão de degrau puro.
- **Rebaseline:** único, ao final; superpõe o da composição (T6/T7 daquele plano).
