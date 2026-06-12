# ROADMAP — Kaezan Arena Fable

> **Para o agente (Codex/Claude) que vai executar:** este documento é a fila de trabalho do
> projeto. Cada task é auto-contida: contexto, arquivos, instruções, critérios de aceite e
> armadilhas. Execute **uma task por sessão**, na ordem de prioridade, salvo instrução contrária.

---

## 0. Regras de trabalho (leia antes de qualquer task)

1. Leia `README.md` e `CLAUDE.md` primeiro. `docs/GDD.md` dá o contexto de design.
2. **Invariantes**: backend autoritativo; determinismo do engine (só `Rng` da run dentro do
   `GameWorld`, nunca `Random`/`DateTime.Now`); todas as constantes em `Domain/GameConfig.cs`;
   IDs estáveis (`waifu:*`, `card:*`, `banner:*`, nomes de espécie) não mudam.
3. O repo `C:\Kaezan\kaezan` (Canary/OTClient + docs em `mapping/`) é **somente leitura** —
   fonte de assets e referência técnica.
4. Verificação mínima ao final de toda task: `dotnet build` (backend) e `npx ng build`
   (frontend) verdes. Para tasks de gameplay, rode os dois servidores
   (backend `dotnet run --urls http://localhost:5210`; frontend `npx ng serve`) e jogue uma run
   no tier 1. Para verificar o canvas em ambiente headless, use o hook `window.__kaezanRenderer`
   (rAF não roda em aba invisível): `__kaezanRenderer.draw(performance.now())` e capture via
   `canvas.toDataURL(...)`.
5. Ao concluir: atualize `README.md` se o comportamento visível mudou, e marque a task aqui
   como `[x]` com uma linha de resumo.
6. Se uma task se revelar maior do que o descrito, **pare e registre** o que falta na própria
   task em vez de entregar pela metade.

Prioridades: **P0** = fundação/feel, **P1** = valor alto, **P2** = quando sobrar tempo.
Esforço: S (< 1 sessão), M (1 sessão), L (1 sessão cheia, escopo apertado).

---

## Fase 1 — Fluidez de gameplay e movimento (P0)

A maior alavanca de qualidade percebida. O jogo funciona, mas o "game feel" tem atritos
conhecidos, listados task a task.

### [ ] T-01 — Movimento fluido: buffer de passo no servidor + tuning de cadência
**P0 · M · backend (+frontend leve)**

**Contexto.** Hoje `GameWorld.TickPlayerMovement()` (em
`backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs`) só inicia um passo novo se o anterior
já terminou **e** o tick coincidir. Com tick de 100ms e passos de ~400ms, o player perde até
100ms entre passos (sensação de "engasgo"), e mudanças de direção no meio do passo são ignoradas
até o próximo tick. O frontend interpola em `core/renderer.ts` (`actorRenderPos`).

**Instruções.**
1. No servidor, ao terminar um passo com `_moveDirX/_moveDirY` ainda pressionados, inicie o
   próximo passo **no mesmo tick**, com `StepStartMs` igual ao fim exato do passo anterior
   (não ao `NowMs` do tick) — elimina o gap de até 100ms. Cuidado para limitar a 1 passo extra
   por tick (não fazer loop de catch-up).
2. Adicione em `GameConfig` um `StepGraceMs` (~80ms): se o comando de direção chegar até X ms
   antes do fim do passo, o próximo passo já usa a nova direção (input buffer).
3. No frontend (`pages/game/game.ts`), envie `Move` também num `setInterval` leve (ex.: 250ms)
   enquanto houver tecla segurada — protege contra perda de pacote/foco e garante que o servidor
   sempre conheça a direção atual. Não envie quando a direção for (0,0) repetida.
4. Tuning em `GameConfig`: avalie `MinStepMs` 180→160 e `DiagonalStepFactor` 1.5→1.4. Jogue e
   ajuste pelo feel; anote os valores finais no commit.

