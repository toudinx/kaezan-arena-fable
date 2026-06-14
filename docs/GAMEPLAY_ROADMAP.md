# GAMEPLAY_ROADMAP — Kaezan Arena Fable

Fila de trabalho focada em **gameplay/feel** (hunts estilo Tibia 600+ no endgame + vida de
mapa). Complementa `ROADMAP.md`/`FABLE_TRACK.md`. Execute **uma task por sessão**, na **Ordem
de execução** (§0.7), salvo instrução contrária.

Prefixo `G-` (Gameplay) pra não colidir com os `T-NN`/`F-X` dos outros documentos.

---

## 0. Regras de trabalho (valem as do `ROADMAP.md` §0; resumo)

1. Leia `README.md` e `CLAUDE.md` primeiro; `docs/DESIGN_NOTES.md` dá o contexto Tibia/Canary.
2. **Invariantes**: backend autoritativo; determinismo (só `_rng` da run no `GameWorld`, nunca
   `Random`/`DateTime.Now`/iteração instável); **todas as constantes em `Domain/GameConfig.cs`**;
   IDs estáveis não mudam.
3. Verificação mínima por task: `dotnet build` + `npx ng build` verdes; para gameplay, suba os
   dois servidores e jogue uma run (tier baixo p/ regressão, tier de endgame p/ a feature nova).
4. Ao concluir: atualizar `README.md` se mudou comportamento visível; marcar `[x]` com 1 linha.
5. Se a task crescer além do descrito, **pare e registre** o que falta — não entregue pela metade.

Prioridades: **P0** fundação/feel · **P1** valor alto · **P2** quando sobrar. Esforço: S (<1
sessão) · M (1 sessão) · L (sessão cheia).

## 0.5 Modelo de owners (igual ao `ROADMAP.md` §0.5)

| Owner | Sweet spot |
|---|---|
| **Sonnet 4.6** | Mecânico, frontend/asset/UI, polish — sem lógica de simulação. |
| **Codex (GPT 5.5)** | Spec fechada, sistema isolado, instruções claras. Owner default. |
| **Opus 4.8** | Cross-cutting com julgamento de design/balance, escopo definível. |
| **Fable 5** | Determinismo-crítico, algoritmicamente difícil, alto raio de explosão (IA no tick, geração procedural com garantias, hardening). |

Etiqueta `Owner: Codex → Opus` = piso Codex, escale pra Opus se surgir decisão de design não
coberta. **Regra de ouro:** task que roda no `GameWorld` e afeta replay → **piso Opus, teto
Fable**; task puramente de apresentação → **piso Sonnet**.

## 0.7 Ordem de execução (SOBREPÕE a ordem temática das ondas)

As "Ondas" abaixo agrupam por tema; a ordem real respeita **dependências e quick-wins**:

1. **G-01** velocidade/input — quick win de feel (P0).
2. **G-12** andada em diagonal — completa o movimento (P0).
3. **G-02** suavidade visual — quick win frontend.
4. **G-08** índice de ocupação (perf) — **pré-requisito de G-10**; fazer cedo.
5. **G-03** obstáculos bloqueantes — **base de G-13** e dá vida/afeta pathing+fog.
6. **G-05** campos de hazard — **base de G-13** e cumpre o pedido poison/fire.
7. **G-04** fog of war — vida de mapa (**absorve T-22**).
8. **G-09** densidade por tier + salas arena/choke — prepara o endgame.
9. **G-10** respawn + ondas — coração da mobada (**depende de G-08**).
10. **G-13** ferramentas de kite — **depende de G-03 + G-05**; torna a mobada kitável.
11. **G-06** baús-armadilha com recompensa — valor, independente.
12. **G-07** vida ambiente — polish.
13. **G-11** polish de swarm — depois de G-10.

---

## Onda 1 — Feel de movimento (quick wins)

### [ ] G-01 — Velocidade & resposta de input
**P0 · S · backend (+frontend leve) · Owner: Codex → Opus**
_(Tuning bounded; escale a Opus se o feel exigir julgamento. Toca step timing no `GameWorld`.)_

