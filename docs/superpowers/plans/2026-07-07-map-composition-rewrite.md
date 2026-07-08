# Map Composition Rewrite — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Ao terminar cada task, marque as checkboxes `[x]` dos steps e da task neste arquivo.**
> Nunca conclua sem atualizar os checkboxes.

**Spec:** `docs/superpowers/specs/2026-07-07-map-composition-rewrite-design.md`

**Goal:** Mapas procedurais que compõem tile como o Remere's/Tibia — maciço preenchido com beira
auto-bordeada sobre respaldo opaco (zero triângulo preto / pedra cortada / interior cinza) e chão por
região com costura contínua e accents (lava) bordeados — e paleta enxuta por bioma. "Simples porém
bonito", com o alvo `audit-shots/ref-troll-cave.png`.

**Architecture:** troca do modelo de 5 arrays fixos (`Ground/Wall/Decor/BorderA/BorderB`) por **duas
pilhas por célula** (`Flat[]` sob criaturas, `Tall[]` y-sorted), migrado primeiro com conteúdo
equivalente (T1 backend + T2 frontend, visualmente idêntico e testes verdes), e só então a composição
nova: brush de montanha (respaldo opaco + corpo-vs-beira pela máscara blob, T3) e brush de chão
(borda sem cap + accents como famílias, T4). Curadoria de material por bioma (T5), varredura de aceite
visual (T6), **um rebaseline deliberado** de golden + replays no fim (T7).

**Tech Stack:** C# .NET 8 (xunit), Node 20 ESM (`node --test`), Angular 21 standalone + signals,
BalanceSim (golden/replay), Map Lab (aba admin já entregue).

## Global Constraints

- **Determinismo do engine:** dentro da geração só o `Rng` da run em ordem de varredura fixa; os
  passes de composição (montanha, borda, smoothing) são 100% rng-free e double-buffered. Proibido
  `Random`, `DateTime.Now`, `Guid.NewGuid()`, iteração sem ordem estável.
- **Constantes de simulação** só em `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`.
- **C# novo/modificado sem `var`** — tipos explícitos sempre (legado com `var` fica como está).
- **Código e strings de display em inglês** (comentários inclusive); docs em `docs/**` podem ser PT.
- **Backend autoritativo:** o frontend só renderiza o que o backend emite; nenhuma lógica de
  composição migra pro cliente — o renderer só desenha as pilhas na ordem dada.
- **IDs estáveis:** `prefab:*` e espécies do Tibia não mudam; o formato de arquivo do prefab não muda
  nesta fatia (conversão pra pilha é no load).
- **Golden é rebaseline total deliberado** (T7): todo floor muda de formato E de conteúdo; não há
  caminho byte-idêntico a preservar.
- **Ao concluir cada task:** `dotnet build backend/src/KaezanArenaFable.Api` limpo e/ou `npx ng build`
  limpo (em `frontend/`); commits pequenos direto na `main`, stage seletivo; checkboxes marcadas.
- Backend de verificação visual: `tools/run-backend.ps1` (Release, porta 5210); frontend
  `npx ng serve`. Screenshot fora de combate no Map Lab (sem risco de freeze de rAF).

## Fatos já verificados (2026-07-07, não re-derivar)

- **Renderer e sprites são fiéis** (auditoria v2 provou renderizando `otservbr.otbm` real). Toda a
  feiura é de dados do gerador — não mexer no `AssetsService.drawObject`.
- **Mapa é enviado só na troca de floor:** `GameWorld.Tick()` retorna `(SnapshotDto, MapDto?)`; `Map`
  é nullable, construído em `BuildMap()` (`GameWorld.cs:5471`) só quando o floor muda. Pilha jagged =
  custo de wire irrelevante.
- **Render em 2 passadas hoje** (`renderer.ts`): passada 1 (~791–802) desenha `ground→borderA→borderB
  →decor` sob as criaturas; passada 4 (~870+) desenha `wall` y-sorted sobre as criaturas. As pilhas
  `Flat`/`Tall` mapeiam exatamente nessas duas passadas.
