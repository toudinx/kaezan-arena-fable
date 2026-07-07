# Map Beauty v2 — Audit: Verified Root Causes per Artifact Class

**Data:** 2026-07-07 · **Autor:** Fable 5 (Task 1 do plano `2026-07-07-map-beauty-v2-audit-and-fixes.md`)
**Método:** relatório de gaps programático + análise das células autoritativas via `POST /api/v1/admin/mapgen/preview`
(5 tiers × seeds {101, 202, 303} + 1 boss floor por tier) + screenshots do Map Lab (zoom 2×) + rastreamento
das causas no código + **render de dados reais do Tibia com o próprio renderer do jogo** (ground-truth).

Screenshots em `audit-shots/`: `t<tier>-s<seed>[-boss].png` (20, estado ATUAL) + `ref-*.png`
(5 regiões reais do `otservbr.otbm` renderizadas com o `AssetsService` do jogo) + `t2-s101-gamelike.png`
(o mesmo mapa gerado, renderizado pelo caminho do jogo com backdrop de bioma).

---

## Descoberta-chave (muda o direcionamento das tasks 4–6)

**O renderer e os sprites do jogo são fiéis.** Renderizei 5 regiões REAIS do mapa do Tibia
(`otservbr.otbm`) com o mesmíssimo `AssetsService.drawObject` do jogo:

- `ref-thais-city.png` / `ref-mintwallin.png` — cidades: paredes de pedra com topo 3D, casas de tijolo
  legíveis, ruas de arenito, bordas de grama. **Impecável.**
- `ref-troll-cave.png` / `ref-rotworm-cave.png` — cavernas: chão com **costuras orgânicas suaves** entre
  materiais (exatamente o alvo que o usuário apontou na screenshot da caverna de cristal). **Impecável.**
- `ref-orc-throne.png` — sala de boss (fortaleza orc). **Impecável.**

Conclusão: **nenhum artefato dos nossos mapas vem de defeito de renderer ou de extração de sprite.**
Todos vêm de (a) o GERADOR compor dados que não seguem a forma com que o Tibia monta os tiles, ou
(b) o preview do Map Lab não replicar o backdrop do jogo. Isso redireciona as tasks: o lado "corrigir
render" das tasks 4/5 quase não é necessário — o trabalho é de DADOS (gerador/conteúdo).

---

## Step 1 — Relatório de gaps (programático)

`node convert-tilesets.mjs --report-only` → **0 sprites faltando.** 11 famílias, 11 border sets, 3 wall sets.
`node export.mjs --report-only` → 8 prefabs, **0 bloqueados por gap** (só faltam *species* de spawn:
Stalker, Orc Leader, Destroyer, Grim Reaper, Hellspawn — criaturas, não tiles).

Cross-check de TODOS os ids que o backend emite fora do tilesets (famílias + border/wall sets +
`Content/prefabs/*.json` camadas ground/wall/decor + palettes de `Domain/Biomes.cs`) contra o
`manifest.json` e `appearance-sizes.json` (500 ids distintos):

### (a) IDs ausentes do manifest: **NENHUM (0).**

Não há gap de extração. A hipótese "triângulos pretos = sprite faltando" está **refutada**.

### (b) IDs multi-tile (w>1 || h>1) referenciados: **124.**

Distribuição (nenhum é palette `Decor`/`Accent` de bioma — essas são 1×1, guardadas por
`Biomes.ValidateDefaults()`):