**Contexto.** Em `PlayerBaseSpeed=250` o passo é `100000/250 = 400ms`/tile, bem acima do piso
`MinStepMs=160` ([GameConfig.cs:11-17](../backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs)).
Devagar pro ritmo de hunt. `StepGraceMs=80` ([GameWorld.cs:519](../backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs)) curto → virar/parar trava.

**Instruções.** 1. `PlayerBaseSpeed` 250→~320-360. 2. `StepGraceMs` 80→~130 (revisar buffer em
`SetMoveDirection`, GameWorld.cs:417). 3. Avaliar `MinStepMs` 160→~140. 4. Revisar `MonsterSpeedMultiplier=2`.
5. Alinhar `MOVE_HEARTBEAT_MS` ([game.ts:305](../frontend/src/app/pages/game/game.ts)).
**Aceite.** Andar/virar/parar responde melhor; player kita bem.
**Armadilhas.** Não cruzar o piso `MinStepMs`; só tuning de constantes (determinismo ok).

### [ ] G-12 — Andada em diagonal (8 direções)
**P0 · S · frontend (+verificar backend) · Owner: Codex → Opus**
_(Maioria frontend, mas a regra de não-cortar-quina toca `CanStep` no `GameWorld`.)_

**Contexto.** Backend **já suporta** diagonal (`DiagonalStepFactor=1.4`, `TryStep` desliza pro eixo,
GameWorld.cs:440-468); `sendMoveDir` já combina teclas (game.ts:350). Mas na prática só anda em N/S/L/O.

**Instruções.** 1. Input de diagonal confiável (numpad 1/3/7/9 e/ou combo de 2 teclas sem timing frágil).
2. Validar fim-a-fim (move → `SetMoveDirection` → `TickPlayerMovement`) + slide. 3. Regra de não cortar quina
(`CanStep` só checa destino, GameWorld.cs:450). 4. Facing/animação nas diagonais.
**Aceite.** Anda nas 8 direções fluida e confiável.
**Armadilhas.** Corner-cutting; não duplicar `DiagonalStepFactor`; determinismo.

### [ ] G-02 — Suavidade visual (render delay / clock smoothing)
**P1 · S · frontend · Owner: Sonnet → Codex**
_(Pura apresentação, sem simulação → piso Sonnet.)_

**Contexto.** A câmera já interpola (renderer.ts:129); o stutter é o relógio `serverNow`
(renderer.ts:102) saltando na chegada do snapshot, e `p` saturando em 1 (`actorRenderPos`,
renderer.ts:108) → "congela e pula".
**Instruções.** 1. Renderizar ~1 tick no passado (`serverNow - RenderDelayMs`). 2. Suavizar/clamp da deriva
do relógio. 3. Manter interpolação **linear**.
**Aceite.** Sem congela-e-pula; contínuo mesmo com jitter.
**Armadilhas.** RenderDelay grande = lag; backend não muda.

## Onda 2 — Dungeon viva (mapa, exploração, risco)

### [ ] G-03 — Obstáculos bloqueantes & dungeons habitadas
**P1 · M · backend (DungeonGenerator) + frontend · Owner: Opus → Fable**
_(Geração procedural com garantia de conectividade → determinismo-crítico.)_

**Contexto.** Decor só ~2.5% e **não-bloqueante** (DungeonGenerator.cs:44,148); `IsBlocked` só conhece parede/vazio.
**Instruções.** 1. Camada de obstáculo bloqueante em `DungeonFloor` (DungeonGenerator.cs:14-30); `IsBlocked` considera-a.
2. Espalhar props (taxa em GameConfig) **garantindo conectividade** e não selando entry/chest/ladder/spawns; usar `_rng`.
3. Mais decor nas salas + corredores. 4. Depth-sort no render (renderer.ts:201-246). 5. Assets via AssetExtractor se preciso.
**Aceite.** Obstáculos bloqueiam movimento+LoS; dungeon habitada; pathing desvia.
**Armadilhas.** `SpawnMonster` checa `IsBlocked` (GameWorld.cs:267); não bloquear caminho crítico; determinismo.