- **`TilesetRegistry`:** `TileFamily(Name, Kind, Items[], ZOrder)`, `BorderSet(Edges: edge→id)`,
  `WallTileSet(Tiles: mask→id)`. O WallSet 47-slot **já contém o mask 0** (corpo do maciço), hoje
  ignorado em favor do bedrock `1116`.
- **`WallAutotile.Mask`** já produz a máscara blob-47 canônica por célula bloqueada; **`Canonical`**
  e o fallback 4-peças ficam. O que muda é o consumo (corpo-vs-beira + respaldo).
- **`BorderAutotile.ResolvePieces`** (8-bit, cantos côncavos, edges, diagonais) está correto; o único
  defeito é o **cap de 2 slots** (`BorderA`/`BorderB`) — sai com a pilha.
- **Lava T4/T5 é `Accent` palette na camada `Decor`** (`DungeonGenerator.cs:~1114`), fora das famílias
  — por isso sem borda. Brush `lava` existe no RME (z-order 7700, 6 items, 1 border).
- **Interior do maciço é bedrock `1116`** (`DungeonGenerator.PaintGround` ~1180–1190) — é a causa do
  "maciço cinza". Deve virar o corpo da família (mask 0 do WallSet).
- **`MapDto`** (`Engine/GameDtos.cs:5`): `ushort[] Ground, Wall, Decor, BorderA, BorderB, bool[]
  Blocked` + POIs/rooms/entry. **`frontend/.../types.ts:691`** espelha como `number[]`.

---

### [x] Task 1: Modelo de duas pilhas no backend (migração de formato, conteúdo equivalente)

