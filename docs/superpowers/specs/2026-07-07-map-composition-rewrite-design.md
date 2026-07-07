# Map Composition Rewrite — Simple-but-Beautiful Generator (Design)

**Data:** 2026-07-07 · **Autor:** Opus 4.8 (brainstorming) · **Status:** aprovado o direcionamento; spec em revisão

## Contexto e problema

O `kaezan-arena-fable` já minera as brushes do Remere's (`tools/map-importer/lib/rme.mjs` →
`Content/tilesets.json`: famílias, border sets, wall sets 47-slot), gera floors proceduralmente
(`Engine/DungeonGenerator.cs`) e renderiza com sprites reais do Tibia via `AssetsService`. A
auditoria `2026-07-07-map-beauty-v2-audit.md` **provou** que renderer e sprites são fiéis (regiões
REAIS do `otservbr.otbm` renderizam impecáveis com o próprio renderer do jogo). **Toda a feiura vem
do gerador compor tiles de um jeito que o Tibia não usa.**

Diagnóstico do usuário (2026-07-07), confirmado no código:

1. **Paredes = empilhar sprite.** O maciço é bedrock cinza opaco (`1116`) no interior + uma peça de
   borda de parede por célula na beira. Não há a gramática do Tibia (corpo de maciço + beira
   auto-bordeada com respaldo opaco). As peças de talude diagonal (64px, metade transparente) sobre
   um interior que não é da família → **triângulos pretos, pedras cortadas, maciço cinza**.
2. **Chãos = espalhar variante aleatória.** Materiais são escolhidos e distribuídos por célula até
   encher; accents (lava) são pintados na camada `Decor` **fora** do sistema de famílias, então nunca
   recebem borda → **blocos de lava colados sem transição**, costuras quebradas.
3. **Ruído de material.** Materiais demais concorrendo na mesma tela (vários grounds + accent + decor)
   — falta calma/coesão.

O alvo já está provado na auditoria: `audit-shots/ref-troll-cave.png` / `ref-rotworm-cave.png`
(costuras orgânicas entre materiais de chão) e cidades legíveis. **"Simples porém bonito" =
compor como o RME compõe + enxugar material por bioma.**

## Decisão de abordagem

Reescrita **limpa** da camada de composição de tile (Abordagem 1 do brainstorming), fiel à gramática
do RME, aceitando **um rebaseline deliberado** dos golden replays (FF-01) como custo conhecido. As
alternativas rejeitadas: consertos incrementais (a auditoria v2 já provou que ficam aquém) e montagem
por chunks autorais (melhor a longo prazo, mas exige o pipeline RME/OTBM — Subsistema B — que fica
para depois).

## Escopo

**Reescrito (só a camada de composição):**
- Modelo de tile do `DungeonFloor` + `Engine/GameDtos.MapDto` + consumo em `frontend/.../renderer.ts`
  e `frontend/.../types.ts`.
- `Engine/BorderAutotile.cs` e `Engine/WallAutotile.cs` → um compositor unificado fiel ao RME.
- `Domain/Biomes.cs`: paleta enxuta por bioma; accents viram famílias.

**Intacto (o usuário não reclamou disso):**
- Layout: placement de salas, erosão celular (`ErodeRoom`/`ErodeArena`), anfiteatro, side-pockets,
  pilares, spanning-tree de corredores.
- `familyOf` (jittered-Voronoi + smoothing de maioria), slot de prefab (`StampBlocked`), spawns
  (`mouths`), chests, santuários, ladders.
- Determinismo (`Rng` em ordem de varredura fixa), tick de 100ms, frequência de envio do mapa
  (nullable, só na troca de floor).

## Arquitetura

### 1. Modelo de dados — duas pilhas por célula

Hoje cada célula é `(Ground, Wall, Decor, BorderA, BorderB)` — 5 slots fixos; o **cap de 2 bordas**
é o que estrangula a costura de chão (o pé-de-parede consome os dois slots). Substituir por **duas
pilhas ordenadas por célula**, espelhando como o Tibia empilha itens e preservando a render em 2
passadas que o `renderer.ts` já faz:

