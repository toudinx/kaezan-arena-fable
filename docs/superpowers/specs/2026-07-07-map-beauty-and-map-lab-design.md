# Map Beauty + Map Lab — Design Spec

**Data:** 2026-07-07 · **Status:** aprovado em brainstorm (sessão 2026-07-07)
**Relação com roadmap:** absorve e materializa a sub-trilha "Editor de Biomas/Tiles" do
`docs/roadmap/ongoing/roadmap_dungeons.md` (LM-09 → LM-13) e destrava o "47-blob pleno"
que lá estava marcado como *"depende de arte/tiles"*.

> **Para o executor:** ao terminar a implementação de cada task do plano derivado deste spec,
> **marque a checkbox `[x]` da task** no arquivo de plano (e, ao fechar a fatia, marque as
> entradas correspondentes no `roadmap_dungeons.md`). Nunca conclua sem atualizar os checkboxes.

## Objetivo

Deixar os mapas procedurais **bonitos** (prioridade nº 1 desta fatia) e entregar a seção admin
**Map Lab** como ferramenta de iteração: preview de mapa por seed/tier + edição dos presets de
bioma (dados que o gerador consome), sem editor de tiles manual nesta fatia.

"Retocar mapa procedural" = **ajustar as regras/dados** (palettes, wall sets, densidades) com
preview e regeneração. O retoque muda todos os mapas futuros, nunca um mapa específico.

## Diagnóstico (por que os mapas estão feios)

Confirmado no código; as 4 correções são obrigatórias nesta fatia:

| Sintoma (screenshots 2026-07-07) | Causa raiz |
|---|---|
| Paredes sem continuidade ("várias paredes lado a lado") | Nenhum bioma tem `WallSet` — todos caem no fallback de 4 peças do `WallAutotile` (`Biomes.cs:30`, todos `null`). A infra 47-case blob já existe e está sem dado. |
| Chão "recortado" (xadrez de grama/terra) | `PaintTiles` faz `rng.Pick(biome.Ground)` por célula, sem camada de border/transição entre famílias de chão. |
| Pedras/objetos cortados | Sprites de decor 64px/multi-tile desenhados em célula única (risco já registrado no plano de authored maps, linha 908). |
| Muros em L no meio do mapa | Pilares procedurais (`PlacePillars`) resolvidos pelo mesmo fallback de 4 peças. |

## Decisão de abordagem

**Escolhida: dados curados do Remere's Map Editor (fork opentibiabr, feito pro Canary) como
fonte primária, com o mapa OTBM real como validação.**

O repo `https://github.com/opentibiabr/remeres-map-editor` carrega em `data/materials/` os
XMLs `borders.xml`, `brushs.xml`, `tilesets.xml` (+ subpastas): brushes de ground com border
sets por alinhamento (12 posições: n/s/e/w, cantos côncavos, diagonais), brushes de
parede/montanha com peças por orientação, e doodads multi-tile. É exatamente o conhecimento
"quais ids combinam como" — curado pela comunidade OT, no mesmo espaço de ids que já validamos
no pipeline de authored maps (OTBM Canary 3.x = appearance ids).

Alternativas rejeitadas:
- *Mineração puramente estatística do OTBM:* funciona, mas era a peça de maior risco; os XMLs
  do RME tornam a estatística desnecessária como fonte (fica só como validação).
- *Autorar wall sets à mão no admin:* trabalho manual brutal (47 slots × biomas × borders ×
  pares de chão) repetindo o que o RME já sabe.
- *Híbrido com override slot-a-slot na UI:* adiado — o `tilesets.json` commitado é legível e
  editável na mão; UI de override só se provar necessidade (YAGNI).

**Licença/fonte:** o RME é GPL. Os XMLs são lidos em tool-time de um **checkout externo**
(caminho no `config.json` do map-importer, padrão idêntico ao OTBM do canary) e **nunca são
commitados**; commitamos só o `tilesets.json` derivado (mapeamento factual de ids).

## Arquitetura — 3 pilares

```
RME materials XML (externo)     otservbr.otbm (externo, validação)
        │                               │
        ▼                               ▼
[P1] tools/map-importer/convert-tilesets.mjs ──► Content/tilesets.json (commitado)
                                                        │
                                                        ▼
[P2] TilesetRegistry (fail-fast) ──► BiomeDef (WallFamily/GroundFamilies)
        │                                   │
        ▼                                   ▼
     PaintTiles v2 (manchas + borders + WallSet) ──► DungeonFloor/MapDto (+Border A/B)
                                                        │
                                                        ▼
[P3] Map Lab (admin): GET/PUT biomes · POST mapgen/preview · GET tilesets
     preview canvas (AssetsService) + editor de preset (ContentStore.ReplaceBiomes)
```

### Pilar 1 — Conversor de tilesets (`tools/map-importer/convert-tilesets.mjs`)