**Model · Effort:** Sonnet 5 · high

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (DungeonFloor + todos os call
  sites que escrevem Ground/Wall/Decor/BorderA/BorderB), `backend/src/KaezanArenaFable.Api/Engine/GameDtos.cs`
  (MapDto + FromFloor), `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` (BuildMap se acessa os
  arrays), `backend/src/KaezanArenaFable.Api/Engine/DungeonValidator.cs` (se lê os arrays)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`

**Interfaces:**
- Produces: `DungeonFloor` expõe `ushort[][] Flat` e `ushort[][] Tall` (uma pilha por célula, índice
  `y*W+x`); `MapDto` expõe `ushort[][] Flat, ushort[][] Tall` no lugar dos 5 arrays. T2 (frontend) e
  T3/T4 (composição) consomem. **Nesta task o conteúdo é equivalente ao atual** (adapter): `Flat[i]`
  = `[Ground[i]?, BorderA[i]?, BorderB[i]?, Decor[i]?]` (não-zero, nessa ordem); `Tall[i]` =
  `[Wall[i]?]`. Nenhuma mudança visual.

- [x] **Step 1:** Introduzir `Flat`/`Tall` no `DungeonFloor`. Manter os 5 arrays internos por
  enquanto **ou** trocá-los direto — decisão de menor blast radius: manter os 5 como campos de
  trabalho durante a geração e derivar `Flat`/`Tall` num passe final `PackStacks(floor)` (rng-free)
  é o caminho mais seguro para T1 (conteúdo idêntico garantido). Documentar a escolha no commit.
- [x] **Step 2:** `MapDto.FromFloor` emite `Flat`/`Tall` (chamar `PackStacks` ou ler os campos já
  empacotados). Remover `Ground/Wall/Decor/BorderA/BorderB` do DTO.
- [x] **Step 3:** Ajustar `DungeonValidator` e qualquer leitor backend dos 5 arrays (grep
  `\.Ground\[|\.Wall\[|\.Decor\[|\.BorderA\[|\.BorderB\[` em `backend/src`) para ler via `Flat`/`Tall`
  ou via os campos de trabalho, conforme o Step 1.
- [x] **Step 4:** Testes: um teste novo `PackedStacksMatchLegacyLayers` que gera um floor e assevera
  que `Flat[i]`/`Tall[i]` contêm exatamente os ids não-zero das 5 camadas na ordem definida. Os testes
  existentes que liam `.Ground[]`/`.Wall[]` etc. são reescritos para ler as pilhas (equivalência).
- [x] **Step 5:** `dotnet test backend/tests/KaezanArenaFable.Api.Tests` PASS; `dotnet build` limpo.
- [x] **Step 6:** Commit (`refactor(engine): per-cell Flat/Tall tile stacks (equivalent content)`).

---

### [x] Task 2: Frontend consome as pilhas (render idêntico)

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `frontend/src/app/core/types.ts` (MapDto), `frontend/src/app/core/renderer.ts` (passadas 1 e 4),
  `frontend/src/app/pages/admin/map-lab.ts` (se lê os arrays direto), `frontend/src/app/core/tile-shade.ts`
  (só se lê camadas; `Blocked` não muda)

**Interfaces:**
- Consumes: `MapDto.Flat: number[][]`, `MapDto.Tall: number[][]` (T1).

- [x] **Step 1:** `types.ts`: `MapDto` troca `ground/wall/borderA/borderB/decor: number[]` por
  `flat: number[][]; tall: number[][]`.
- [x] **Step 2:** `renderer.ts` passada 1: em vez de desenhar `ground`, `borderA`, `borderB`, `decor`
  em sequência, iterar `map.flat[i]` e `drawObject` cada id na ordem. Passada 4 (y-sorted): iterar
  `map.tall[y*w+x]` e desenhar cada id (hoje só `wall`).
- [x] **Step 3:** Ajustar o Map Lab e o `tile-shade` se lerem as camadas antigas (o shade usa a
  vizinhança de `Blocked`/parede — se lê `wall[]`, trocar por "`tall[]` não-vazio").
- [x] **Step 4:** `npx ng build` limpo. Verificação visual: Map Lab T2 seed 101 **idêntico** ao antes
  do T1/T2 (nenhuma mudança de composição ainda). Screenshot de sanidade.
- [x] **Step 5:** Commit (`refactor(render): draw Flat/Tall tile stacks`).

---

### [x] Task 3: Brush de montanha — respaldo opaco + corpo-vs-beira pela máscara

**Model · Effort:** Fable 5 · high

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (PaintGround / o trecho
  ~1180–1190 que decide borda-vs-bedrock e usa `1116`), `backend/src/KaezanArenaFable.Api/Engine/WallAutotile.cs`
  (helper de corpo-vs-beira se precisar), `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs` (confirmar
  `Bedrock` opaco por bioma)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`

**Interfaces:**
- Consumes: `WallAutotile.Mask(floor,x,y)`, `TilesetRegistry.WallSet(family)` (contém mask 0),
  `biome.Bedrock`. Produces: toda célula bloqueada com `Flat` = `[Bedrock]` (opaco) e `Tall` =
  `[peça do WallSet para o mask]` (mask 0 = corpo; beira = talude). T6/T7 dependem.

- [x] **Step 1: Testes que falham (TDD).**
  - `NoBlockedCellLacksOpaqueBacking`: para todo `i` com `Blocked[i]`, `Flat[i]` não-vazio e o
    primeiro id é opaco (o `Bedrock` do bioma; opacidade conferida pelo conjunto de bedrocks do bioma).
  - `MassifInteriorUsesFamilyBody`: para toda célula bloqueada de mask 0 (cercada), o `Tall[i]` usa o
    slot mask-0 do WallSet da família — **não** `1116`/`WallCorner` genérico (quando a família tem
    WallSet com mask 0).
  Rodar `--filter "NoBlockedCell|MassifInterior"` → FAIL. _(6 casos FAIL confirmados; usam
  `Biomes.Resolve(ForTier(t))` p/ exercitar o WallSet real, ao contrário do helper `Generate` legado.)_
- [x] **Step 2: Implementar.** No passe de parede: para cada célula bloqueada, `mask =
  WallAutotile.Mask(floor,x,y)`; `Flat` recebe `biome.Bedrock`; `Tall` recebe
  `WallAutotile.Resolve(mask, biome)` — e `Resolve`/o call site passa a usar o **corpo** do WallSet
  no mask 0 em vez do bedrock/`1116`. Se a família não tem WallSet ou não tem slot mask-0, `Tall`
  recebe o fallback 4-peças e o corpo cai no `Bedrock` opaco (nunca preto). Remover o uso de `1116`
  como interior de maciço. _(Os dois ramos edge/enclosed foram unificados; passe agora rng-free —
  removido o `rng.Pick(biome.Ground)` da borda. `WallAutotile.Resolve(0)` já retorna o corpo pois os
  3 WallSets têm slot mask-0.)_
- [x] **Step 3:** `dotnet test` (novos PASS + suíte verde); `dotnet build` limpo. _(105/105 PASS;
  `legacy_biome_output_is_byte_identical` rebaselinado deliberadamente — o brush mudou conteúdo E
  sequência de rng do caminho legado, sem caminho byte-idêntico a preservar.)_
- [x] **Step 4: Verificação visual:** Map Lab T2 (Uruk Fort) e T4/T5 (crystal) seeds 101/202/303:
  zero triângulo preto, zero pedra cortada, maciço lê como a família (não cinza). Screenshots "depois"
  no doc de auditoria (classes triângulos / boulders / crystal-wall). _(Verificado via API real
  (`/admin/mapgen/preview`) em 5 tiers × 3 seeds + render no Map Lab T2 s101 e T4 s303; resultado
  documentado na seção "Resultado → T3" do audit doc.)_
- [x] **Step 5:** Commit (`feat(engine): mountain brush — opaque backing + family body/edge by mask`).

---

### [x] Task 4: Brush de chão — borda sem cap + accents (lava) como famílias

**Model · Effort:** Sonnet 5 · high

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Engine/BorderAutotile.cs` (empilhar todas as peças no
  `Flat`, sem cap), `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (PaintTiles /
  PaintClusters → PaintAccentPatches), `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs`
  (`AccentFamily`), `tools/map-importer/tilesets-config.json` + `backend/src/KaezanArenaFable.Api/Content/tilesets.json`
  (família `lava` regenerada), `backend/src/KaezanArenaFable.Api/Content/ContentStore.cs` (reseed)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`, `tools/map-importer/test/tilesets.test.mjs`

**Interfaces:**
- Consumes: `familyOf` (inalterado), `TilesetRegistry.Family("lava")`. Produces: `BiomeDef` ganha
  `string AccentFamily = ""`; `BorderAutotile.Paint` escreve N peças no `Flat` da célula (não 2);
  poças de accent entram no sistema de famílias.

- [x] **Step 1: Família lava no conversor.** Adicionar `"lava"` a `grounds` em `tilesets-config.json`;
  teste em `tilesets.test.mjs` (`lava family + border set emitidos`); `node convert-tilesets.mjs`
  (fechar gap de sprite no `content-config.json` + re-extrair se reportar). Commit parcial dos dados.
  _(Feito: `lava` emitida; gap de 17 sprites fechado via `content-config.json` + AssetExtractor
  `--sprites-only`; `tilesets.json` regenerado com 12 famílias/12 border sets.)_
- [x] **Step 2: Borda sem cap.** Em `BorderAutotile.Paint`, remover o `if (pieces.Count >= 2) break;`
  e o limite de 2 slots — empurrar todas as peças resolvidas no `Flat[i]` (após o ground, antes do
  decor), preservando a ordem determinística (z-order desc, ordinal). Teste
  `EverySeamCellCarriesABorderPiece` (portado do v2, sem cap): célula aberta com vizinho de z maior
  tem ≥1 peça de borda.
  _(Feito via `BorderStack`; `border_stack_keeps_every_resolved_piece_in_draw_order` cobre 3 peças
  resolvidas na mesma célula.)_
- [x] **Step 3: Accents como famílias.** `BiomeDef.AccentFamily`; T4/T5 = `"lava"`; `Biomes.Resolve`
  valida contra `TilesetRegistry.HasFamily`; reseed em `ContentStore` (documentado). `PaintAccentPatches`
  (espelha `PaintClusters` mas escreve `ground` + `familyOf = accentIndex`); o passe de bordas inclui
  a família de accent (`[..familyNames, AccentFamily]`). Teste `AccentPatchesAreBordered` (portado do
  v2): poça de lava com borda em toda a volta, 0 costura nua.
  _(Feito: defaults T4/T5 usam `AccentFamily = "lava"`; biomas antigos sem lava reseedam; admin valida
  família de accent.)_
- [x] **Step 4:** `dotnet test` + `npm test` (map-importer) PASS; `dotnet build` limpo; regenerar/commitar
  `tilesets.json`.
  _(PASS: `dotnet test backend/tests/KaezanArenaFable.Api.Tests`, `npm test` em `tools/map-importer`,
  `dotnet build backend/src/KaezanArenaFable.Api`, `npx ng build`.)_
- [x] **Step 5: Verificação visual:** Map Lab T4 seed 101: lava com borda orgânica; seguir 3 costuras
  longas por mapa (T2/T4) no zoom 2× sem quebra. Screenshots "depois" no doc de auditoria.
  _(Feito: `audit-shots/t4-s101-t4-after.png`; API preview T4 seed 101 confirmou 43 tiles de lava e
  42/42 costuras abertas cardinais com borda `lava->none`.)_
- [x] **Step 6:** Commit (`feat(engine): ground brush — uncapped seam borders + accent families`).

---

### [ ] Task 5: Curadoria de material por bioma (enxugar o ruído)

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs` (defaults de `GroundFamilies`/`WallFamily`/
  `AccentFamily` por tier), possivelmente `tilesets-config.json` + `tilesets.json` (se minerar família/
  corpo novo), `backend/src/KaezanArenaFable.Api/Content/ContentStore.cs` (reseed)

**Interfaces:**
- Consumes: Map Lab (Preview draft) para julgar contraste/leitura; T3/T4 aplicados.

- [ ] **Step 1:** Com T3/T4 no ar, revisar os 5 tiers no Map Lab. Reduzir `GroundFamilies` por bioma
  ao conjunto curado (alvo: 1 primária + 1 secundária de contraste); confirmar 1 `WallFamily` e a
  `AccentFamily` onde faz sentido. Ajustar no editor (Preview draft, sem salvar) até a leitura ficar
  calma e coesa.
- [ ] **Step 2:** Consolidar as escolhas finais nos defaults de `Biomes.cs` com comentário do porquê
  (padrão da fatia anterior); reseed se necessário (mesmo mecanismo do T4). Se alguma família precisar
  de corpo (mask 0) minerado, adicionar em `tilesets-config.json` e reconverter/re-extrair.
- [ ] **Step 3:** `dotnet test` PASS; `dotnet build` limpo.
- [ ] **Step 4: Verificação visual:** 5 tiers seed 101 lado a lado com `audit-shots/` — leitura de
  parede e chão aprovada, menos ruído. Screenshots "depois" no doc de auditoria.
- [ ] **Step 5:** Commit (`feat(content): curated per-biome material palette`).

---

### [ ] Task 6: Varredura de aceite visual + testes de regressão finais

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `docs/superpowers/specs/2026-07-07-map-beauty-v2-audit.md` (seção "Resultado" antes/depois) OU
  um novo `audit-shots/` set desta fatia; `backend/tests/KaezanArenaFable.Api.Tests/DungeonGeneratorTests.cs`
  (consolidar/portar `GroundPatchesHaveNoSingleCellIslands`)

**Interfaces:**
- Consumes: todos os fixes T3–T5. Produces: evidência de aceite + suíte de regressão completa verde.

- [ ] **Step 1:** Portar/confirmar `GroundPatchesHaveNoSingleCellIslands` no modelo de pilha (o
  smoothing de `familyOf` não mudou; o teste reconstrói família pelo ground do `Flat`). PASS.
- [ ] **Step 2:** Varredura sistemática no Map Lab: 5 tiers × seeds {101,202,303} + 1 boss/tier, zoom
  2×. Registrar screenshots "depois" contra as refs (`audit-shots/ref-*.png`).
- [ ] **Step 3:** Checklist de aceite do spec (marcar cada com screenshot): zero triângulo preto ·
  zero sprite cortado · zero terreno sem borda · costura contínua no zoom 2× · maciço lê como a
  família · uma run real T2 e T4 (backend Release + frontend) confirmando que o jogo reflete o Map Lab.
- [ ] **Step 4:** `dotnet test` + `npm test` + `dotnet build` + `npx ng build` limpos.
- [ ] **Step 5:** Commit (`test+docs: composition rewrite acceptance sweep`).

---

### [ ] Task 7: Rebaseline deliberado + docs + push

**Model · Effort:** Sonnet 5 · medium

**Files:**
- Modify: `docs/balance/golden_dungeon.txt` (rebaseline), `backend/src/KaezanArenaFable.Api/.data/replays/`
  (bateria regravada), `README.md` (se comportamento visível mudou), este arquivo (checkboxes)

- [ ] **Step 1:** Bateria completa: `dotnet test` + `npm test` (map-importer) + `dotnet build` +
  `npx ng build` limpos.
- [ ] **Step 2: REBASELINE total (esperado — formato E conteúdo mudaram):**

```powershell
dotnet run --project tools/BalanceSim -- --golden
dotnet run --project tools/BalanceSim -- --golden-check
```

Regravar a bateria de replays (apagar `.replay.json.gz` antigos, rodar o BalanceSim normal) e:

```powershell
dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays
```

Esperado: 0 divergências.

- [ ] **Step 3: Docs.** `README.md` só se comportamento visível mudou além do já documentado; TODAS as
  checkboxes deste plano marcadas; atualizar `docs/ROADMAP.md`/`FABLE_TRACK.md` conforme a origem.
- [ ] **Step 4: Commit final + push**

```powershell
git add -A docs docs/balance backend/src/KaezanArenaFable.Api/.data/replays README.md
git commit -m "docs: map composition rewrite delivered (deliberate golden rebaseline + replay battery)"
git push origin main
```

(Stage seletivo se houver mudanças alheias no working tree — conferir `git status` antes do `-A`.)

---

## Ordem e dependências

```
T1 (modelo backend) ─→ T2 (frontend render) ─→ T3 (brush montanha) ─→ T4 (brush chão)
                                                        │                    │
                                                        └────────┬───────────┘
                                                                 ↓
                                                        T5 (curadoria) ─→ T6 (aceite) ─→ T7 (rebaseline + push)
```

- T1 e T2 são a migração de formato (conteúdo idêntico) — precisam vir primeiro e juntos pro jogo
  seguir renderizando. T3 e T4 mudam a composição (sequenciais: ambos tocam `DungeonGenerator`). T5
  julga o visual com T3/T4 aplicados. T6/T7 fecham.

## Riscos conhecidos

- **Blast radius do modelo de pilha (T1/T2):** migrar formato com conteúdo equivalente PRIMEIRO
  (adapter `PackStacks`), testes verdes e render idêntico, antes de trocar composição. Dois passos
  verificáveis.
- **WallSet sem slot mask-0 (T3):** fallback ao `Bedrock` opaco (nunca preto); minerar o corpo se a
  leitura ficar ruim.
- **Reseed do `biomes.json` (T4/T5):** edições de admin descartadas — deliberado, documentado.
- **Curadoria subjetiva (T5):** consolidar nos defaults com justificativa, não deixar como edição
  volátil.
- **Golden/replay (T7):** rebaseline total, único; replays antigos deixam de re-simular (esperado).