- **`flat[]`** (plano do chão, desenhado **sob** as criaturas — passada 1 atual, `renderer.ts`
  ~791–802): `ground` → bordas (sem cap) → decor de chão.
- **`tall[]`** (desenhado na passada **y-sorted** com as criaturas — passada atual ~870+): paredes /
  objetos altos, pra oclusão de profundidade continuar correta.

`DungeonFloor`: `ushort[] Ground/Wall/Decor/BorderA/BorderB` → `ushort[][] Flat` e `ushort[][] Tall`
(jagged; uma pilha curta por célula; `Blocked`/`Rooms`/`Entry`/… inalterados). `MapDto`: os 5 arrays
viram `ushort[][] Flat, ushort[][] Tall`. Custo de wire irrelevante — mapa enviado só na troca de
floor. O modelo passa a ser **idêntico ao dos prefabs/OTBM** (`ground/wall/decor` empilháveis),
pavimentando o Subsistema B de graça.

> **Nota de migração:** os prefabs (`Content/prefabs/*.json`) hoje guardam `ground/wall/decor` como
> arrays chatos. `StampBlocked`/o stamp de prefab passa a empurrar `ground`+`decor` no `flat[]` e
> `wall` no `tall[]` da célula. O formato de arquivo do prefab **não muda** nesta fatia (a conversão
> é no load), preservando os ids estáveis `prefab:*`.

### 2. Brush de montanha (paredes/maciço) — a correção do "empilhar sprite"

Regra única para **toda** célula bloqueada, resolvida pela máscara blob-47 (a `WallAutotile.Mask`
atual já produz isso):

- **Respaldo opaco sempre.** `flat[]` da célula bloqueada recebe um ground opaco (o `Bedrock` do
  bioma). Assim, qualquer pixel transparente da peça de parede mostra rocha, não preto → **fim dos
  triângulos pretos**, sem depender do backdrop do preview.
- **Corpo vs beira pela máscara.** `tall[]` recebe a peça do WallSet da família para o mask blob:
  - **mask 0** (célula totalmente cercada = interior do maciço) → a peça de **corpo** da família
    (o slot mask-0 do WallSet, ex. mountain/crystal body) — **não** o `WallCorner`/`1116` genérico.
    O maciço inteiro passa a ler como a família (cristal/montanha), não pedra cinza.
  - **masks de beira** → a peça de talude/topo correspondente (como hoje), agora sempre sobre o
    respaldo opaco.
- Fallback: se o WallSet da família não tiver o slot mask-0 (família sem corpo), cair no `Bedrock`
  opaco como corpo (nunca preto). Famílias sem WallSet mantêm o fallback 4-peças (`WallAutotile.Fallback`).

Isto substitui o trecho de `DungeonGenerator.PaintGround` (~1180–1190) que hoje decide "borda vs
bedrock" e o uso de `1116`.

### 3. Brush de chão (regiões + costura) — a correção do "espalhar variante"

- **Região = 1 material** com variação sutil dentro (rng.Pick dos `Items` da família em ordem de
  varredura fixa). `familyOf` (Voronoi + smoothing) já entrega a região; mantido.
- **Costura auto-bordeada sem cap.** A resolução de borda do `BorderAutotile.ResolvePieces` (máscara
  de 8 bits, cantos côncavos, edges, diagonais, z-order) é **mantida**, mas empilha **todas** as
  peças no `flat[]` (fim do cap de 2). A regra RME "família de z-order maior borda sobre a menor"
  continua; o pé-de-parede deixa de roubar o slot da costura de chão porque não há mais slot único.
- **Accents são famílias de 1ª classe.** Lava (e afins) saem da camada `Decor` e viram família de
  ground com border set próprio (minerada do RME: brush `lava`, z-order 7700). `PaintAccentPatches`
  escreve `ground` + índice de família; a costura sai de graça do passe de bordas. (Absorve a Task 2
  do plano v2.)

### 4. Curadoria de material — a correção do "ruído"