- **Input:** XMLs de `data/materials/` do RME (checkout externo) + `appearance-flags.json` +
  `loadMap` do map-importer (validação).
- **Tradução:**
  - Border sets: 12 posições de edge do RME → nossos masks de border (tradução direta).
  - Wall sets: peças por orientação dos brushes de parede/montanha → 47 casos blob
    (`WallAutotile.Canonical`); masks sem cobertura → fallback por distância de Hamming pro
    caso mais próximo, marcados "sintéticos" no relatório.
  - Famílias de ground: os ground brushes do RME já agrupam ids por material (sem estatística).
- **Output:** `backend/src/KaezanArenaFable.Api/Content/tilesets.json`:

```json
{
  "families":   { "grass": { "kind": "ground", "items": [4515, 4516], "zOrder": 3200 } },
  "borderSets": { "grass->none": { "n": 4445, "e": 4446, "cnw": 4449, "dnw": 4453 } },
  "wallSets":   { "mountain": { "0": 1128, "16": 874, "20": 879 } }
}
```

(`borderSets` mantém os 12 edge names do RME — mais legível/testável; `wallSets` usa o mask
blob canônico do `WallAutotile`. Refinado durante o writing-plans.)

- **Validação (node --test):** border sets com as 12 posições traduzidas; wall sets com ≥40/47
  casos por família usada; **teste de predição**: re-resolver células de trechos conhecidos do
  otservbr usando só os sets convertidos → ≥95% de acerto de id; fail com relatório claro se um
  bioma referencia família inexistente.
- **Sprites:** ids novos entram em grupos `wallset.*`/`border.*` no `content-config.json` do
  AssetExtractor; fluxo gap-report → extract (padrão da Task 6 do authored maps).

### Pilar 2 — Painter/Engine

**`BiomeDef`** (serializável; biomas já vivem no `ContentStore`):
- `WallFamily: string` → resolvido pro `WallTileSet` no load (campo `WallSet` existente,
  hoje `null`). Os 4 campos legados (`WallH/V/Pole/Corner`) ficam como último fallback.
- `GroundFamilies: string[]` (1–3 famílias nomeadas; palettes de ids vêm do `tilesets.json`).
  É o que dá border: o painter sabe **qual família** está em cada célula.
- `Bedrock`, `Decor`, `Accent`, `Atmosphere` inalterados.

**`TilesetRegistry`** novo (`Content/`, padrão `PrefabRegistry`): carrega `tilesets.json` no
startup, fail-fast se bioma referencia família inexistente ou se palette contém decor >1×1.

**`PaintTiles` v2** (Pass 1 em 3 sub-passos):
1. **Manchas de chão:** família por célula via value noise de baixa frequência (grade ~6×6 com
   valores do run rng em ordem fixa, nearest) — regiões orgânicas em vez de xadrez. O id dentro
   da família continua `rng.Pick` (variação de textura do mesmo material é ok).
2. **Paredes:** `WallAutotile.Resolve` com o wall set minerado (zero mudança no autotiler).
   Bedrock/massif como hoje. Resolve também os muros em L dos pilares.
3. **Borders:** passada final; célula aberta com vizinho de família diferente → consulta o
   border set do par → escreve na camada nova. **2 slots por célula** (`BorderA`/`BorderB`;
   Tibia empilha até 2 em cantos côncavos). Par sem set (ex.: grama↔lava) → sem border.

**DTO/renderer:** `DungeonFloor` e `MapDto` ganham os 2 arrays de border; `renderer.ts` desenha
ground → borderA → borderB → decor → wall (ordem do Tibia), via `drawObject` existente.

**Decor sem corte:** extractor emite `width/height` do appearance; palettes (biomas e prefabs)
rejeitam ids >1×1 (fail-fast nos defaults via `TilesetRegistry`; gap report no export de
prefab). Decor multi-tile bem renderizado (doodads do RME) = fatia futura.

### Pilar 3 — Map Lab (admin)

**Endpoints** (`MetaEndpoints`, junto dos admin existentes — materializa LM-09):
- `GET /api/admin/biomes` → `BiomeRow[]`.
- `PUT /api/admin/biomes` → valida (famílias existem, tiers 1–5, densidades em faixa) e
  `ContentStore.ReplaceBiomes`. Runs novas usam na hora; runs em andamento não mudam
  (biome resolvido na construção — comportamento LM-08).
- `POST /api/admin/mapgen/preview` `{ tier, seed, floorIndex, bossFloor, biome? }` →
  `DungeonGenerator.Generate` (com prefabs do tier, mesmo caminho do `GameWorld`) →
  `MapDto.FromFloor`. `biome` inline = preview de rascunho antes de salvar. Nenhuma run criada.
- `GET /api/admin/tilesets` → famílias/sets/cobertura do `TilesetRegistry` (pro editor).

