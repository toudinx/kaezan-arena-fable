# Roadmap — Lugares & Modos (estudo do Tibia + arena de sobrevivência)

> **Como usar este arquivo.** Cada `LM-NN` abaixo é uma unidade de trabalho **auto-contida**: o
> agente que executa começa "frio", então o prompt já traz o contexto que precisa. Você dispara com
> **"implemente o prompt LM-NN do `docs/roadmap_dungeons.md`"** e o agente faz o resto.
>
> Cada prompt declara: **Modelo · Effort · Skill · Depende de · Aceite · Verificação.** Há
> dependências reais (a costura de modo precede o modo arena; a medição-ouro precede a costura).
>
> **Não confundir com:** `docs/roadmap_refactor_kaelis.md` (roster/kits/gacha das Kaelis),
> `docs/roadmap_meta_gameplay.md` (meta-gameplay, tuning por papel) e `docs/ROADMAP.md` (tasks
> pequenas Codex). Este arquivo trata de **variedade de lugares e modos de jogo**: a dungeon
> procedural vira *um* modo, e o primeiro modo novo é a **arena de sobrevivência**. Toca
> **principalmente o backend** (`Engine`, `Domain`, `Hubs`), mais HUD pontual de frontend.

---

## Modelos & quando usar

| Modelo | Papel | Effort típico | Por quê |
|---|---|---|---|
| **Claude Code Opus 4.8** | Costura de engine, design de modo, pacing de waves, estudo de design, qualquer coisa onde errar cascateia no tick | `high` / `medium` | Decisões de game design + invariantes de engine (determinismo, backend autoritativo). Vale o modelo premium. |
| **GPT-5.5 (Codex)** | Mudanças bounded com regra já fechada: teste-ouro mecânico, HUD/menu copiando padrão existente | `low` / `medium` | Regra explícita e padrão a seguir. Barato e rápido. |

- Use **`use context7`** ao consultar API de biblioteca (ASP.NET Core, SignalR, Angular) nos prompts.
- **Nenhuma skill é necessária** para esta trilha — as decisões de design vivem nos próprios prompts.

---

## Invariantes inegociáveis (todo prompt respeita)

- **Backend autoritativo.** Frontend nunca simula combate/movimento — só interpola e renderiza.
- **Determinismo do engine.** `GameWorld` usa só o `Rng` da run. Nunca `Random`, `DateTime.Now`, ou
  iteração de coleção instável dentro do tick. **Spawn de waves também é determinístico** (`Rng` + contagem de tick).
- **Todas as constantes de simulação em `Domain/GameConfig.cs`.** Nada de hardcode no tick.
- **IDs estáveis** (espécies, `waifu:*`, ids de POI) não são renomeados; mudança exige migração.
- **Assets do Tibia por enquanto.** Tileset próprio é a última prioridade (ver `## Depois`); não desvincular nesta trilha.
- `dotnet build` (backend) e `npx ng build` (frontend) passam sem erro ao fim de cada prompt que tocar o respectivo lado.

---

## Tese

Hoje só existe **um** lugar: a dungeon procedural (`Engine/DungeonGenerator.cs`), rodada como uma run
pelo `Engine/GameWorld.cs`. A visão é ter **variedade de lugares e modos**, com a dungeon virando *um*
modo entre vários. Aprendemos com o Tibia *como* ele constrói cidades, dungeons circulares, salas de
boss e arenas, e replicamos esses padrões no Kaezan — sem importar pixels (assets do Tibia continuam
servindo de visual por ora; o estudo é de **estrutura/design**).

Começamos cauteloso: peças baratas que destravam e de-riscam (o doc de estudo + uma rede de segurança
de determinismo), o **mínimo de abstração** que o primeiro modo exige, e a **arena como fatia vertical
fina**. A abstração de modos amadurece quando tivermos 2–3 modos — nada de framework especulativo.

## Decisões Fechadas

- A dungeon procedural atual vira **um modo** (`DungeonMode`); não é reescrita, só fica atrás de uma costura.
- **Primeiro modo novo = arena de sobrevivência** (sala única + waves de mobs até morrer). Reusa o combate atual.
- **Aprender do Tibia = doc de estudo de design** (`docs/design/tibia_map_patterns.md`), sem nenhuma ferramenta/parser de OTBM.
- **Tileset próprio é a ÚLTIMA preocupação** — segue com assets do Tibia nesta trilha.
- **Hazard no tick (dano vindo do mapa) está cortado** — pouco ganho, risco ao determinismo. Fica no `## Depois`.
- Toda mexida no `GameWorld`/gerador passa pelo **teste-ouro de determinismo** (LM-01) antes de ser considerada pronta.