**Aceite.** Segurar uma direção atravessa um corredor longo sem micro-pausas visíveis; trocar
de direção em movimento responde no passo seguinte; `dotnet build` verde; run jogada de ponta
a ponta sem regressão.

**Armadilhas.** Não introduza client-side prediction (fora de escopo; quebra o modelo
autoritativo). `StepStartMs` no passado é esperado pelo renderer (interp clampa em 0..1).

### [ ] T-02 — IA de monstros: anti-empilhamento, perda de aggro e desvio
**P0 · M · backend**

**Contexto.** `TickMonsters()`/`StepToward()` usam passo greedy (sinal de dx/dy). Resultado
visível: mobs formam fila indiana em corredores e travam uns nos outros; aggro nunca é perdido
(perseguem para sempre, mesmo sem ver o player).

**Instruções.**
1. **Desvio lateral:** quando o passo greedy e os dois eixos falham, tente os 2 passos
   perpendiculares à direção desejada (ordem decidida por `_rng` para não sincronizar mobs).
2. **Separação:** ao escolher o passo, evite destino adjacente a 2+ mobs vivos quando houver
   alternativa de mesma distância (cheap: contar vizinhos ocupados antes de mover).
3. **Perda de aggro:** se `Chebyshev > 12` por mais de 4s contínuos **ou** sem LOS por mais de
   6s, zere `TargetId` e volte a `Wander`. Guarde `LastSawPlayerAtMs` no `Actor`.
   Constantes novas em `GameConfig` (`AggroDropRange`, `AggroDropNoLosMs` etc.).
4. **staticAttackChance:** o campo já vem do Canary (`Species.StaticAttackChance`, 0-100).
   Use-o: a cada decisão de movimento com alvo em range de ataque, role `_rng` — se passar,
   o mob fica parado atacando em vez de colar no player (dá identidade: atiradores como
   Minotaur Archer mantêm distância de verdade).

**Aceite.** Numa sala com 6+ mobs, eles cercam o player em arco em vez de fila; fugir de uma
sala e quebrar LOS por ~6s faz os mobs desistirem; archers param para atirar. Determinismo
preservado (só `_rng`).

### [ ] T-03 — Escolha de card sem morrer: pausa tática
**P0 · S · backend**

**Contexto.** A oferta de cards (`_pendingOffer`) não pausa nada — o player apanha enquanto lê.
Decisão original era "action roguelike", mas em tier alto é punitivo demais.

**Instruções.** Enquanto `_pendingOffer != null`: monstros não se movem nem atacam, projéteis/
DoTs não tickam, e o player não se move nem ataca (congele cedo no `Tick()`, mas continue
processando comandos `ChooseCard`). Adicione um cap: se o player ignorar a oferta por 20s
(`GameConfig.CardOfferTimeoutMs`), escolha automaticamente a primeira opção e despause —
evita exploit de pausa infinita e softlock.

**Aceite.** Ao subir de nível no meio de uma horda, o mundo congela visivelmente até a escolha;
após escolher, tudo retoma; timeout de 20s auto-escolhe.

**Armadilhas.** Não usar tempo real para o timeout — contar ticks (`TickCount`), senão quebra
determinismo de replay futuro.

### [ ] T-04 — Reconexão de run (refresh não mata a run)
**P1 · M · backend + frontend**

**Contexto.** `RunManager.DropRun` trata desconexão como abandono imediato. Um F5 acidental
destrói a run.

**Instruções.**
1. Em `DropRun`, em vez de encerrar, mova a run para um dicionário `_orphans` com timestamp;
   pause o mundo dela (não tickar). Após 60s sem reclamação, aí sim aplique o abandono atual.
2. `GameHub.JoinRun`: aceite um parâmetro opcional `resume: bool` — se houver run órfã (há só
   uma conta), reassocie ao novo `ConnectionId`, reenvie o `map` e retome o tick.
