# Gameplay: cards com peso, baseline autossuficiente, chaos legível

**Data:** 2026-07-06 · **Status:** aprovado (brainstorm com o usuário)

## Problema (feedback de gameplay, 2026-07-06)

1. **Escolhas de card demais por andar.** Beats atuais: elite morto, cada baú aberto
   (baú dropa a cada 6 kills → 3–6 por andar), Echo Sanctuary e fim de andar. Cap de
   `MaxCardChoicesPerRun = 9` numa run de 2 andares = 4–5 overlays por andar. A escolha
   virou rotina, não decisão.
2. **Dependência total das cards.** O tuning MG-08 dos `MonsterStatLines` foi calibrado
   contra o poder acumulado de ~9 escolhas + stacks; sem cards o baseline não fecha o
   andar. A intenção de design é card = vantagem estratégica/build, não requisito.
3. **Chaos desordenado.** Em `TryMonsterAttacks` (GameWorld.cs), mob casta spell de área
   sempre que `dist <= range && LoS && chance`: nova self-centered (radius até 3) pinta
   FX em volta do mob com o player a 6 tiles ("UE no vazio"), e cones usam
   `Min(attack.Length, 5)` — gigantescos. FX em todo tile do shape mesmo sem atingir nada.
   O jogo quer chaos denso, mas todo efeito na tela precisa significar ameaça.

## Decisões (aprovadas)

- **Cadência:** escolha de card só entre andares + Echo Sanctuary (opcional). Baús e
  elites deixam de abrir oferta.
- **Power budget:** baseline (kit + level-up + equip) carrega o clear card-less;
  cards recalibradas como vantagem de build. Calibração via BalanceSim.
- **Chaos:** gate de intenção com margem de proximidade (não precisa acertar o player,
  mas precisa ser perto dele) + cap de tamanho de shape para mobs comuns/elite; boss
  isento (UE grande é assinatura dramática de boss). Sem telegraph (direção mantida).
  Referência de tamanho: fire wave do dragão do Tibia (~reach 3) "ou menor".

## Frente 1 — Cadência de cards

Beats que abrem oferta passam a ser somente:

| Beat | Oferta | Nota |
|---|---|---|
| Fim do andar (escada) | 1 garantida | `OfferChoiceOnFloorClear` mantido |
| Echo Sanctuary (1/andar, no minimapa) | 1 opcional | jogador escolhe desviar até ele |

Mudanças nos demais beats:

- **Elite morto:** não chama mais `OfferCardBeat()`; dropa recompensa direta
  (gold + Echo material) para manter o beat de dopamina sem overlay.
- **Baú (a cada 6 kills):** loot puro (gold + item de tier + material), sem oferta.
  Cursed chest mantém ambush/slow/mimic, paga em materiais extras
  (`CursedChestMaterialDrops`); a *blessed offer* é removida
  (`BlessedOfferProgress`/`_offerBlessed` saem do fluxo de baú).
- **Level-up:** drip automático de common (`GrantAutoStatus`) permanece como está.

Constantes:

- `MaxCardChoicesPerRun` 9 → **4** (3 esperadas + margem).
- Curva de raridade (`CardRarityWeight`): com ~3 picks, subir o peso de rare/echo no
  início da curva para o echo da Kaeli aparecer até a 2ª escolha (valores exatos na
  implementação, validados por distribuição amostrada no simulador/teste determinístico).
- Reroll (`CardRerollsPerRun`/`CardRerollGoldCost`) e auto-pick do helper inalterados.

Frontend: overlay de card não muda (só dispara menos). Verificar que a UI de baú não
espera oferta após abrir (remover expectativa se existir).

## Frente 2 — Power budget (baseline autossuficiente)

Princípio: **card-less limpa o andar**, ~25–35% mais lento que o alvo; com os ~3 picks
da run, volta ao alvo MG-08 (common ~3 · elite ~6 · boss ~12 ciclos de ação; mortes ~0
para mage/archer, boss nunca < 8 ciclos, sem one-shot).

Atenção: a frente 1 sozinha corta ~metade do poder de cards que o MG-08 assumia — sem
recalibrar, o jogo fica muito mais difícil. As duas frentes são calibradas juntas.

Método (mesma metodologia MG-08, `tools/BalanceSim`):

1. Estender o simulador com modo **card-less** e modo **"3 picks típicos"**
   (determinístico).
2. Sweep *before* (estado atual) → CSV em `docs/balance/`.
3. Recalibrar, nesta ordem de preferência: `MonsterStatLines` (coluna Health primeiro,
   Damage se preciso) e/ou `PlayerDamageMult`/`AtkPerRunLevel`, até os aceites acima.
4. Sweep *after* → CSV; cada número justificado pelo sweep.

Cards: valores das commons permanecem (a escassez de picks já limita o acúmulo);
rare/echo ganham peso de oferta por serem a decisão estratégica (ver frente 1).

## Frente 3 — Chaos ordenado (disciplina de AoE de mob)

Regras novas no cast de mob (`TryMonsterAttacks`), todas em `GameConfig`:

1. **Gate de intenção com margem** — `MonsterAoeProximityMargin = 2` (Chebyshev):
   - Nova/área self-centered (`Target=false`, `Radius>0`): só casta se
     `dist <= Radius + margem`.
   - Cone (`Length>0`): só casta se o player está num tile do cone **ou** a até
     `margem` tiles do tile de cone mais próximo.
   - Área mirada (`Target=true`): centrada no player, sempre legítima (sujeita ao cap).
   - Sem telegraph. O roll de `Chance` acontece **após** o gate (muda consumo de Rng —
     ver "Replay" abaixo).
2. **Cap de tamanho por rank**, aplicado no cast (cobre canary importados e authored):
   - Common/elite: `MonsterConeReachCap = 3` (hoje `Min(length,5)`),
     `MonsterAoeRadiusCap = 2`.
   - Boss: `BossConeReachCap = 5`, `BossAoeRadiusCap = 4` — a UE gigante vira
     assinatura de boss.
3. **Retune dos dados** para bater com os caps: `MonsterBehaviorProfiles` — artillery
   radius 3 → 2; breather cone 4 → 3.

Densidade (SpawnBudget) e agressividade de horda não mudam: o chaos continua, mas cada
explosão na tela é perigo real. Se o gate reduzir demais a pressão dos casters,
compensar em `Chance` dos ataques (calibração por feeling, fora do escopo do aceite).

## Efeitos colaterais e verificação

- **Replay FF-01:** o gate muda a ordem de consumo do Rng → golden replay quebra;
  rebaseline obrigatório via `--replay-check` na task da frente 3.
- **Determinismo:** todas as regras novas usam apenas estado do tick + `Rng` da run.
- **Build:** `dotnet build` (backend) e `npx ng build` (frontend) limpos.
- **Balance:** CSVs before/after no `docs/balance/` (frente 2).
- **Feeling:** run manual observando: nº de overlays por andar (~1–2), clear card-less
  possível, nenhum AoE de mob disparando longe do player, cones ≤ reach 3 em não-boss.

## Ordem de execução

1. Frente 1 (cadência) — independente.
2. Frente 3 (chaos) — independente (inclui rebaseline de replay).
3. Frente 2 (balance) — por último, calibrada com 1 e 3 já aplicadas.