### [ ] G-05 — Campos de hazard no chão (poison/fire/energy)
**P1 · M · backend + frontend · Owner: Opus → Fable**
_(Fields tickam dano dentro do tick → determinismo-crítico.)_

**Contexto.** Condições já existem (`ApplyConditionToPlayer`, GameWorld.cs:1125; tipos GameConfig.cs:146),
mas **não há field de chão** e `TryStep` não tem hook pós-passo (GameWorld.cs:459).
**Instruções.** 1. Fields transientes por floor `{x,y,type,expiresAtMs,dmg,tickMs,owner}` indexados por `(x,y)`; usar `NowMs`.
2. Hook pós-passo (e parado) → `ApplyConditionToPlayer`, espelhando `TickPickup` (GameWorld.cs:1633).
3. Fontes: ambientais (DungeonGenerator), ataques/morte de mob, e (opcional) skill `field` do player. 4. Tick/expira/efeito
(reusar `ConditionTickFx`); fields do player danam mobs. 5. Render do field. 6. `fields[]` no snapshot.
**Aceite.** Pisar/ficar em poison/fire aplica condição e dana ao longo do tempo; fields visíveis e expiram.
**Armadilhas.** `ConditionResistCap`; re-aplicar só refresca; perf via dict.

### [ ] G-04 — Fog of war (mapa velado) — **absorve T-22**
**P1 · M · backend + frontend · Owner: Codex → Opus**
_(Estado de exploração no server, mas é visual/não afeta combate; escale se a transmissão exigir cuidado.)_

**Contexto.** Mapa todo visível; minimapa mostra tudo (`drawMinimap`, renderer.ts:341).
`HasLineOfSight` (Bresenham, GameWorld.cs:1765) reusável. O `ROADMAP.md` lista isso como **T-22** — esta task a substitui.
**Instruções.** 1. `bool[] Explored` por floor; const `VisionRange`. 2. Cada tick (pós-movimento) cast de
visibilidade (Chebyshev ≤ range + LoS) marcando revelados. 3. Transmitir **só índices novos** no snapshot (não
reenviar MapDto); resetar por floor/run. 4. Overlay escuro em não-explorado (opcional fog 2 níveis). 5. Minimapa só explorado.
**Aceite.** Mapa começa velado, revela ao andar com LoS; minimapa idem; sem reenvio do mapa inteiro.
**Armadilhas.** Custo do cast → `VisionRange` pequeno; resetar por floor/run.

### [ ] G-06 — Baús-armadilha: horda escalada + recompensa ao limpar
**P1 · M · backend · Owner: Opus**
_(Loot/ambush usam `_rng` e hooks de morte no tick → afeta replay.)_

**Contexto.** Emboscada (25%) spawna 3 commons fixos e dá **zero loot**; baú normal dá gold + 2 itens
(`TryInteract`, GameWorld.cs:1679-1731).
**Instruções.** 1. Escalar horda por tier (qtd + mix common/elite). 2. Marcar POI ambush + registrar mobs spawnados.
3. Ao limpar todos (hook no caminho de morte ~GameWorld.cs:993), conceder recompensa selada reusando loot do baú
(GameWorld.cs:1712-1730) + bônus; emitir "RECOMPENSA!". 4. Alternativa: dropar na hora E spawnar a horda.
**Aceite.** Emboscada recompensa ao limpar; determinístico; sem reward duplicada.
**Armadilhas.** Distinguir mobs do ambush de normais/summons; run terminar antes de limpar.

### [ ] G-07 — Vida ambiente do mapa
**P2 · M · frontend (+backend leve opcional) · Owner: Sonnet → Codex**
_(Decor/ambiente; piso Sonnet.)_