3. Frontend: `GamePage` ao iniciar tenta `JoinRun(tier, resume: true)`; o backend responde se
   retomou ou criou run nova; mostre toast "Run retomada".

**Aceite.** F5 no meio da run volta para a mesma dungeon (mesmo mapa, mesmos mobs, mesmo HP)
em até 60s; após 60s, abandono normal com metade do ouro.

---

## Fase 2 — Conteúdo em escala (P0/P1)

O pipeline existe; estas tasks são principalmente **curadoria + re-rodar tools**, com pouco
código. São as de melhor custo-benefício do roadmap.

### [ ] T-10 — +30 monstros do Tibia (preencher os 5 tiers)
**P0 · M · tools + backend (dados)**

**Contexto.** `tools/convert-monsters/config.json` lista 29 espécies. Os tiers em
`GameConfig.Tiers` reusam poucas espécies. Fonte: `C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\monster\`
(use `find` por nome de arquivo).

**Instruções.**
1. Adicione ao config (verifique existência do arquivo antes; nomes em snake_case):
   - Tier 1 (caverna): `bug`, `wolf`, `winter_wolf` (mammals), `troll`, `troll_champion` (humanoids), `scorpion` (vermins), `slime` (slimes), `centipede` (vermins)
   - Tier 2 (forte orc): `orc_shaman`, `orc_rider`, `goblin`, `goblin_scavenger` (humanoids), `war_wolf` (mammals), `dwarf`, `dwarf_soldier` (humanoids)
   - Tier 3 (cripta): `mummy`, `crypt_shambler`, `bonelord` (não-undead? verifique pasta), `ghost`, `banshee` (undeads), `witch` (humans)
   - Tier 4 (covil): `fire_elemental`, `earth_elemental` (elementals), `dragon_hatchling`, `dragon_lord_hatchling`, `frost_dragon_hatchling` (dragons), `minotaur_guard` (humanoids)
   - Tier 5 (abismo): `hellhound`, `hellfire_fighter` (demons), `frost_dragon` (dragons), `hydra` (reptiles), `dark_torturer`, `juggernaut` (demons)
2. `node convert.mjs`; revise o output: espécies com 0 ataques ou lookType 0 devem ser
   removidas (alguns usam `lookTypeEx`/item — não suportado).
3. Re-rode o AssetExtractor (comando no README) para puxar os novos outfits/loot/corpses.
4. Atualize `GameConfig.Tiers` distribuindo as espécies novas em `CommonMobs`/`EliteMobs`
   por dificuldade (use `health`/`experience` como guia: commons < ~300 hp no tier respectivo).
5. Jogue 1 run em cada tier desbloqueável e confirme que tudo renderiza (sem sprites invisíveis).

**Aceite.** ≥55 espécies em `monsters.json`; cada tier com ≥5 commons e ≥3 elites distintos;
manifest atualizado; runs nos tiers 1-2 verificadas em jogo.

**Armadilhas.** Monstros com `summon`/invisibilidade/healing funcionam mas ignoram essas
mecânicas (ok por ora). Bosses de raid têm HP estranho — confira `BossHpScale` se promover
algum a boss de tier.

### [ ] T-11 — Preços reais de itens (npcsaledata) + items.json
**P0 · M · tools + backend + frontend**

**Contexto.** `MetaEndpoints.ItemValue` é um placeholder (`15 + itemId % 35`). O protobuf de
appearances tem `AppearanceFlagNPC npcsaledata` com `sale_price`/`buy_price` por item
(campo 40 de `AppearanceFlags` — já compilado no AssetExtractor).

**Instruções.**
1. No `AssetExtractor`, ao exportar objects, colete `flags.npcsaledata`: gere
   `backend/src/KaezanArenaFable.Api/Data/items.json` com `{ itemId, name, salePrice }`
   (maior `sale_price` entre os NPCs; name do appearance ou do loot). Inclua **todos** os ids
   presentes em monsters.json loot + os semantic.
2. Backend: carregue em `GameData` (`Items`); `ItemValue` passa a usar `salePrice`
   (fallback 5 se ausente). Exponha `salePrice` no `/catalog` para a Mochila.
3. Frontend Mochila: mostre o valor de venda no card do item ("Vender 1 (+45 🪙)").

**Aceite.** Vender um dragon ham paga o preço real do Tibia; itens sem comprador pagam 5;
`items.json` commitado; nenhum `itemId % 35` restante no código.

### [ ] T-12 — Biomas visuais por tier + cantos de parede
**P1 · L · tools + backend + frontend leve**

**Contexto.** Todos os tiers usam o tileset de caverna (`DungeonGenerator` hardcoda
`CaveGround`/`Wall*`). Tiles de stone wall (1112-1122) e lava (727-730) já estão extraídos.
As paredes usam só 3 variantes (pole/horizontal/vertical) — sem cantos, o contorno fica pobre.

**Instruções.**
1. Crie em `GameConfig` (ou arquivo novo `Domain/Biomes.cs`) um `BiomeDef` por tier:
   ground[], wallPole/H/V + corners, decor[], accent[] (ex.: lava no tier 4/5). Curadoria de ids:
   grep em `canary-3.4.1/data/items/items.xml` por nome (`stone wall`, `lava`, `grave`, `bone`,
   `mountain`); adicione os ids novos ao `content-config.json` e re-extraia. **Valide
   visualmente**: extraia, monte uma página de debug simples ou capture o canvas via hook.
2. `DungeonGenerator.Generate` recebe o `BiomeDef` (do tier) em vez das constantes atuais.
3. Cantos: na `PaintTiles`, classifique a célula de parede por vizinhança em 8 direções
   (N/S/E/W abertos) e escolha entre H, V, canto-NE/NW/SE/SW e pole. A família dirt wall
   356-367 tem as variantes — descubra o mapeamento renderizando as 12 lado a lado (hook do
   renderer ou contact sheet) e documente no código qual id é qual.
4. Decoração temática: cripta = ossos/lápides; covil = pedras + poças de lava decorativas
   (accent em células de sala, nunca bloqueando o caminho entre entrada↔escada↔boss).

**Aceite.** Tier 1 caverna, tier 3 cripta de pedra, tier 5 com lava; cantos de parede corretos
(sem "dentes" visuais nas quinas das salas); caminho sempre transitável.

### [ ] T-13 — 6 novas waifus (expansão do elenco gacha)
**P1 · M · backend (dados) + frontend leve**

**Contexto.** 13 waifus em `Domain/Waifus.cs`. Outfits femininos já extraídos e ainda não
usados: 140 (noblewoman), 150 (oriental), 157 (beggar), 158 (shaman), 270 (jester),
279 (brotherhood), 288 (demonhunter).

**Instruções.**
1. Crie 6 waifus (2× 3★, 3× 4★, 1× 5★) usando esses lookTypes, com identidade de elemento/arma
   que preencha lacunas do elenco (hoje falta: 5★ melee, 4★ earth, ice wand…). Skills: monte
   kits com os shapes existentes (`single|beam|nova|area|cone|buff`) e FX já extraídos —
   só adicione `effectIds`/`missileIds` novos ao content-config se necessário (e re-extraia).
2. Nomes/títulos/descrições em PT-BR no tom do elenco atual (1 linha de lore com personalidade).
3. Cores de outfit (head/body/legs/feet 0-132): escolha paletas distintas; valide no preview
   da página Kaelis.
4. A nova 5★ entra no pool do banner padrão; avalie criar um segundo banner promocional
   rotativo em `GachaService.Banners` (id estável `banner:<tema>`).

**Aceite.** 19 waifus no catálogo; todas renderizam no Kaelis/preview com addons; pull do
banner pode tirar as novas; nenhuma skill órfã (todo kit referencia skills existentes).

### [ ] T-14 — Condições do Tibia: poison/burn como DoT
**P1 · M · tools + backend + frontend leve**

**Contexto.** O converter (`tools/convert-monsters/convert.mjs`) descarta o campo `condition`
dos ataques (ex.: snake/poison spider aplicam veneno no Tibia). O engine não tem DoT.

**Instruções.**
1. Converter: quando o ataque tiver `condition = { type = "CONDITION_POISON"|..., totalDamage/
   interval/...}`, emita `condition: { type: "poison"|"fire"|"energy", totalDamage, tickMs }`
   (mapeie os nomes; valores ausentes → defaults: 10 ticks de 2s).
2. Engine: lista de DoTs ativos no player (`type, damagePerTick, ticksLeft, nextTickAtMs`);
   aplicada quando o ataque acerta; tick dentro do `Tick()` com dano via `DamagePlayer`
   (sem refresh stack — reaplicar substitui se maior). FX por tick: 17 (poison) / 16 (fire) /
   12 (energy) no tile do player, e cor do número de dano correspondente.
3. HUD: chip de condição ativa ao lado dos buffs (`PSN`/`BRN` com cor).
4. Card novo `card:antidote` ("Resistência: -50% dano de condições") entra no pool.

**Aceite.** Levar hit de Poison Spider aplica veneno visível (chip + ticks verdes);
o card reduz; determinismo preservado.

---

## Fase 3 — UI/UX e juice (P1)

### [ ] T-20 — Juice de combate
**P1 · M · frontend (renderer)**

**Contexto.** `core/renderer.ts`. Hoje: número de dano + efeito de sprite. Falta impacto.

**Instruções.**
1. **Hit flash:** ao ingerir evento `damage` de um monstro, pinte o sprite dele de branco por
   ~90ms (composite `source-atop` num offscreen por criatura, ou `filter: brightness` via
   `ctx.filter` — medir custo; cache por espécie se necessário).
2. **Screen shake:** 4-6px decaindo em 250ms quando o player toma hit ≥8% do MaxHp e no cast
   de ultimate (offset aleatório do camera antes do clamp).
3. **Números de dano:** crit maior (16px) com pop (escala 1.4→1.0 nos primeiros 120ms);
   dano do player no monstro em branco/amarelo, dano recebido em vermelho — já há cores, falta
   o pop e empilhamento (offsets alternados quando vários números no mesmo tile).
4. **Kill feedback:** no death event, fade-out rápido do sprite do monstro (já existe corpse;
   adicione 150ms de "ghost" branco esvaecendo).

**Aceite.** Capturas antes/depois via hook mostram flash/shake/pop; FPS estável (~60) com 10+
mobs na tela (medir com `performance.now()` no loop e logar média no console em dev).

### [ ] T-21 — HUD informativo: tooltips, alvo e bússola
**P1 · M · frontend**

**Instruções.**
1. **Tooltip de skill:** hover/long-press num slot mostra nome, descrição (`catalog.skills`),
   custo/cooldown — hoje o jogador não tem como ler o que a skill faz dentro da run.
2. **Frame do alvo:** painel compacto no topo (nome, HP numérico, elemento fraco/resistente —
   derivável de `catalog.monsters[species].loot`? Não: exponha `elements` do monstro no
   `/catalog`) quando houver `targetId`.
3. **Bússola de objetivo:** seta discreta na borda da tela apontando para a escada (andar 1)
   ou para o boss (andar 2), usando `map.ladderX/Y` e a posição do boss no snapshot —
   dungeons procedurais confundem; isso elimina o "andar perdido".
4. **Indicador de range:** com alvo travado, mostre se está em range de auto-attack
   (borda do frame do alvo verde/cinza).

**Aceite.** As 4 features visíveis e correta; nenhuma sobreposição com HUD existente em 1280×720.

### [ ] T-22 — Minimapa com fog of war
**P2 · S · frontend**

**Instruções.** `drawMinimap` hoje revela o mapa inteiro. Mantenha um `Set` de tiles vistos
(raio 9 do player, atualizado por snapshot); desenhe não-visitados em preto, visitados em
marrom, e POIs só depois de vistos. Reset por andar.

**Aceite.** Minimapa começa quase preto e revela conforme exploração; escada aparece só
depois de vista.

### [ ] T-23 — Cerimônia do gacha
**P1 · M · frontend**

**Contexto.** `pages/recruit/recruit.ts` tem reveal por cards com cor de raridade — funcional,
sem clímax.

**Instruções.**
1. **Tease de raridade:** antes do flip, brilho na borda na cor do melhor item do pull
   (dourado se houver 5★, roxo se 4★) — padrão do gênero.
2. **5★ especial:** ao revelar uma 5★, overlay fullscreen rápido (1.2s, skippable por clique):
   fundo radial dourado + outfit preview grande animado + nome/título.
3. **Histórico de pulls:** registre cada pull no backend (lista no `AccountState`, máx. 200)
   e mostre num modal "Histórico" na página (data, banner, resultado, raridade) — transparência
   de pity que jogador de gacha espera.
4. Som fica para T-34; não bloqueie nisso.

**Aceite.** 10-pull com 5★ mostra o tease dourado + cutscene skippable; histórico lista os
pulls com raridade colorida; pity exibido continua correto.

### [ ] T-24 — Polish geral de meta
**P2 · S · frontend**

**Instruções.** (a) Título da aba "Kaezan Arena Fable" + favicon (gere um PNG 32px do sprite
da Mirai com o próprio AssetExtractor ou recorte manual); (b) confirmação ao vender pilha
inteira (>5 itens); (c) botão "Vender duplicatas baratas" na Mochila (vende tudo com
salePrice < 20, depende de T-11); (d) skeleton/spinner de loading nas páginas meta em vez de
"Carregando..."; (e) estados de erro do fetch com retry (hoje só `alert`).

---

## Fase 4 — Features de sistema (P1/P2)

### [ ] T-30 — Sealed Reward: baú selado pós-boss com reroll
**P1 · L · backend + frontend**

**Contexto.** Inspiração: `kaezan/mapping/changes/features/sealed_reward/` e
`canary/client/features/sealed_reward.md` (somente leitura). Conceito: vitória → baú com
N slots revelados um a um; 1 reroll grátis em um slot.

**Instruções.**
1. Backend: ao matar o boss, gere (com o `_rng` da run — determinístico) 4 recompensas:
   1 garantida (Kaeros), 1 item raro do loot pool do tier (chance < 2000 no canary = raro),
   1 material/ouro, 1 slot "premium" (pequena chance de Echo Shards de waifu aleatória).
   Inclua no `RunEndDto` como `sealedReward: [{kind, itemId?, amount, revealed}]` + flag
   `rerollAvailable`.
2. Novo comando hub `RerollSealedSlot(index)` válido apenas com run terminada vitoriosa e
   reroll disponível; re-rola aquele slot (mesma tabela) e aplica a diferença na conta via
   `RewardService`.
3. Frontend: na tela de vitória, os slots aparecem virados; clique revela com flip + FX 184
   (pixie explosion) por sprite; botão de reroll por slot (1 uso).

**Aceite.** Vitória mostra o ritual de revelação; reroll funciona 1x e persiste o resultado
final na conta; derrota não gera sealed reward.

### [ ] T-31 — Boss Posture (Echo Break)
**P1 · M · backend + frontend**

**Contexto.** Inspiração: `kaezan/mapping/changes/features/boss_posture/`. Barra secundária
do boss que enche com hits; cheia = boss quebrado (stun longo + dano amplificado).

**Instruções.**
1. Backend: `PostureMax` por boss (escala com tier; constante base em `GameConfig`). Cada hit
   do player adiciona postura (skills > auto-attack; valores em `GameConfig`). Postura decai
   ~2/s quando sem hits por 3s. Ao encher: stun 4s, +50% dano recebido durante o stun, FX 35 +
   texto "QUEBRADO!", barra reseta.
2. Snapshot: `bossPosture/bossPostureMax` no `RunStateDto`.
3. Frontend: barra fina dourada sob a HP bar do boss; pisca quando >80%.

**Aceite.** Lutar contra a Rotworm Queen permite ~1-2 quebras por luta; quebra é visível e
recompensadora; decaimento impede encher "de graça".

### [ ] T-32 — Imbuements lite (sockets elementais com loot)
**P2 · L · backend + frontend**

**Contexto.** Inspiração: `kaezan/mapping/canary/systems/economy.md` (imbuements). Dá uso ao
loot além de vender.

**Instruções.** 1 slot de imbuement por waifu (desbloqueia em A1). Receitas fixas em
`GameConfig`: ex. "Presas venenosas" = 20× snake skin? → use itens que realmente dropam
(verifique `monsters.json`); efeito = +10% dano do elemento X ou +5% lifesteal, por **conta**
(persistente). Endpoints REST `POST /api/v1/waifus/imbue`; UI na página Kaelis (aba do kit);
consumo de itens do inventário com confirmação.

**Aceite.** Aplicar um imbuement consome itens, persiste, afeta o dano em run (verificável
nos números), e aparece no detalhe da waifu.

### [ ] T-33 — Replay determinístico
**P2 · M · backend**

**Contexto.** O engine já é determinístico por seed+comandos; falta gravar/reproduzir
(kaezan-arena tinha; ver `InMemoryBattleStore.Replay.cs` lá como referência de shape).

**Instruções.** Grave `(tick, Command)` de cada run em memória; ao fim, persista JSON em
`.data/replays/` (`seed`, `tier`, `waifu`, `ascension`, `commands[]`, hash do estado final).
Endpoint dev `POST /api/v1/dev/replay` roda a run inteira headless re-aplicando os comandos
nos mesmos ticks e responde o hash final — base do teste T-40. Sem UI.

**Aceite.** Replay de uma run real reproduz hash idêntico; alterar 1 comando muda o hash.

### [ ] T-34 — Áudio
**P2 · L · frontend (+tools)**

**Contexto.** Zero áudio hoje. O Canary tem ids de som por monstro mas os arquivos de áudio
do cliente não estão disponíveis no formato extraível — **não tente extrair**; use SFX
livres (ex.: Kenney.nl, CC0) baixados para `frontend/public/assets/sfx/`.

**Instruções.** Serviço `AudioService` (Web Audio, pool, volume em localStorage, mute por
padrão até 1ª interação): hit melee, projétil, level-up, abrir baú, morte de mob, ultimate,
reveal de gacha (comum/4★/5★), vitória/derrota. Gatilhos pelos eventos já existentes no
renderer/ingest. Slider de volume no overlay de pausa/config (criar mini-modal de config
no shell).

**Aceite.** Sons nos 9 gatilhos; mute/volume persistem; sem erro de autoplay no primeiro load.

---

## Fase 5 — Qualidade, bugs e dívidas (P1)

### [ ] T-40 — Testes de determinismo e de regras (xunit)
**P1 · M · backend**

**Instruções.** Projeto `backend/tests/KaezanArenaFable.Tests` (xunit):
1. **Determinismo:** mesma seed + mesma sequência de comandos sintéticos (500 ticks de
   movimento + casts) ⇒ snapshots finais idênticos (serialize e compare). Rode 2× no mesmo
   processo e 1× com ordem de dicionários embaralhada se possível.
2. **Geração:** para 100 seeds, o dungeon é conexo (BFS da entrada alcança escada/boss),
   tem ≥1 baú e o boss existe no andar 2.
3. **Gacha:** com `Random` seedado injetável (refatore `GachaService` para aceitar um
   `Func<double>`), valide: hard pity 80 nunca estoura, 4★ a cada 10 sempre, 50/50 + garantia.
4. Adicione `dotnet test` ao fluxo de verificação no CLAUDE.md.

**Aceite.** `dotnet test` verde; teste de determinismo falha se alguém introduzir `Random`
no engine (prove removendo temporariamente o guard e revertendo).

### [ ] T-41 — Robustez de assets: fallback de sprite + diagnóstico
**P1 · S · frontend + tools**

**Contexto.** Sprite não extraído = invisível silencioso (ex.: futura espécie com lookType
fora do manifest).

**Instruções.** (1) `AssetsService`: quando `entry()` falhar, desenhe um placeholder
(quadrado magenta 50% alpha com "?") e registre `console.warn` com dedupe (1× por id).
(2) Tool nova `tools/check-assets.mjs`: cruza monsters.json (lookTypes, corpses, loot ids) e
Waifus/skills (effect/missile ids — exporte-os num JSON pequeno do backend ou hardcode a
extração via regex de `Waifus.cs`) contra o manifest, e lista o que falta. Rode no aceite
de toda task de conteúdo (referencie em T-10/T-12/T-13).

**Aceite.** Id inexistente mostra placeholder visível; `node check-assets.mjs` sai com
exit 1 e lista clara quando falta asset.

### [ ] T-42 — Limpeza de dívidas pontuais
**P2 · S · backend + frontend**

Checklist objetiva (tudo pequeno):
- [ ] `GameWorld.KillMonster`: guard de double-kill é código morto confuso — substitua por
  early-return `if (monster.Hp <= 0 && !overkill) return;` **antes** de processar, e garanta
  que `DealDamageToMonster` não chame `KillMonster` duas vezes no mesmo tick.
- [ ] `KaelisPage`: troque o `setInterval` de inicialização por `effect()` sobre os signals
  de `ApiService`.
- [ ] `ItemIcon`: troque o retry de 10× `setTimeout` por `await assets.image(...)` direto
  (a API já existe) e um único redraw.
- [ ] `GamePage.onKeyDown`: `Q/E/R` como alias já ok; adicione `preventDefault` nos números
  para não rolar a página em layouts pequenos.
- [ ] Facing diagonal: `FacingFrom` prefere E/W em diagonal; mantenha o último eixo dominante
  do passo anterior quando |dx|==|dy| (campo `LastFacingAxis` no Actor) para o sprite não
  "tremular" andando em diagonal.
- [ ] `MetaEndpoints`: padronize erros como `{ error }` com status 400 em todos os handlers
  (já é o padrão; revise `items/sell` e `active-waifu`).
- [ ] Remova `window.__kaezanRenderer`? **Não** — mantenha; é o hook de verificação headless.
  Documente-o com comentário apontando para o ROADMAP §0.4.

---

## Backlog (sem spec ainda — promover a task quando chegar a vez)

- Montarias como drop raro de boss (extractor já lê patternZ de mount nos outfits).
- Segundo modo de jogo: "Echo Spot" rápido de 1 sala com waves (budget já existe) para daily.
- Eventos de mundo / elemento do dia (kaezan-arena tinha; trazer com FX do Tibia).
- Expedições idle de waifus fora da equipe (pet expedition).
- Wheel-like de progressão por waifu (mastery) — ver `kaezan/mapping/changes/features/kaeli_mastery/`.
- Acessibilidade: remapeamento de teclas, daltônico (cores de dano).
- i18n EN (strings hoje em PT-BR espalhadas nos templates).
- Multiplayer co-op (grande; exigiria revisitar snapshot por conexão e occupancy).