Passe de dados em `Domain/Biomes.cs`: reduzir `GroundFamilies` por bioma a um conjunto curado
pequeno (alvo: 1 primária + 1 secundária de contraste), 1 `AccentFamily` onde fizer sentido, 1
`WallFamily`. Menos variantes concorrendo na tela. Curadoria feita no Map Lab (Preview draft),
consolidada nos defaults com comentário do porquê (padrão da fatia anterior).

## Determinismo (invariante inegociável)

- Dentro da geração, só o `Rng` da run em ordem de varredura fixa. Os passes de composição
  (montanha, borda) são **resolução pura, rng-free** (como o `BorderAutotile` atual). O único uso de
  rng é o `Pick` de variante de ground, que já existe e mantém a ordem.
- Proibido `Random`, `DateTime.Now`, `Guid.NewGuid()`, iteração de coleção sem ordem estável.

## Verificação (como medir "bonito")

Reusar a metodologia da auditoria — screenshots do Map Lab (5 tiers × seeds {101,202,303} + 1 boss
por tier, zoom 2×) lado a lado com as refs reais do Tibia (`audit-shots/ref-*.png`). Testes xunit
(TDD, escritos antes) que travam as regressões:

- **`NoBlockedCellLacksOpaqueBacking`** — toda célula bloqueada tem um ground opaco no `flat[]`
  (fim dos triângulos pretos, por construção).
- **`MassifInteriorUsesFamilyBody`** — célula bloqueada de mask 0 usa o corpo da família, não `1116`.
- **`EverySeamCellCarriesABorderPiece`** — célula aberta com vizinho de z maior tem ≥1 peça de borda
  no `flat[]` (portado do v2; agora sem cap).
- **`AccentPatchesAreBordered`** — poças de accent (lava) têm borda em toda a volta (portado do v2).
- **`GroundPatchesHaveNoSingleCellIslands`** — smoothing mantém regiões contínuas (portado do v2).
- Testes de byte-identidade legados que dependiam do formato de 5 arrays são **reescritos** para o
  modelo de pilha (rebaseline deliberado — ver abaixo).

Critério de aceite (checklist visual, screenshot de cada no doc de auditoria, seção "depois"):
zero triângulo preto · zero sprite cortado · zero terreno sem borda · seguir qualquer costura no
zoom 2× sem quebra · maciço lê como a família (não cinza) · uma run real T2 e T4 confirmando que o
jogo reflete o Map Lab.

## Golden / replay

**Um rebaseline deliberado** ao final. Todo floor muda de formato (5 arrays → pilhas) e de conteúdo
(composição nova), então não há caminho byte-idêntico a preservar — diferente do v2, aqui o rebaseline
é total e esperado. Regravar a bateria de replays; `--replay-check` deve dar 0 divergências após.

## Riscos conhecidos

- **Blast radius do modelo de pilha:** a migração `MapDto`/`renderer.ts`/`types.ts` toca o caminho de
  render do jogo inteiro. Mitigação: migrar o formato **primeiro** com conteúdo equivalente (adapter
  que empacota os 5 arrays atuais nas pilhas) e testes verdes, e só depois trocar a composição — dois
  passos separados, cada um verificável.
- **WallSet sem slot mask-0:** algumas famílias podem não ter corpo minerado. Fallback ao `Bedrock`
  opaco (nunca preto); se a leitura ficar ruim, minerar o corpo no `tilesets-config.json`.
- **Curadoria subjetiva:** "bonito" é julgado no Map Lab; consolidar escolhas nos defaults com
  justificativa, não deixar como edição de admin volátil (reseed descarta).
- **Determinismo do smoothing/borda:** manter os passes rng-free e double-buffered; qualquer novo
  passe segue a mesma disciplina.

## Fora de escopo (fica para depois)

- Subsistema B (import RME/OTBM de hunts/quest/boss/cidades, editor, cobertura de sprite).
- Montagem por chunks autorais (Abordagem 3).
- Layout/planta dos mapas (erosão, corredores) — só a composição muda.