**Contexto.** Mapa parece morto fora de combate. Já há pulse de POI (renderer.ts:195) e idle anim (assets.service.ts:285).
**Instruções.** 1. Criaturas/decor ambiente vagando (preferir frontend; se reagir, espécie inofensiva só `Wander`).
2. Tiles ambientais animados + expandir pulse. 3. Variação no idle.
**Aceite.** Mapa com movimento ambiente; não parece morto.
**Armadilhas.** Não confundir com mobs de combate; perf de render.

## Onda 3 — Mobada de endgame (tier-scaled)

### [ ] G-08 — Perf: índice de ocupação por tile (pré-requisito de G-10)
**P0 · M · backend · Owner: Opus → Fable**
_(Refactor no hot path do tick com paridade exata → hardening de simulação.)_

**Contexto.** `OccupiedBy`/`MonsterAt` varrem `_monsters` linearmente (GameWorld.cs:411); 60+ mobs → O(n)/passo degrada o tick.
**Instruções.** 1. `Dictionary<(floor,x,y),Actor>` atualizado em `TryStep`/`SettleStep`/spawn/morte. 2. `OccupiedBy`/`MonsterAt` usam o índice.
**Aceite.** Tick <100ms com teto de mobs; comportamento idêntico.
**Armadilhas.** Sincronizar em **todos** os pontos de posição/morte/spawn; determinismo inalterado.

### [ ] G-09 — Densidade por tier + salas de endgame (arena/choke)
**P1 · M · backend (DungeonGenerator + GameConfig) · Owner: Opus → Fable**
_(Geração procedural com garantias + balance por tier.)_

**Contexto.** Budget cresce pouco (`SpawnBudgetTierGrowth=0.55`, GameConfig.cs:179); salas 5-9, floors 40/30 (GameConfig.cs:170).
**Instruções.** 1. Curva de budget agressiva no endgame; tier 1-2 ~igual. 2. Endgame: salas-arena maiores + mais salas com
**choke points** (porta 1 tile) → afunila pra box/AoE; tamanho/contagem por tier. 3. Manter roles.
**Aceite.** Tier baixo segue ralo; endgame com packs grandes e gargalos.
**Armadilhas.** Conectividade e spawn de boss; determinismo.

### [ ] G-10 — Respawn contínuo + ondas (coração da mobada)
**P0 · L · backend · Owner: Fable 5** — depende de **G-08**
_(IA/spawn dentro do tick, determinismo-crítico, alto raio de explosão.)_

**Contexto.** Spawn é único no init; sem respawn/ondas a hunt esvazia. Summons dão aggro imediato via
`AcquirePlayer` (GameWorld.cs:1484) — modelo reusável.
**Instruções.** 1. `TickSpawns()` após `TickMonsters()` (GameWorld.cs:391), ativo só com `Tier.Tier >= EndgameTierThreshold`.
2. Manter população-alvo perto do player; abaixo + `RespawnIntervalMs` → reforços fora da câmera (`PickReinforcementSpawn`)
com `AcquirePlayer`; reusar `SpawnMonster`+sorteio. 3. Ondas periódicas (`WaveIntervalMs`/`WaveSize`) das bordas.
4. Teto `MaxLiveMonstersEndgame` por tier. 5. `_rng`+`NowMs`.
**Consts:** `EndgameTierThreshold`, `EndgameTargetLivePopulation(+PerTier)`, `RespawnIntervalMs`, `WaveIntervalMs`, `WaveSize`, `MaxLiveMonstersEndgame`, `ReinforcementSpawnMinDistance`.
**Aceite.** Endgame mantém pressão (respawn+ondas) sem estourar teto; tier baixo intacto.
**Armadilhas.** Sem G-08 o tick degrada; não spawnar em cima do player/parede; determinismo.

### [ ] G-13 — Ferramentas de kite (magic wall, bomba no pé) + target/aggro pró-kite
**P1 · M · backend (+frontend) · Owner: Opus → Fable** — depende de **G-03 + G-05**
_(Novas mecânicas de combate no tick com julgamento de design.)_