- **Wall sets de família (esperado):** `mountain` (1085, 4815, 4816), `crystal wall`
  (14863/14864/14866/14870, 15290–15296), `mossy wall mountain` (15347–15461, 15061–15392). Paredes do
  Tibia são legitimamente altas (corpo + topo). **A premissa da Task 4 Step 2 ("toda peça de wall/border
  é 1×1") está ERRADA para paredes e para os foot-borders de parede** — a validação 1×1 só pode valer
  para border sets de CHÃO (os `*->none`), nunca para wall sets nem para `*->OPEN`.
- **Decor/wall de PREFAB (a fonte real de "sprites cortados"):** ex. `orc-warlord-throne.decor`
  (2928/2929/2931, 4415–4425), `demon-hall.decor` (20670–20690), `mintwallin-block.wall` (1270–1301,
  2108–2164, 3619–3688). Objetos multi-tile são normais dentro de um crop de prefab; o problema é COMO
  o prefab guarda/desenha (ver classe "Estruturas ilegíveis").
- `1128` (mountain body) e `1116` (stone corner) — usados também como massa do maciço (ver "Triângulos").

Lista completa em `audit-shots/` (rodar o cross-check é reproduzível; script one-off descartado).

---

## Classe: Triângulos pretos (Uruk Fort e todos os tiers com maciço `mountain`/`crystal`)

**Evidência:** `t2-s101.png`, `t2-s303.png`, `t4-s101.png` (triângulos pretos nas bordas diagonais do
maciço). Comparar com `t2-s101-gamelike.png`.

**Causa raiz confirmada (no código + visualmente):**
1. **Não é gap de sprite** (0 ids ausentes) nem defeito de renderer (dados reais renderizam perfeito).
2. O maciço é construído em `DungeonGenerator.PaintGround` (`DungeonGenerator.cs:1180-1190`): célula
   bloqueada que toca chão = parede de borda com peça autotile da família (`WallAutotile.Resolve`, linha
   1183); célula totalmente cercada = **bedrock** = `biome.Bedrock` (416, opaco) + `biome.WallCorner`
   (**1116**, opaco). Por isso o INTERIOR do maciço é `1116` cinza (não crystal/mountain): em T2 s101,
   **360/441** células de parede são `1116` (bedrock), só ~81 são peças de família.
3. As peças de borda diagonal da família `mountain` (4815/4816/1085) são sprites 64px com **metade
   transparente** (talude). Nas bordas diagonais do maciço, a metade transparente não é respaldada por
   rocha opaca.
4. **O preto puro é um artefato SÓ do Map Lab:** `map-lab.ts` faz `clearRect` (canvas transparente,
   shell escuro atrás). O renderer do jogo (`renderer.ts:782-788`) preenche um backdrop de bioma
   (`k=0.42 × tint`) ANTES dos tiles. Em `t2-s101-gamelike.png` os triângulos pretos viram triângulos
   de **rocha esverdeada esmaecida** — some o preto, mas **o buraco diagonal permanece** (a peça de
   talude não cobre a célula).

**Fix (Task 4):** duas coisas, ambas de DADOS/preview (não de renderer core):
  (i) **Map Lab:** pintar o backdrop de bioma no `map-lab.ts` igual ao `renderer.ts` (elimina o preto
      do preview — o preview passa a espelhar o jogo).
  (ii) **Buraco real in-game:** garantir respaldo opaco atrás de qualquer pixel transparente de parede
      de borda — a correção limpa é a célula de parede de borda receber `biome.Bedrock` (rocha opaca)
      como ground em vez de `biome.Ground`, OU o maciço estender bedrock sob a borda. Anotar repro
      mínimo com o mask exato ao implementar.

**Notas:** os mesmos taludes diagonais 4815/4816 aparecem como NUBS isolados invadindo a arena — é a
classe "boulders cortados" abaixo (mesma raiz).

---

## Classe: Boulders / pedras "cortadas" (Uruk Fort)

**Evidência:** `t2-s101.png` (fatias de rocha diagonal cinza na arena), `t4-s101.png`.

**Causa raiz confirmada:** **NÃO** são decor multi-tile. O decor de Uruk Fort é `CaveRocks`
(1772–1775), todas **1×1** (confirmado: não estão no `appearance-sizes.multiTile`; análise mostrou
`multi-tile DECOR ids in-map: none` para todos os T1/T2). As "pedras cortadas" são **células de parede
`mountain` isoladas/penínsulas** (mask com poucos vizinhos abertos) cuja peça de borda diagonal (4815/
4816, 64px, ancorada bottom-right) lê como um pedaço de rocha fatiado invadindo a sala — a mesma peça e
a mesma raiz dos triângulos pretos.

**Fix (Task 4):** mesma correção da classe triângulos (respaldo opaco + backdrop). Adicionalmente,
avaliar em curadoria (Task 6) se o gerador deve evitar nubs de parede de 1 célula (suavizar o maciço).
A guarda 1×1 do conversor (Task 4 Step 2) **não** se aplica aqui — não há decor multi-tile envolvida.

---

## Classe: Lava sem borda (Scaled Lair T4, Echoing Abyss T5) — o pior artefato de costura

**Evidência:** `t4-s101.png` (poças de lava = blocos retangulares laranja/vermelho colados na rocha,
zero transição). Contraste com `ref-troll-cave.png` (costuras orgânicas reais do Tibia) e a screenshot
de referência do usuário (caverna de cristal).

**Causa raiz confirmada (no código):** lava é a palette `Accent` pintada na camada **Decor**, fora do
sistema de famílias. `DungeonGenerator.PaintTiles` (`DungeonGenerator.cs:1114`):
`PaintClusters(floor, room, rng, biome.Accent, ...)` escreve `floor.Decor`. O passe de bordas (linha
1120-1121) só roda sobre `GroundFamilies` — **acentos nunca recebem borda.** Confirmado nos dados:
T4 s101 tem **48 células de lava, todas em Decor, 0 em Ground**; a lava não tem transição para a rocha.

**Fix (Task 2):** mover acentos de terreno para o sistema de famílias (`AccentFamily` + família `lava`
minerada do RME + `PaintAccentPatches` escrevendo GROUND + índice de família → `BorderAutotile` dá as
bordas RME). Exatamente o desenho da Task 2.

**Notas:** este é o item que mais destoa visualmente e o que o usuário destacou. O alvo é a lava com
borda orgânica em toda a volta, como o cristal↔pedra da referência.

---

## Classe: Costuras de terreno (famílias de chão)

**Evidência/dados** (contagem por mapa, seeds 101/202/303 + boss):

| Padrão | Ocorrência | Observação |
|---|---|---|
| **Lava sem borda** (classe acima) | 48 céls/mapa (T4/T5) | dominante; é Task 2, não Task 3 |
| (i) peça ausente / costura nua (`borderA==0` com vizinho de z maior) | **10 no total** (T1 s202: 7; T4 s202: 3; resto 0) | ver mecanismo abaixo |
| (ii) ilha de 1 célula (ruído Voronoi) | **1 no total** (T1 s202) | quase inexistente |
| (iii) peça errada (corner onde devia edge) | não detectado na análise de células | — |

**Causa raiz confirmada da costura nua (padrão i):** **não** é sprite faltando (ids no manifest). É o
**cap de 2 slots vencido pelo foot-border de PAREDE.** Em `BorderAutotile.Paint`
(`BorderAutotile.cs:41,97-104`), a família de parede entra na ordenação com z-order da montanha
(9900, o MAIOR) e é resolvida PRIMEIRO; se a célula toca 2+ paredes bloqueadas, o foot-border
(`wall->OPEN`) preenche `BorderA` **e** `BorderB` (`pieces.Count >= 2 → break`) e a costura da família
de chão de z menor (ex. dirt sobre cave) fica sem slot. As 7 células nuas de T1 s202 são exatamente
cave(z200)↔dirt(z400) na borda do maciço, com o foot-border da montanha consumindo os dois slots.

**Fix (Task 3):** o smoothing de maioria previsto ataca o padrão (ii) — que é **raro** (1 ocorrência):
o teste `GroundPatchesHaveNoSingleCellIslands` pode quase passar já; considerar uma definição de ilha
mais estrita (penínsulas de 2 células) ou aceitar que o ganho do smoothing é menor que o previsto. O
padrão (i)/(iv) é decisão de PRIORIDADE de slot: hoje o foot-border de parede ganha — é defensável
(a rocha ao pé da parede importa), então **documentar e não perseguir**, OU (se a auditoria visual
achar gritante) reordenar para a costura de chão ganhar um slot. Ocorrência atual: 10 células em 15
mapas, todas coladas no maciço — **não é gritante**; recomendo documentar e manter.

---

## Classe: Estruturas autorais ilegíveis (a "casa" confusa)

**Evidência:** `t4-s202.png` (estrutura de tijolo vermelho em labirinto, canto inferior-esquerdo — é o
prefab `mintwallin-block`; e a plataforma de pedra com escada acima é `ancient-temple`). Comparar com
`ref-mintwallin.png` (a MESMA região real: casa de tijolo LEGÍVEL, chão de arenito limpo, cama, escada).

**Causa raiz confirmada:** o prefab é o `mintwallin-block` (theme `city`, crop 32388,32096,z15).
Renderizando a região REAL com o nosso renderer (`ref-mintwallin.png`) a casa é perfeitamente legível
→ **não é defeito de renderer.** A perda de legibilidade está no EXPORT do prefab (`Content/prefabs/
mintwallin-block.json` guarda 1 item por camada por célula: `ground/wall/decor`) — a treliça de paredes
internas de tijolo lê como labirinto porque o pareamento parede+topo / o chão correto se perdeu no
crop, OU o crop escolhido é intrinsecamente confuso (interior denso de paredes).

**Fix (Task 5):** comparar célula-a-célula o crop no jogo vs a região real; se o exporter guarda camadas
insuficientes → corrigir `lib/prefab.mjs` + re-exportar (revalidar TODOS os 8 prefabs); se o render de
camadas está certo mas o crop é ruim → **re-curar** (novo crop com mesmo tema/tier, id `prefab:*`
estável). Nota: a região real também tem postes azuis de diamante (objeto nativo de Mintwallin) que
poluem — a re-curadoria deve evitá-los.

---

## Classe: Crystal wall "chapado" (T4/T5)

**Evidência:** `t4-s101.png`, `t5-s101.png` (parede de cristal teal na borda; interior do maciço cinza).

**Causa raiz confirmada:** o foot-border de cristal (`crystal wall->OPEN`) **ESTÁ sendo pintado** (é
z-order 9900, resolvido primeiro em `BorderAutotile`; visível como pé teal/dourado ao pé da parede).
Logo **não** é foot-border faltando. O "chapado" tem duas causas de DADOS:
1. O **interior do maciço é bedrock `1116` (pedra cinza)**, não cristal — só a casca de 1 célula na
   borda é da família cristal (`DungeonGenerator.cs:1188-1189`). Em T4/T5 o maciço deveria ler como
   cristal, e lê como pedra cinza genérica.
2. Peças da família para masks só-N / só-W desenham corpo sem face (esperado — o Tibia não tem face
   N/W). Isso é correto e não é o problema principal.

**Fix (Task 6):** recomendação forte — a célula de bedrock usar o CORPO da família de parede (mask 0,
ex. crystal 15290) em vez do genérico `1116`, para o maciço inteiro ler como cristal/mossy no tier
certo. Alternativamente, reavaliar a família de cristal se a leitura continuar ruim. (Curadoria roda por
último, com os fixes 2–5 aplicados.)

---

## Alvo (target state) — referências

- **Usuário (2026-07-07):** screenshot de caverna de cristal real do Tibia — chão de cristal encontra o
  chão de pedra por bordas autotile suaves e orgânicas; clusters de cristal ficam POR CIMA como decor.
  "As bordas deveriam ficar assim, não do jeito que está."
- **`ref-troll-cave.png` / `ref-rotworm-cave.png`:** costuras orgânicas reais entre materiais de chão de
  caverna — o alvo para as costuras de família E para a lava com borda (Task 2/3).
- **`ref-thais-city.png` / `ref-mintwallin.png`:** casas legíveis com paredes de topo 3D — alvo para os
  prefabs (Task 5).
- **`ref-orc-throne.png`:** sala de boss real — alvo para os boss floors.

---

## Mapa artefato → task

| Classe | Task | Ação principal |
|---|---|---|
| Lava sem borda | **2** | acentos viram família com borda |
| Costura nua (cap/wall foot) | **3** | documentar; smoothing só ataca ilhas (raras) |
| Triângulos pretos | **4** | backdrop no Map Lab + respaldo opaco na borda do maciço |
| Boulders cortados | **4** | mesma raiz (peças diagonais de parede); guarda 1×1 só p/ borders de CHÃO |
| Estruturas ilegíveis | **5** | re-export/re-curar `mintwallin-block` (renderer está OK) |
| Crystal wall chapado | **6** | bedrock usar corpo da família; re-curar se preciso |

**Correções à premissa do plano (verificadas):**
1. **Zero gaps de manifest** — Task 4 Step 1 não fecha gap nenhum (não existem); foca em backdrop +
   respaldo de borda.
2. **Guarda 1×1 (Task 4 Step 2) NÃO pode incluir wall sets nem `*->OPEN`** — paredes e foot-borders são
   legitimamente multi-tile (124 ids). Só border sets de CHÃO (`*->none`) são 1×1.
3. **Ilhas de Voronoi são raríssimas (1 em 15 mapas)** — o ganho do smoothing da Task 3 é menor que o
   previsto; a costura nua real vem do cap de 2 slots no pé do maciço, não de ruído.
4. **Renderer e sprites são fiéis** (provado com dados reais do Tibia) — o lado "corrigir render" das
   tasks 4/5 é quase desnecessário; o trabalho é de dados/conteúdo/preview.

---

## Resultado (preenchido na Task 7)

_(antes/depois por classe após as tasks 2–6)_
