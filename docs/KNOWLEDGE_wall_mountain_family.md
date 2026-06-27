# KNOWLEDGE — Família de parede-montanha (H-08 / B4)

Insumo da **H-08** do `docs/roadmap/not started/roadmap_hunts.md`. Cataloga a família de
**parede-montanha** do Tibia (corpos + cantos reais + ridge) que falta hoje para a borda das
hunts deixar de ser linha fina de 1 tile. **Esta unidade só fornece os tiles**; apontar o
`Biomes.cs` para eles é o refino de B2 (`## Depois` #1 do roadmap_hunts).

## A família: ids `5630–5653` (massiço marrom)

24 aparências contíguas, `unpass+unsight`, 32px — um conjunto de parede completo. Descobertas
com o diagnóstico novo do extractor (ver abaixo) + leitura da máscara de alpha por borda
(qual lado é rocha vs. chão aberto). Já extraídas para
`frontend/public/assets/tibia/objects/` e tagueadas em `content-config.json` →
`manifest.json` nos grupos:

| Grupo `semantic` | ids | Papel |
|---|---|---|
| `wall.mountain.body` | 5630, 5631, 5638, 5639, 5640, 5641 | faces correntes (corpo do muro) |
| `wall.mountain.corner` | 5632, 5633, 5634, 5635, 5642, 5643, 5644, 5645 | **cantos convexos reais** (4 orientações × 2 estratos) |
| `wall.mountain.ridge` | 5636, 5637, 5646, 5647, 5648, 5649, 5650, 5651, 5652, 5653 | rocha sólida em relevo (bedrock + ridge de borda) |

### Classificação por orientação (máscara de alpha — lado opaco = rocha, lado vazado = chão)

**Corpos (`wall.mountain.body`)** — um lado aberto:

| id | borda aberta | papel no `BiomeDef` |
|---|---|---|
| 5630 | direita | `WallV` (muro N–S, rocha à esquerda) — marrom |
| 5640 | esquerda | `WallV` (rocha à direita) — marrom |
| 5631 | baixo | `WallH` (muro L–O, rocha em cima) — marrom |
| 5641 | cima | `WallH` (rocha embaixo) — marrom |
| 5638 / 5639 | dir / baixo | mesma coisa, **estrato cinza** (variante de pedra) |

**Cantos convexos (`wall.mountain.corner`)** — dois lados adjacentes abertos = canto de
verdade (o que falta hoje, já que na cave `WallCorner==WallH`):

| canto (chão na diagonal) | marrom | cinza |
|---|---|---|
| NO (open T+L) | 5632 | 5642 |
| NE (open T+R) | 5633 | 5643 |
| SE (open B+R) | 5634 | 5644 |
| SO (open B+L) | 5635 | 5645 |

**Ridge/bedrock (`wall.mountain.ridge`)** — tiles 100% sólidos com relevo de rocha. Servem de
miolo do maciço (bedrock) e de carimbo de ridge na primeira fileira de parede (B2/H-04).

## Como apontar o `Biomes.cs` (refino B2 — NÃO feito aqui)

`Domain/Biomes.cs:67-73` hoje degenera `WallCorner` para um body. Com a família acima dá para
fechar cantos reais, mas **o engine só guarda 1 `WallCorner`** por `BiomeDef` — para usar os 4
cantos orientados, `ClassifyWall` (`DungeonGenerator.cs:336`) precisaria escolher o canto por
vizinhança (refino de B2). Caminho mínimo, sem mexer no engine:

```csharp
// mountain (massiço marrom) — cantos reais + ridge
private const ushort MtnH = 5631, MtnV = 5630, MtnPole = 5648, MtnCorner = 5632;
private static readonly ushort[] MtnRidge = [5648, 5649, 5650, 5651, 5652, 5653];
```

Trocar `DirtH/DirtV/DirtPole/DirtCorner` do bioma `Cave` por `MtnH/MtnV/MtnPole/MtnCorner` já dá
borda espessa de montanha; o ganho pleno (canto certo em cada quina) vem quando `ClassifyWall`
passar a discriminar côncavo/convexo e indexar `wall.mountain.corner` por orientação.

## Variante de gelo: ids `6822–6837` (fora de escopo)

Há uma segunda família de montanha **gelo/neve** (`6822–6837`, branco/ciano + rocha cinza
6834–6837) com cantos próprios. Não taguei (não existe bioma congelado hoje); fica registrada
aqui caso um estrato de gelo entre no roadmap.

## Diagnóstico de curadoria: `--dump-walls`

Os tiles de parede são **sem nome** (invisíveis ao `--dump-names`). Para achá-los foi adicionado
um modo ao extractor que lista toda aparência `unpass+unsight` com dimensão de sprite e flags:

```bash
cd tools/AssetExtractor
dotnet run -- --things <things/1500 dir> --out <scratch> --config content-config.json --dump-walls
# → <scratch>/wall-candidates.txt  (id, name, cell, frames, elevation, flags)
```

Filtre por `32x32`, sem nome, sem flag `ground`, e procure faixas de ids contíguas (len ≥ 8) —
cada faixa é uma família de borda candidata.

## Nota de re-extração (importante)

O comando original que gerou o `manifest.json` (2483 objetos) **não está versionado** e usa
inputs ricos (loot de `monsters.json` + equipamento/items.xml) que não estão neste checkout —
re-rodar o extractor "cru" produz só ~1059 objetos e **encolheria** o manifest. Por isso a H-08
foi aplicada como **merge cirúrgico**: o extractor gerou os 24 PNGs + entradas de objeto da
família (mesmo code path, bytes idênticos), e só esses 24 ids + os 3 grupos `semantic` foram
mesclados no `manifest.json`, preservando intactas as 2483 entradas existentes (add-only). Quando
o comando completo de extração for resgatado, basta re-rodá-lo com o `content-config.json` já
atualizado para reproduzir o mesmo resultado.