**Contexto.** Faltam ferramentas de kite (magic wall, bombas no pé); mob "cola" e o player sente que perde o
target ao kitar. Aggro cai em 4s fora de alcance/6s sem LoS (GameWorld.cs:1222-1242; `DropAggro` 1291); target do
player só some quando o alvo morre/troca floor (GameWorld.cs:605/1741).
**Instruções.** 1. **Magic wall:** tile bloqueante temporário (reusar G-03 + expiração `NowMs`); bloqueia mob+LoS.
2. **Bomba no pé:** field de dano no tile do player (reusar G-05). 3. **Anti-cola:** mobs não empilham e mantêm
espaçamento (chase já pontua crowding ~GameWorld.cs:1568). 4. **Target/aggro:** manter `TargetId` enquanto o alvo
existir + feedback ao sair de LoS; tunar `AggroDrop*`; auto-retarget opcional.
**Aceite.** Dá pra kitar a mobada com paredes/bombas; target/aggro cooperam.
**Armadilhas.** Paredes não prendem o player nem selam salas; fields/walls expiram; determinismo.

### [ ] G-11 — Polish de swarm (números/efeitos)
**P2 · S · frontend · Owner: Sonnet → Codex**
_(Pura apresentação.)_

**Contexto.** AoE acerta 15-25 mobs → 15-25 eventos `damage`; sem agrupamento vira sopa (renderer.ts:54-98).
**Instruções.** Clusterizar números por posição; pooling de efeitos no mesmo tile; cuidar do volume de eventos.
**Aceite.** Combate contra muitos mobs legível e satisfatório.
**Armadilhas.** Não esconder dano relevante; perf de render.

---

## Tabela-mestra de atribuição

| Task | P | Esf | Área | Owner | Reasoning |
|---|---|---|---|---|---|
| G-01 Velocidade/input | P0 | S | back+front | **Codex → Opus** | Tuning bounded; toca step timing (determinismo) → teto Opus. |
| G-12 Diagonal | P0 | S | front+back | **Codex → Opus** | Input no front; corner-cutting toca `CanStep` no GameWorld. |
| G-02 Suavidade visual | P1 | S | front | **Sonnet → Codex** | Só render; sem simulação. |
| G-03 Obstáculos | P1 | M | back+front | **Opus → Fable** | Geração procedural com garantia de conectividade. |
| G-05 Hazard fields | P1 | M | back+front | **Opus → Fable** | Fields tickam dano no tick → determinismo. |
| G-04 Fog of war (T-22) | P1 | M | back+front | **Codex → Opus** | Estado de exploração no server, mas visual. |
| G-06 Baús-armadilha | P1 | M | back | **Opus** | Loot/ambush via `_rng` + hooks de morte → replay. |
| G-07 Vida ambiente | P2 | M | front | **Sonnet → Codex** | Decor/ambiente. |
| G-08 Índice de ocupação | P0 | M | back | **Opus → Fable** | Refactor de hot path com paridade exata. |
| G-09 Densidade/salas tier | P1 | M | back | **Opus → Fable** | Geração procedural + balance por tier. |
| G-10 Respawn + ondas | P0 | L | back | **Fable 5** | IA/spawn no tick, determinismo-crítico, alto raio. |
| G-13 Kite (wall/bomba) | P1 | M | back+front | **Opus → Fable** | Novas mecânicas de combate no tick. |
| G-11 Polish de swarm | P2 | S | front | **Sonnet → Codex** | Só render. |

---

## Verificação (por onda)

- **Build:** `dotnet build` + `npx ng build` limpos antes de fechar cada task.
- **Determinismo:** run com seed fixa 2x → mesmo padrão de spawn/onda/ambush/fog.
- **Densidade por tier:** tier baixo (continua ralo) e endgame (mobada+respawn+ondas) via preview.
- **Perf:** com `MaxLiveMonstersEndgame`, medir `Tick()` < `TickMs` (100ms).
- **Fog/hazard/box/kite:** validar revelação progressiva, status ao pisar em field, gargalos pra box, e magic wall/bombas no kite — via `preview_*`.
- **README:** atualizar quando o comportamento visível mudar.