**Aba "Map Lab"** (`pages/admin/map-lab.ts`, padrão dos editores existentes — standalone,
template inline, signals):
- *Esquerda — preview:* canvas desenhando o `MapDto` via `AssetsService` (pinta 1× por
  regeneração, sem tick). Controles: tier, seed (+reroll), andar (1/2/boss), zoom/pan.
  Overlays opcionais: grid, blocked, POIs, contorno de sala prefab (`Room.PrefabId`).
- *Direita — preset do bioma:* famílias de chão e wall family (dropdowns do tilesets),
  palettes de decor/accent com thumbnails de sprite (padrão `item-icon`), densidades
  (sliders), atmosfera (pickers). Botões **Preview** (rascunho, mesmo seed), **Save** (PUT),
  **Reset to defaults**.
- *UX de referência:* organização de palette por brush/tileset do RME (inspiração pro tile
  picker; sem editor de tiles pintável nesta fatia).

## Determinismo e golden

- Todos os draws novos (noise de manchas) vêm do run rng em ordem de varredura fixa.
- **Um rebaseline deliberado** quando painter novo + defaults novos entram juntos:
  `--golden` + regravar bateria de replays + `--replay-check` (procedimento da Task 10 do
  authored maps). Nunca rebaselinar para "ficar verde" sem entender o diff.
- Preview do Map Lab não toca golden (geração pura, sem run). Edição de preset em runtime não
  toca golden (o golden mede os defaults canônicos de `Biomes.AllDefaults()`).

## Testes

- **P1 (node --test):** parse/tradução, cobertura ≥40/47, predição ≥95% contra o mapa real,
  fail-fast de família inexistente.
- **P2 (xunit):** determinismo (mesmo seed → mesmos `Ground/Wall/Border`); manchas (≥2 famílias
  num floor tier 2, nenhuma célula fora de `GroundFamilies`); borders (toda costura entre
  famílias com par existente tem border; célula interna tem 0); `TilesetRegistry` fail-fast
  (padrão `PrefabRegistryTests`); testes existentes do `DungeonGenerator` intactos (layout não
  muda, só pintura).
- **P3:** preview com mesmo body → `MapDto` byte-idêntico; PUT inválido → 400; frontend com
  `npx ng build` limpo + verificação manual.

## Verificação fim-a-fim (aceite da fatia)

Backend Release (`tools/run-backend.ps1`) + frontend preview:
1. Map Lab, tier 2 (Fort), seed fixo: chão em manchas com borders, paredes contínuas, zero
   pedra cortada — **screenshot antes/depois** vs. as capturas de 2026-07-07.
2. Editar densidade de decor → Preview (mesmo seed) → Save → run tier 2 real com preset aplicado.
3. Runs tiers 1 e 3–5: os 5 biomas renderizam sem buraco de sprite/magenta.

## Rollout — 3 entregas commitáveis independentes

Convenção do projeto: **toda task do plano declara Model · Effort** (preferência registrada).
Distribuição por perfil: **Fable/Opus** para o que é cross-cutting/algorítmico com garantias;
**Codex (GPT-5.5)** para tasks pequenas e bem-especificadas de frontend/plumbing.

| Entrega | Conteúdo | Valor visível | Model · Effort (indicativo) |
|---|---|---|---|
| ① Tilesets | `convert-tilesets.mjs` + `tilesets.json` + extração de sprites | dados prontos + relatório de cobertura | Fable 5 · high (conversor/tradução blob) · Codex para extractor/config |
| ② Painter | `TilesetRegistry` + `BiomeDef` v2 + `PaintTiles` v2 + border layer + DTO/renderer + **rebaseline** | o jogo fica bonito, sem UI nova | Fable 5 · high (painter/borders) · Sonnet · medium (registry, fail-fast) · Codex para renderer.ts |
| ③ Map Lab | endpoints + aba admin | ferramenta de iteração | Sonnet/Opus · medium (endpoints) · Codex · medium (aba admin, canvas, editor) |

Se ③ atrasar, ① e ② já resolvem a motivação original. O plano de implementação detalhado
(formato roadmap com prompts NN, Model · Effort por task, dependências e ondas) sai do
writing-plans a partir deste spec.

## Fora do escopo (fatias futuras registradas)

- Editor de tiles pintável no admin + exportar prefab (mini map editor).
- Override slot-a-slot de wall set na UI (por ora: editar `tilesets.json` na mão).
- Decor/doodads multi-tile renderizados inteiros (doodad brushes do RME como fonte).
- Autoria de cidades/quests no admin (docs de anatomia de 2026-07-06 são a referência).
- Switch de tema por sala prefab (prefab manter visual próprio já acontece via stamp LM-08).

## Riscos residuais

- Tradução 12 posições RME → 47 blob pode ter ambiguidade em montanha → mitigada pelo teste de
  predição contra o mapa real.
- Volume de sprites novos → mitigado pelo gap-report antes de escrever qualquer prefab/tileset.
- Manchas de chão podem pedir tuning de frequência/tamanho → constantes no `GameConfig`,
  iteráveis no Map Lab.