## Modos alvo (visão)

| Modo | Lugar | Spawn | Objetivo / fim | Status |
|---|---|---|---|---|
| **Dungeon** (atual) | andares procedurais | pré-colocado por sala | descer pela escada / chegar no boss | existe (vira `DungeonMode` na LM-03) |
| **Arena de sobrevivência** | sala única | waves que escalam | sobreviver; morte encerra; score = waves+tempo | LM-04/LM-05 |
| **Boss-rush** | sequência de salas de boss | só bosses | vencer a sequência | `## Depois` |
| **Cidade + 1 house** | mapa autorado sem combate | NPCs de lore | construir/decorar a house | `## Depois` |

---

## Mapa de prompts (escopo)

| Prompt | Tema | Modelo | Effort | Depende de | Onda |
|---|---|---|---|---|---|
| LM-01 ✅ | Teste-ouro de determinismo (rede de segurança) | GPT-5.5 (Codex) | medium | — | 1 |
| LM-02 ✅ | Doc de estudo de design do Tibia | Opus 4.8 | medium | — | 1 |
| LM-03 ✅ | Costura mínima de modo/mapa (⭐ fundação) | Opus 4.8 | high | LM-01 | 2 |
| LM-07 | Qualidade de geração (bedrock fill + auto-tiling + regras de decor) | Opus 4.8 | medium | LM-03 | 3 |
| LM-04 | Modo Arena — backend (mapa + waves + fim) | Opus 4.8 | high | LM-07, LM-02* | 4 |
| LM-05 | Modo Arena — frontend (HUD + entrada) | GPT-5.5 (Codex) | medium | LM-04 | 5 |
| LM-06 | Skill `map-mode-author` (scaffold de modo novo) | Opus 4.8 | medium | LM-05 | 6 |

> O `*` em LM-04 marca dependência **branda**: LM-02 *informa* o design da arena (spec de "layout de
> arena"), mas não bloqueia o código. Se o doc não estiver pronto, use o padrão sugerido no próprio LM-04.
>
> **LM-06 é o ÚLTIMO prompt, de propósito.** Só destila numa skill um padrão **já testado e validado** —
> a costura (LM-03) + o modo arena de ponta a ponta (LM-04/LM-05). Implementar antes seria codificar um
> padrão adivinhado. Ideal: rodar só depois que a arena estiver jogável e validada (e melhor ainda com um
> 2º modo em vista, que confirma que o padrão de fato se repete).
>
> **LM-07 vem ANTES do LM-04 (apesar do número maior — foi adicionado depois do roadmap inicial).** A
> arena constrói o mapa dela reusando as primitivas de geração (`ClassifyWall`, pintura de tile). Arrumar
> a qualidade **antes** faz a arena já nascer com paredes limpas e bedrock-fill — sem retrabalhar os dois.

---

## Execução paralela ⭐

**Regra de ouro:** dois prompts só rodam em paralelo se (a) as dependências fecharam **e** (b) não
editam o mesmo arquivo. Casamento natural: 1 Opus + 1 Codex por onda.

```
Onda 1   LM-02 (Opus · doc Tibia)      ‖  LM-01 (Codex · teste-ouro)
              │                              arquivos disjuntos: docs/design vs tools/BalanceSim+docs/balance
              ▼
Onda 2   LM-03 (Opus · costura modo/mapa, solo)
              │                              toca o coração do engine (GameWorld/RunManager) — sem par disjunto seguro
              ▼
Onda 3   LM-07 (Opus · qualidade de geração, solo)
              │                              DungeonGenerator + renderer — arruma as primitivas ANTES da arena
              ▼
Onda 4   LM-04 (Opus · arena backend, solo)
              │                              GameMode/ArenaMode + GameDtos + GameHub (já herda paredes limpas)
              ▼
Onda 5   LM-05 (Codex · arena frontend, solo)
              │
              ▼
Onda 6   LM-06 (Opus · skill map-mode-author, solo — só após o padrão estar validado)
```

**Conflitos que forçam sequencial:**
- **LM-03 → LM-07** — LM-07 reusa as primitivas/saída da costura e rebaselina o teste-ouro (LM-01).
- **LM-07 → LM-04** — a arena constrói seu mapa com as primitivas de geração; com a qualidade arrumada
  antes, ela nasce limpa e não há retrabalho nos dois.
- **LM-04 → LM-05** — LM-05 consome os campos novos de DTO (`GameDtos.cs`) e o método de hub criados em LM-04.
- **LM-05 → LM-06** — a skill só pode destilar um padrão que já existe e foi validado de ponta a ponta.

**Caminho crítico:** LM-01 → LM-03 → LM-07 → LM-04 → LM-05 (→ LM-06). LM-02 "sai de graça" em paralelo
na Onda 1 e já entrega valor sozinho (é o aprendizado visual que motivou a trilha).

---

# LM-01 — Teste-ouro de Determinismo (rede de segurança)  ⭐ medição primeiro

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** LM-02 (Onda 1)
- **[x] Concluído.** Modo `--golden`/`--golden-check` em `tools/BalanceSim` (`Golden.cs`): hash SHA-256 por
  andar (Ground/Wall/Decor/Blocked + Room{X,Y,W,H,Role} + Entry/LadderDown/Chests/Sanctuaries) para
  7 seeds fixas × 5 tiers × 2 andares, espelhando o `GameWorld`. Baseline em
  `docs/balance/golden_dungeon.txt`. Comparador sai ≠0 com diff legível; determinismo e green confirmados.

**Objetivo:** criar uma **baseline reprodutível** da saída do gerador/run para seeds fixas, de modo que
qualquer mexida futura em `GameWorld`/`DungeonGenerator` (LM-03, LM-04 e o `## Depois`) possa ser
verificada contra ela. Sem essa rede, o refactor da costura (LM-03) não tem como provar paridade.

**Contexto técnico / Arquivos prováveis:**
- Gerador: `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` (`DungeonFloor` com
  `ushort[] Ground/Wall/Decor`, `bool[] Blocked`, `List<Room> Rooms` com `Role`, `Entry`, `LadderDown`, `Chests`).
- RNG: `backend/src/KaezanArenaFable.Api/Engine/Rng.cs` (xorshift seedado).
- Biomas: `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs` (`Biomes.ForTier(tier)`).
- Harness existente: `tools/BalanceSim/` (já referencia o engine). Use-o como casa do modo-ouro
  (evita assumir projeto de teste). Baselines versionados em `docs/balance/`.

**Tarefas:**
- Adicionar um modo ao `tools/BalanceSim` (ex. `--golden`) que, para uma lista fixa de seeds e tiers,
  gera o(s) andar(es) via `DungeonGenerator.Generate(...)` e computa um **hash estável** por andar de:
  `Ground`, `Wall`, `Decor`, `Blocked`, a sequência de `Room{X,Y,W,H,Role}`, `Entry`, `LadderDown` e `Chests`.
- Emitir um arquivo de baseline determinístico (ex. `docs/balance/golden_dungeon.txt` — seed → hashes).
- Modo de **comparação**: re-gera e falha (exit code ≠ 0 + diff legível) se algum hash divergir do baseline.
- Documentar no topo do arquivo de baseline como regenerá-lo conscientemente (quando uma mudança de
  geração for **intencional**, rebaseline explícito).

**Aceite:**
- Rodar o modo-ouro duas vezes seguidas dá resultado idêntico (determinismo confirmado).
- O comparador falha se o gerador mudar e passa se não mudar.
- Baseline commitado em `docs/balance/`.

**Verificação:** `dotnet build` da solução/tools limpo. Rodar o modo-ouro → baseline; rodar de novo em
modo comparação → verde. (Conferência cruzada: alterar 1 constante de `GameConfig` de sala localmente
deve fazer o comparador falhar; reverter volta ao verde.)

---

# LM-02 — Doc de Estudo de Design do Tibia

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** LM-01 (Onda 1)
- **[x] Concluído.** `docs/design/tibia_map_patterns.md`: 4 arquétipos (cidade, hunt circular, sala de
  boss, arena) cada um com (a) o que o Tibia faz / (b) por quê / (c) spec em parâmetros do engine / (d)
  fora de escopo + tabela final "Padrão → modo/prompt". Base empírica medida no `otservbr-monster.xml`
  (radius 1/3 dominantes, spawntime 60–90s, bolsões multi-espécie). §4 (arena) traz os números prontos
  para a LM-04 pôr em `GameConfig` (sala 21×15, 8 pontos de borda, budget de wave, thresholds).

**Objetivo:** produzir `docs/design/tibia_map_patterns.md` — um estudo **acionável** de como o Tibia
constrói lugares, traduzido para os conceitos do nosso engine. É o "aprendizado visual" que motivou a
trilha e alimenta a arena (LM-04), o boss-rush e a cidade (`## Depois`). **Não é tooling** — é leitura +
síntese de design.

**Contexto técnico / Fontes (sem parser):**
- Formato e estrutura de mapa do OT: `C:\Kaezan\kaezan\mapping\baseline\canary\systems\map.md`
  (hierarquia OTBM, spawns, teleports, áreas instanciadas).
- **Spawn XMLs legíveis** (texto puro — dá pra ler/grep padrões de raio/clustering/respawn):
  `C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\world\otservbr-monster.xml`.
- Dungeons de quest discretas como exemplos de "sala de boss"/arena:
  `C:\Kaezan\kaezan\canary-3.4.1\data-otservbr-global\world\quest\` (Soul War, Dream Courts, Cults of Tibia).
- Conhecimento geral de design do Tibia (cidades, hunts circulares, salas de boss). `use context7` se consultar algo de lib.
- Nosso lado, para mapear os padrões: `Engine/DungeonGenerator.cs` (rooms/corredores/POIs/`Role`),
  `Domain/GameConfig.cs` (budgets de spawn, tamanhos), `Domain/Biomes.cs`.

**Tarefas (uma seção por arquétipo):**
- **Estrutura de cidade:** quarteirões, praça/templo/depot, malha viária, blocos de casa, NPCs de serviço.
  → spec: como compor um mapa de cidade no nosso formato (salas grandes + corredores largos + POIs de NPC).
- **Hunt circular:** anel de corredores com bolsões de spawn; rota de "fazer a volta". → spec: geometria
  (raio, largura do anel, nº de bolsões) e densidade de spawn por bolsão mapeada ao nosso `SpawnBudget`.
- **Sala de boss:** forma da arena, choke de entrada, espaço de kite, pilares/cobertura, escolta. → spec:
  parâmetros para um modo boss-rush e para a sala de boss da dungeon atual.
- **Layout de arena (foco):** forma e tamanho ideais para waves, pontos de entrada de spawn, espaço de
  kite/recuo. → spec consumida diretamente pela LM-04.
- Em cada seção: **(a)** o que o Tibia faz, **(b)** por que funciona, **(c)** a spec traduzida em
  parâmetros do nosso engine (tamanhos em tiles, contagens, faixas de spawn), **(d)** o que fica de fora/risco.
- Fechar com uma tabela "Padrão → Onde aplica no Kaezan (modo/prompt)".

**Aceite:**
- Doc existe em `docs/design/tibia_map_patterns.md` com as 4 seções de arquétipo + tabela final.
- Cada arquétipo termina em **specs acionáveis** (números/parâmetros), não descrição vaga.
- A seção de arena é concreta o suficiente para a LM-04 consumir (forma, tamanho, pontos de spawn).
- Nenhuma mudança de código/gameplay (só docs).

**Verificação:** revisão de texto; nenhum build necessário (só docs). Sanidade: a spec de arena casa
com os números que a LM-04 usa.

---

# LM-03 — Costura Mínima de Modo/Mapa  ⭐ fundação de engine

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ SignalR/ASP.NET) · **Depende de:** LM-01 · **Paraleliza com:** — (solo, Onda 2)
- **[x] Concluído.** `Engine/GameMode.cs`: enum `GameMode { Dungeon, Arena }` + estratégia
  `GameModeStrategy` (abstrata) que localiza as 3 diferenças — `BuildFloors` (fonte de mapa),
  `Populate`/`OnTick` (povoamento inicial + waves) e `OnMonsterKilled` (condição de fim) — com
  `DungeonModeStrategy` rodando o comportamento legado (2 andares + boss = vitória). `GameWorld`
  roteia construção/povoamento/morte pela costura; tick, movimento, combate, snapshot e reward
  seguem compartilhados. `JoinRun` aceita `mode` (default Dungeon). Como o SignalR exige aridade
  exata, `game-client.service.ts` passa o `mode` (default Dungeon) — fluxo legado idêntico.
  **Verificado:** `dotnet build`/`ng build` limpos; LM-01 `--golden-check` VERDE (70 andares);
  run de dungeon jogada no preview sem mudança de comportamento. Arena pluga aqui na LM-04.

**Objetivo:** introduzir o **mínimo de abstração** que separa *fonte de mapa* (como o lugar é
produzido) de *ruleset/modo* (objetivo + comportamento de spawn + condição de fim), e mover a run atual
para trás dessa costura como `DungeonMode` — **sem mudança de comportamento** (paridade verificada por
LM-01). Só abstraia o que a arena (LM-04) vai precisar; nada especulativo.

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` — hoje o construtor gera os andares
  (`DungeonGenerator.Generate`), faz `SpawnFloorMonsters`/`SpawnPois`, e `Tick()` retorna
  `(SnapshotDto, MapDto?)`. É aqui que a fonte de mapa, o spawn e a condição de fim estão entrelaçados.
- `backend/src/KaezanArenaFable.Api/Hubs/GameHub.cs` — `JoinRun(tier, waifuId, seed, resume)` constrói
  `new GameWorld(runSeed, tierDef, waifu, ...)` e chama `runs.StartRun`.
- `backend/src/KaezanArenaFable.Api/Engine/RunManager.cs` — tica todas as runs; agnóstico de modo (não deve precisar mudar muito).
- `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` — constantes.

**Tarefas:**
- Definir o conceito de modo de forma enxuta. Sugestão: um `enum GameMode { Dungeon, Arena }` (ou uma
  pequena estratégia/`IGameMode`) que localiza as 3 diferenças: **(1)** construção do mapa (andares de
  dungeon × sala única de arena), **(2)** povoamento (pré-spawn por sala × agendador de waves), **(3)**
  condição de fim (descer/boss × sobreviver). Mantenha tick, movimento, combate, snapshot e reward
  **compartilhados**.
- Refatorar `GameWorld` para receber o modo e rotear essas 3 decisões pela costura; a run atual passa a
  ser `Dungeon`. Não duplicar o loop de tick nem o pipeline de combate.
- Passar o modo pelo `JoinRun` (parâmetro novo com default `Dungeon` para não quebrar o cliente atual).
- Garantir que `RunManager`/reconnect continuam funcionando independente do modo.

**Aceite:**
- A run de dungeon roda **idêntica** ao comportamento atual (LM-01 verde sem rebaseline).
- Existe uma costura clara onde um modo novo pluga mapa+spawn+fim sem tocar o pipeline de combate.
- `JoinRun` aceita o modo (default `Dungeon`); cliente atual não quebra.
- Determinismo preservado (só `Rng` da run).

**Verificação:** `dotnet build` limpo. **LM-01 em modo comparação: verde** (paridade do gerador). Subir
e jogar uma run de dungeon no preview — comportamento inalterado, sem erro de console.

---

# LM-04 — Modo Arena de Sobrevivência (backend)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ SignalR) · **Depende de:** LM-03, LM-02* (branda) · **Paraleliza com:** — (solo, Onda 3)

**Objetivo:** implementar o backend do primeiro modo novo: uma **sala de arena** única onde **waves de
mobs** escalam até o jogador morrer. Reusa o combate, o `SpawnMonster` e os tiers de mob existentes —
só troca mapa, povoamento e condição de fim pela costura da LM-03.

**Contexto técnico / Arquivos prováveis:**
- `Engine/GameWorld.cs` (ou novo `Engine/ArenaMode.cs` atrás da costura) — build do mapa de arena +
  agendador de waves + condição de fim "sobreviver".
- Spawn reaproveitado: `SpawnMonster(...)` e os tiers de mob em `Domain/GameConfig.cs` (CommonMobs/EliteMobs/StatMultiplier por tier).
- Mapa: reusar tiles de bioma via `Domain/Biomes.cs` (`Biomes.ForTier`); a arena é uma sala aberta
  (paredes em volta) — informada pela spec de arena de `docs/design/tibia_map_patterns.md` (LM-02).
- DTOs: `Engine/GameDtos.cs` — `MapDto`/`SnapshotDto`/`RunStateDto`. Adicionar os campos de modo/wave/score.
- Entrada: `Hubs/GameHub.cs` `JoinRun` (modo `Arena`).
- Recompensa: `Meta/RewardService` (aplicado pelo `RunManager` ao fim) — mapear score → reward.

**Modelo de arena (ponto de partida — afinar e mandar tudo p/ `GameConfig.cs`):**
- **Mapa:** sala retangular aberta (ex. ~21×15 tiles) com parede de borda, `Entry` no centro; sem POIs/escada.
- **Waves:** wave `w` começa quando a anterior é limpa (ou após um intervalo de graça). Orçamento de
  spawn por wave escala com `w` e com o tier (reusar a lógica de budget: base × (1 + crescimento·(w-1))).
  Elites entram a partir de wave `N`; mini-boss a cada `M` waves. Spawns aparecem em pontos de borda
  determinísticos (via `Rng` da run), nunca em cima do jogador.
- **Fim:** morte do jogador encerra a run (`Ended` com vitória=false). Rastrear `WavesCleared` e `ElapsedMs`.
- **Score → reward:** score derivado de waves limpas (+ tempo/kills) vira gold/XP/kaeros via `RewardService`.

**Tarefas:**
- Construir o mapa de arena (sala única) na costura de fonte-de-mapa do modo `Arena`.
- Implementar o agendador de waves determinístico (estado por-run; só `Rng` + contagem de tick),
  reusando `SpawnMonster` e os tiers; escalonar contagem/elite/mini-boss por wave.
- Implementar a condição de fim "sobreviver" e o tracking de `WavesCleared`/score.
- Expor no snapshot os campos de modo/wave/score (estender `RunStateDto`/`MapDto` conforme necessário) —
  manter o núcleo `ushort[]` do `MapDto`.
- Ligar `JoinRun(mode=Arena)` e mapear score → reward no fim.
- Pôr **todas** as constantes novas (tamanho da arena, budget/escala de wave, intervalo de graça,
  thresholds de elite/mini-boss, fórmula de score) em `Domain/GameConfig.cs`.

**Aceite:**
- Iniciar uma run em modo arena gera a sala única e a primeira wave.
- Limpar uma wave dispara a próxima, com dificuldade crescente (mais/maiores mobs).
- Morte do jogador encerra a run com `WavesCleared`/tempo e concede recompensa coerente com o score.
- Determinístico: mesma seed + mesmos inputs → mesma sequência de waves/spawns (LM-01-style: estável).
- Constantes em `GameConfig.cs`; dungeon mode segue intacto (LM-01 verde).

**Verificação:** `dotnet build` limpo. Subir e, via preview MCP, jogar uma arena: waves escalam, morte
encerra, `Ended` traz waves/score. Rodar a mesma seed duas vezes e confirmar sequência de waves idêntica.

---

# LM-05 — Modo Arena (frontend: HUD + entrada)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma (`use context7` p/ Angular/SignalR) · **Depende de:** LM-04 · **Paraleliza com:** — (solo, Onda 4)

**Objetivo:** dar ao jogador como **entrar** no modo arena e um **HUD** que mostra a wave atual e o
score. Tarefa bounded de frontend — consome os campos de DTO e o `JoinRun(mode)` criados na LM-04;
preserva a lógica (backend autoritativo, cliente só renderiza).

**Contexto técnico / Arquivos prováveis:**
- Tipos: `frontend/src/app/core/types.ts` — espelhar os campos novos de `RunStateDto`/`MapDto` (modo/wave/score).
- Cliente de jogo / chamada de hub: o serviço que invoca `JoinRun` (passar o modo).
- Tela de pré-run / menu: onde hoje se escolhe tier/Kaeli — adicionar a opção "Arena".
- HUD da run: a página `pages/game/` — exibir wave atual e score (espelhar os chips/HUD existentes,
  ex. o chip de passiva/level já presentes).
- Renderer: `frontend/src/app/core/renderer.ts` — **inalterado** (desenha o mapa de arena como qualquer mapa).

**Tarefas:**
- Atualizar `types.ts` com os campos novos do snapshot.
- Adicionar a entrada de modo arena no fluxo de início de run (botão/seleção que chama `JoinRun(mode=Arena)`).
- Mostrar no HUD a wave atual e o score; na tela de fim, exibir waves limpas + recompensa.
- Garantir que o reveal de fim de run lida com `Ended` do modo arena (vitória=false por morte) sem quebrar.

**Aceite:**
- Dá pra iniciar uma arena pela UI.
- HUD mostra a wave subindo durante a run; tela de fim mostra waves limpas + recompensa.
- Nenhuma regressão na UI da dungeon; console limpo.

**Verificação:** `npx ng build` limpo. Via preview MCP: iniciar arena, ver o HUD de wave escalando,
morrer e ver a tela de fim com waves/score. Screenshots de arena + tela de fim.

---

# LM-06 — Skill `map-mode-author` (scaffold de modo novo)  ⭐ último, só após validação

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma (cria uma) · **Depende de:** LM-05 · **Paraleliza com:** — (solo, Onda 5)

**Objetivo:** destilar o padrão **já testado** de "adicionar um modo/lugar" (costura da LM-03 + arena de
ponta a ponta LM-04/LM-05) numa **única skill** parametrizada por arquétipo, que faz scaffold consistente
de um modo novo seguindo as convenções do repo. **Não** é uma skill por tipo de mapa — o arquétipo é
argumento, e a spec de cada arquétipo vive como dado em `docs/design/tibia_map_patterns.md` (LM-02).

**Pré-condição (não comece antes):** a arena (LM-04/LM-05) precisa estar **jogável e validada**. A skill
só pode codificar um padrão que já existe; idealmente rode com um 2º modo em vista (boss-rush) para
confirmar que o padrão se repete de fato. Se o padrão ainda não estabilizou, **adie**.

**Contexto técnico / Arquivos prováveis:**
- Modelo de skill existente no repo: `.claude/skills/franchise-mining/` (tem *modos* internos — espelhe
  esse formato de "uma skill, vários modos") e `.claude/skills/roadmap-from-plan/`.
- A costura de modo concreta (depois da LM-03): `Engine/GameWorld.cs` + onde mapa/spawn/fim plugam.
- Base de conhecimento de arquétipo: `docs/design/tibia_map_patterns.md`.
- Invariantes a embutir: determinismo (`Rng` da run), constantes em `Domain/GameConfig.cs`, teste-ouro
  (`tools/BalanceSim --golden`), backend autoritativo, assets do Tibia por ora.

**Tarefas:**
- Criar `.claude/skills/map-mode-author/SKILL.md` com o arquétipo (arena/boss-rush/cidade/hunt circular)
  como argumento/modo.
- A skill deve produzir, para um modo novo: a spec do arquétipo (puxando do doc LM-02), o checklist de
  onde plugar na costura (mapa/spawn/fim), as constantes a criar em `GameConfig`, e como estender o
  teste-ouro — espelhando o que a arena fez.
- Documentar quando **não** usá-la (modo que não cabe na costura, ou que precise de novo pipeline de combate).

**Aceite:**
- Existe **uma** skill (não várias) que gera o scaffold de um modo novo a partir de um arquétipo.
- Rodá-la para "boss-rush" produz um plano de implementação coerente com a costura real e o doc LM-02.
- A skill embute os invariantes (determinismo, `GameConfig`, teste-ouro) — não os deixa o autor reinventar.

**Verificação:** invocar a skill para um arquétipo (ex. boss-rush) e conferir que o artefato gerado é
acionável por um agente frio e respeita as convenções; nenhum build (a skill é processo/autoria).

---

# LM-07 — Qualidade de Geração de Dungeon (vem antes da arena)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** LM-03 · **Paraleliza com:** — (solo, Onda 3)

**Objetivo:** arrumar os defeitos visíveis de geração que tornam alguns mapas "estranhos" (vazios pretos
de borda dura, "dentes" nos cantos das paredes, lava/decor solta em corredor) **antes** de a arena
construir o mapa dela — assim a arena reusa as primitivas já limpas e não há retrabalho dos dois. Tudo
server-side, determinístico e reusando tiles do Tibia atuais (zero arte nova).

**Diagnóstico (causas reais observadas):**
- **Vazios pretos:** o renderer só desenha onde `map.ground[i]`/`map.wall[i]` ≠ 0
  (`frontend/src/app/core/renderer.ts:630-745`); o gerador só pinta parede em células bloqueadas que
  *encostam* em chão (`DungeonGenerator.PaintTiles`), então o miolo de rocha sólida fica `0/0` → fundo preto.
- **Corner teeth:** `ClassifyWall` usa vizinhança-4 (`DungeonGenerator.cs:291-305`) — não resolve cantos.
- **Decor/accent solto:** `PaintTiles` espalha `Accent`/`Decor` por chance de célula, inclusive em corredor estreito.

**Contexto técnico / Arquivos prováveis:**
- `backend/src/KaezanArenaFable.Api/Engine/DungeonGenerator.cs` — `PaintTiles`, `ClassifyWall`, `ReservedCells`.
- `backend/src/KaezanArenaFable.Api/Domain/Biomes.cs` — peças de parede disponíveis por bioma (`WallH/V/Pole/Corner`).
- `frontend/src/app/core/renderer.ts` — desenho de ground/wall/decor (linhas ~630-745), se precisar de backdrop de void.
- Rede de segurança: `tools/BalanceSim --golden` / `docs/balance/golden_dungeon.txt` (LM-01) — **rebaseline consciente**.

**Tarefas:**
- **Bedrock fill:** pintar as células bloqueadas que *não* encostam em chão com um tile de rocha
  opaco do bioma (ou um backdrop tintado no renderer), pra a negativa do mapa ler como maciço de rocha,
  não buraco preto. Maior ganho visível, ganho puro.
- **Auto-tiling melhor:** trocar a classificação de parede para vizinhança-8 (ideal: bitmask 47-blob),
  escolhendo a melhor peça **entre as disponíveis no bioma**. Onde o conjunto de sprites do Tibia não
  cobre todos os cantos, fechar com a peça sólida (sem deixar dente). *(47-blob pleno depende de arte
  própria — fica para o item de tileset no `## Depois`; aqui melhoramos dentro do que existe.)*
- **Regras de decor/accent:** só dentro de sala (não em corredor estreito), agrupar em vez de pontilhar,
  densidade/raio em `GameConfig.cs`. Lava (accent) coerente com o bioma, não no meio do caminho.
- Manter determinismo (só `Rng` da run) e **rebaselinar o teste-ouro** conscientemente (a saída muda de propósito).

**Aceite:**
- Sem vazios pretos de borda dura no interior dos mapas (rocha maciça preenche).
- Cantos de parede sem "dentes" perceptíveis (dentro das peças do bioma).
- Decor/accent não aparece solto em corredor; lava lê como ambiente, não obstáculo aleatório.
- Determinístico; LM-01 rebaselinado de propósito (não por não-determinismo); constantes em `GameConfig.cs`.

**Verificação:** `dotnet build` + `npx ng build` limpos. Rodar a mesma seed do print (tier 2, `?waifu:gaia`)
e, via preview MCP, comparar screenshots antes/depois — sem vazios pretos, paredes limpas, decor contextual.
`tools/BalanceSim --golden-check` verde contra a baseline nova.

---

## Depois (fora de escopo desta trilha — não perder as ideias)

Em ordem de interesse do usuário:
1. **Boss-rush** — sequência de salas/arenas de boss, usando a spec de "sala de boss" da LM-02. Pluga
   na costura de modo (LM-03) como `BossRush`.
2. **Cidade autorada + 1 house construível** — mapa sem combate, NPCs de lore + **uma** house do
   jogador (persistência em `Meta/Persistence` + UI de construção). Informada pela spec de cidade da LM-02.
3. **Dungeon procedural mais rica** — o passe básico de qualidade (bedrock fill, auto-tiling dentro das
   peças do Tibia, regras de decor) foi **promovido para LM-07**. Fica aqui o que é maior/estrutural:
   prefabs/"stamps" de sala (data-driven), salas orgânicas (autômato celular), 47-blob pleno (depende de
   arte própria) e fontes de luz. Baixo risco e independente de arte na maior parte.
4. **Tilesets próprios (desvínculo do Tibia)** — ÚLTIMA prioridade. O renderer já é agnóstico de id
   (`assets.service.ts`), então é troca de manifest/ids num range próprio quando chegar a hora.
5. **Hazard-as-gameplay (dano vindo do mapa)** — cortado por ora (pouco ganho, risco ao determinismo).
