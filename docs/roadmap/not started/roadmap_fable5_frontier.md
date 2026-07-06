# Roadmap — Fable 5 Frontier

> **O que é este doc.** A fila de features reservadas para o **Claude Fable 5** durante o período
> de testes do modelo. O critério é mais duro que o do antigo `docs/FABLE_TRACK.md` (**legado —
> não usar como fonte**): aqui só entra trabalho onde **Sonnet 5 e Opus 4.8 provavelmente
> falhariam** — não por volume de código, mas por exigir *simultaneamente* raciocínio
> arquitetural longo, disciplina de determinismo num hot path de ~5.600 linhas, decisões de
> design sem spec fechada e rigor estatístico/algorítmico.
>
> Cada feature abaixo declara **por que o Opus 4.8 falharia** — se a justificativa não convencer,
> a feature desce para os roadmaps normais (Opus/Codex).
>
> Para executar: converter a feature escolhida em prompts NN com a skill `roadmap-from-plan`,
> ou rodar direto como uma sessão longa de Fable 5 (plan mode primeiro).

## Foco atual (2026-07-02): consolidar antes de expandir

**Decisão do usuário:** parar de empilhar features novas em cima de um engine que ainda não tem
rede de segurança nem estrutura — **melhorar o que existe primeiro**. As features de produto
(Echo Team, co-op, gerador-avaliador) saíram da fila ativa e foram para `## Pausadas` no fim do
doc — a análise e o design continuam lá, só não são o trabalho de agora. **Fila ativa: FF-01 →
FF-02.**

---

## Estado atual (fatos que ancoram as propostas)

- **Engine determinístico:** `Engine/GameWorld.cs` (~5.6k linhas, um único arquivo — o maior
  god-class do repo), tick 100ms, um único `Rng` xorshift128+ por run, comandos via
  `Enqueue(Command)`. Card offers pausam o relógio da simulação; refresh preserva a run por 60s
  (`RunManager` + orfanato por conexão).
- **RunManager:** 1 run por conexão SignalR (`ConcurrentDictionary`), snapshot por cliente.
  Reiniciar o backend mata todas as runs (nenhum estado sobrevive ao processo).
- **BalanceSim (`tools/BalanceSim`):** simulador headless que varre {7 Kaelis} × {5 tiers} × {N
  seeds} com autopilot e mede TTK/hunt time/dano — mais o **teste-ouro do gerador** (LM-01,
  hashes SHA-256 de layout em `docs/balance/golden_dungeon.txt`). Cobre só o **layout**, não o
  combate/tick inteiro — é a lacuna que o FF-01 fecha.
- **Kits 2×2 implementados** (`docs/design/kaelis_kit_reformulation.md`), mas **o feeling
  continua ruim na prática** mesmo pós-reformulação (números, execução do autopilot e feedback
  visual todos suspeitos) — sinal de que o problema não é só "achar os valores certos" (isso é
  medição), é também estrutural: o kit dispatch e a IA do helper vivem espalhados dentro do
  mesmo arquivo de 5.6k linhas, difícil de auditar kit-a-kit.
- **Jogo é autoplay-first:** a graça é assistir a run bem jogada, não pilotar.

## Invariantes (valem para todas as features)

- Backend autoritativo; frontend só interpola/renderiza.
- Determinismo do tick: só o `Rng` da run; nunca `Random`/`DateTime.Now`/`Guid.NewGuid()`/
  iteração instável. **Toda feature daqui passa pelo golden (LM-01) e por `dotnet build` +
  `npx ng build` antes de ser considerada pronta.**
- Constantes em `Domain/GameConfig.cs`; IDs estáveis; skills por shape parametrizado.
- Código e strings de jogador em inglês; docs em PT.

---

## Fila ativa

| # | Feature | Por que Fable-tier (resumo) | Depende de |
|---|---|---|---|
| FF-01 ✅ | **Replay bit-perfect (hash de estado)** | auditar 5.6k linhas atrás de não-determinismo sutil sem quebrar o golden; vira o gate que prova qualquer refactor futuro não mudou comportamento | — |
| FF-02 | **Decomposição do `GameWorld` (fim do god-class)** | reorganizar o hot path determinístico em unidades menores **sem alterar 1 bit de resultado** — o tipo de refactor onde "parece" preservar comportamento mas sutilmente não preserva | FF-01 (gate obrigatório) |

FF-01 primeiro **de propósito**: sem hash de estado, um refactor de 5.6k linhas não tem como
provar que preservou comportamento — só "parece igual". Com o gate em mãos, FF-02 vira segura de
tentar, e deixa o terreno pronto pra próxima rodada de balance/feeling (fora desta fila por ora).

---

## FF-01 ✅ — Replay bit-perfect (hash de estado)

> **Concluído 2026-07-02.** `GameWorld` grava `(tick, Command)` no ponto de aplicação + hash
> canônico SHA-256 (checkpoint a cada 100 ticks) em `Engine/GameWorld.Replay.cs` (partial);
> `RunManager` persiste gzip-JSON em `.data/replays/` (cap `GameConfig.ReplayKeepLast`);
> `BalanceSim --replay-check <arquivo|pasta>` re-simula e bissecta divergência,
> `--save-replays <dir>` gera bateria headless. Validado: 33/33 replays headless (7 Kaelis ×
> 5 tiers) + 1 run real de browser **com resume no meio** bit-perfect; comando adulterado → FAIL
> no checkpoint 100; `Random` plantado → 25/33 FAIL (e revertido). A auditoria não achou fonte
> de não-determinismo no tick (Dictionary/HashSet nunca dependem de ordem de bucket; floats
> quantizados a 6 casas no hash). **Nota:** o golden (LM-01) estava **pré-quebrado no main**
> (commits `1e08429`/`57e9832` mudaram DungeonGenerator/Biomes sem rebaselinar — 70/70 floors
> divergiam já em HEAD limpo); rebaselinado junto desta entrega, conforme previsto no aceite.

**A tese.** Hoje o determinismo é um invariante *declarado* e só parcialmente protegido (o golden
do gerador cobre apenas o layout do mapa). Esta feature o torna *provado fim-a-fim*: gravar
`(tick, Command)` de qualquer run, re-simular headless e obter um **hash idêntico do estado
final**. Isso não é feature de produto — é infraestrutura de segurança para tudo que vem depois
(a começar pelo FF-02). Desafio Diário/Ghost/replays-como-produto ficam **fora de escopo por
ora**: o hash + o harness de verificação são o entregável; o resto é upside opcional se sobrar
fôlego, não critério de aceite.

**Por que o Opus 4.8 falharia.**
- A parte difícil não é gravar comandos — é **auditar 5.6k linhas de hot path** atrás de fontes
  sutis de não-reprodutibilidade (ordem de iteração de `Dictionary`, consumo de RNG condicionado
  a estado de conexão, o pause de card offer, o caminho de resume do orfanato, float acumulado)
  e corrigi-las **sem alterar o resultado de nenhuma seed atual** (golden intacto) ou, onde for
  inevitável, declarar e executar um "reset de seeds" num único commit.
- O hash de estado precisa ser **canônico** (ordem estável, sem ponteiros/tempo real) e barato o
  bastante para rodar a cada N ticks em debug — desenhar essa serialização exige entender o
  `GameWorld` inteiro, não um subsistema.

**Design.**
- **Gravação:** `GameWorld` acumula `commands: List<(int tick, Command)>` + metadados
  (`seed, tier, mode, kaeliId, ascension, mastery, equipmentStats congelado, helper profile`).
  Runs terminadas persistem o replay (JSON comprimido em `.data/replays/`, cap de retenção —
  puramente para debug/verificação, não exposto ao jogador ainda).
- **Verificação:** modo do BalanceSim, `--replay-check <replay>`, que re-simula sem SignalR e
  compara o hash do estado final (e hashes intermediários a cada 100 ticks, para bissecção de
  divergência caso algo quebre no futuro).
- **Hash de estado:** SHA-256 sobre uma serialização canônica de: atores (ordenados por id) com
  posição/HP/cooldowns/buffs/marcas, estado do mapa mutável (baús, fields, POIs), relógio de
  simulação, contadores de RNG. Nada de floats crus — quantizar (ex.: `Math.Round(x, 6)`) ou
  migrar os acumuladores críticos para inteiros, decisão a documentar.
- **Bônus estrutural (não é o objetivo, mas cai de graça):** `seed + command log` permite
  reconstruir o estado em qualquer tick — abre a porta pra runs sobreviverem a restart do
  backend e pra um scrub-de-tick no admin, ambos como extensões futuras opcionais.

**Arquitetura/toques.** `Engine/GameWorld.cs` (gravação, hash, correções de determinismo),
`Engine/RunManager.cs` (persistir replay no fim), `tools/BalanceSim` (`--replay-check`).
Constantes novas em `GameConfig` (`ReplayKeepLast`).

**Aceite.**
- Replay de uma run real (jogada no browser, com card offers, dash, resume no meio) re-simula
  com hash final idêntico; mudar 1 comando muda o hash.
- Plantar um `Random` no engine faz `--replay-check` falhar (prova de que o teste pega).
- Golden do gerador (LM-01) intacto (ou reset de seeds documentado num commit único).
- Rodar `--replay-check` contra ~20 replays reais de runs diferentes (tiers/Kaelis variados)
  como bateria de regressão — vira o gate de aceite do FF-02.

**Riscos.** Divergência de float entre Debug/Release ou máquinas (mitigar com quantização no
hash e, se necessário, inteiros nos acumuladores); custo de memória do log em runs longas
(comandos são pequenos — cap generoso); o pause de card offer precisa entrar no log como evento.

---

## FF-02 — Decomposição do `GameWorld` (fim do god-class)

**A tese.** `Engine/GameWorld.cs` tem ~5.6k linhas fazendo tick, movimento, IA de monstro,
autopilot/helper (`TickHelperBox`/`TickHelperMobbing`), dispatch de skill por shape, dano,
postura/Echo Break, dash por classe, e mais. É o maior risco estrutural do repo: qualquer feature
nova (ou até um bugfix) precisa segurar esse arquivo inteiro na cabeça, e é exatamente o tipo de
arquivo onde um refactor malfeito introduz não-determinismo silencioso. Esta feature reorganiza
o arquivo em unidades coesas — **sem mudar 1 bit do resultado de nenhuma run existente**, provado
pelo FF-01.

**Por que o Opus 4.8 falharia.**
- Não é refactor mecânico (extrair método, renomear). É **decompor um hot path determinístico**:
  mover código entre classes pode sutilmente reordenar iteração ou mudar a ordem de consumo do
  `Rng` sem quebrar a build e sem "parecer" errado na leitura — o tipo de erro que só aparece
  como número diferente três semanas depois, indistinguível de ruído de balance.
- Exige manter **toda a superfície de invariantes implícitos** do arquivo (que ordem os atores
  são processados, que efeitos são `fromTrait`/`fromSkill`, o que acontece durante o pause de
  card offer) enquanto se decide os cortes de responsabilidade — julgamento arquitetural sobre um
  sistema vivo, não um exercício de estilo de código.
- O critério de aceite não é "parece mais limpo" — é **hash idêntico** numa bateria de replays
  reais (FF-01). Sem esse gate mecânico, nenhum refactor aqui seria confiável.

**Design (direção, não prescrição rígida — julgamento do executor).**
- Candidatos de extração (nomes ilustrativos, ajustar durante o trabalho):
  - **Movimento/pathing** do player e monstros.
  - **`HelperAi`** — toda a lógica de autopilot/helper (`TickHelperBox`, `TickHelperMobbing`,
    disciplina de AoE, auto-heal, auto-loot/BFS) isolada do resto do tick.
  - **`SkillDispatch`** — o roteamento por shape (`single|beam|nova|area|cone|buff|...`),
    separado da resolução de dano.
  - **`CombatResolver`** — `DealDamageToMonster`/proc de trait/postura/Echo Break.
  - **`ActorState`** — cooldowns, buffs, marcas, dash-por-classe.
  - `GameWorld` fica como orquestrador do tick, delegando pros serviços acima — todos recebendo
    o **mesmo `Rng`** e operando sobre o mesmo `Actor`/estado compartilhado (sem duplicar RNG
    streams aqui; isso é uma decisão de design separada, não desta feature).
- **Cada extração é um commit isolado**, gate: `--replay-check` verde na bateria de replays do
  FF-01 antes de passar pra próxima. Nunca mover duas responsabilidades no mesmo commit.
- Usar os agentes/skills já instalados no marketplace `dotnet-skills` durante o trabalho (ver
  seção de skills/MCP abaixo): `crap-analysis` para achar os hotspots de complexidade primeiro
  (prioriza onde cortar), `csharp-type-design-performance` + `csharp-coding-standards` pra guiar
  a forma dos novos tipos, `slopwatch` rodado a cada commit pra pegar atalho barato (catch vazio,
  teste desabilitado), `csharp-concurrency-patterns`/`dotnet-concurrency-specialist` se mexer no
  locking do `RunManager`.

**Aceite.**
- `GameWorld.cs` sai de ~5.6k linhas para um orquestrador enxuto + N arquivos coesos por
  responsabilidade (meta sugerida: nenhum arquivo novo passa de ~800-1000 linhas; ajustar com
  julgamento, não é lei).
- `--replay-check` (FF-01) idêntico antes/depois em toda a bateria de replays.
- Golden do gerador (LM-01) intacto.
- `dotnet build` limpo; `BalanceSim` roda o baseline de 50 seeds × 7 Kaelis × 5 tiers com números
  idênticos (TTK/hunt time/dano) ao baseline anterior — segunda prova, mais grossa, de que nada
  mudou.
- Nenhuma mudança de comportamento visível ao jogador (o objetivo é 100% estrutural).

**Riscos.** O maior é achar que "compila e os testes de balance batem" é suficiente sem o
`--replay-check` — o hash pega divergências que médias agregadas escondem. Segundo risco: tentar
fazer tudo num commit gigante — force incremental, um subsistema por vez, gate a cada passo.
Terceiro: essa decomposição vai colidir com qualquer trabalho de balance/feeling em paralelo —
por isso está sequenciada **antes** de retomar a auditoria de feeling dos kits, não junto.

---

## Skills e MCP recomendados para esta fila

**Já instalados, usar ativamente:**
- `dotnet-skills:crap-analysis` — identifica hotspots de risco (complexidade × cobertura) no
  `GameWorld.cs`; roda antes do FF-02 pra decidir a ordem de extração pelos dados, não por
  achismo. (Precisa de coverage via OpenCover — se o projeto não tem testes cobrindo o engine
  ainda, vale gerar cobertura mínima do caminho de tick antes, ou usar como leitura qualitativa
  de complexidade ciclomática mesmo sem cobertura completa.)
- `dotnet-skills:csharp-type-design-performance` e `dotnet-skills:csharp-coding-standards` —
  moldam os tipos extraídos (sealed, readonly struct onde fizer sentido, funções estáticas puras
  para os serviços sem estado).
- `dotnet-skills:slopwatch` — rodar depois de cada commit do FF-02; pega atalhos que mascaram
  quebra de comportamento (try/catch vazio "pra passar", assert removido, etc.) — o tipo de coisa
  fácil de introduzir sem querer num refactor grande.
- `dotnet-skills:csharp-concurrency-patterns` / `dotnet-skills:dotnet-concurrency-specialist` —
  se qualquer extração tocar o locking do `RunManager` (runs concorrentes por conexão).
- `code-review` (skill genérica, `/code-review high` ou `ultra`) — segunda opinião antes de cada
  merge de extração; `/simplify` depois, se sobrar gordura.
- `verify` (skill genérica) — validar manualmente no browser que nenhuma run "sente" diferente
  após cada extração (o replay-hash prova bit-a-bit, mas vale o olho humano também).

**Faltando, vale instalar:**
- **context7 MCP.** O `CLAUDE.md` raiz já instrui "use context7" para consultar Angular/EF
  Core/SignalR/ASP.NET Core, mas o servidor não está conectado nesta máquina — nenhuma tool
  `context7` aparece disponível. Instalar via CLI (fora desta sessão de chat):
  ```
  claude mcp add context7 -- npx -y @upstash/context7-mcp
  ```
  Confirme o pacote/flags atuais na doc oficial (github.com/upstash/context7) antes de rodar,
  caso tenham mudado desde o meu conhecimento. Sem isso, toda consulta de API de biblioteca cai
  pra conhecimento de treino em vez de doc versionada — o `CLAUDE.md` já assume que existe.

**Não é necessário para esta fila:** nada de novo em MCP de browser/computer-use (a decomposição
é 100% backend, sem UI observável) nem conectores de terceiros — o `mcp-registry` deste ambiente
não lista nada relevante para C#/determinismo além do que os agentes `dotnet-skills` já cobrem.

---

## Pausadas (fora da fila ativa — não perder o design)

> Decisão de 2026-07-02: focar em consolidar (FF-01/FF-02) antes de qualquer feature de produto
> nova. As análises abaixo continuam válidas; retomar só depois que o feeling atual dos kits
> tiver sido auditado e o engine estiver decomposto.

### Echo Team — repensar como "Echo Assist" situacional, não companions full-time

A versão original (2 companions IA lutando full-time na mesma arena) **não deveria voltar como
estava desenhada**: numa arena mobada única, tunada para **um** herói orbitar/kitar/cleavar, três
corpos fazendo o mesmo AoE na mesma pilha viram redundância visual ou exigem inflar tanto o spawn
budget que a arena fica ilegível — atacando direto os pilares de legibilidade do HUD/helper já
construídos. Se retomado, a direção certa é algo **situacional**: uma Kaeli da coleção aparece só
num momento específico (ex.: janela de Echo Break, ou invocação via ult) em vez de ocupar espaço
full-time na pilha. Preserva "minha coleção importa" sem brigar com a identidade mobada.

### Auto-tuner de balanceamento (BalanceSim como otimizador)

Modo do BalanceSim que recebe curvas-alvo (hunt time, TTK, win rate) e busca sozinho os valores
de `GameConfig`/role-tuning que as atingem, com estatística honesta (seeds pareadas, intervalos
de confiança, busca que não superajusta ao autopilot). Entregaria os números pendentes do
Berserk global (§2.6 do `kaelis_kit_reformulation.md`) e do retune de barrage (§2.3). Zero risco
de engine (só `tools/`) — bom candidato pra **depois** do FF-02, quando o `GameWorld` decomposto
tornar mais fácil instrumentar métricas por subsistema.

### Co-op online de 2 jogadores

Uma `GameWorld`, dois heróis, dois clientes — possível sem netcode de rollback porque o backend
é autoritativo, mas exige generalizar dezenas de pontos "single-player por construção" (card
offer pausa o relógio, morte "do player", `RunManager` por conexão). A mais arriscada e cara do
repo; **fica pra depois do FF-02** — generalizar "o player" num `GameWorld` já decomposto custa a
metade do que custaria hoje.

### Gerador-avaliador: procgen com crítico automático

Gerar N candidatos de andar → simular com autopilot → pontuar pacing → rejeitar
deterministicamente → publicar o vencedor. Pesquisa aplicada (definir "diversão" computável) mais
infra de simulação em lote. Depende do auto-tuner (acima) e de coordenar com `roadmap_hunts`;
registrada só para não perder a ideia.

---

## Como usar esta fila

1. **FF-01 primeiro.** É o gate mecânico — sem ele, FF-02 (e qualquer refactor futuro do engine)
   não tem como provar que preservou comportamento.
2. **FF-02 em seguida**, um subsistema por commit, `--replay-check` verde a cada passo.
3. Só depois disso retomar: auditoria de feeling kit-a-kit, e então (se ainda fizer sentido) as
   itens da seção `## Pausadas`.
4. Toda sessão: plan mode primeiro; golden + `--replay-check` + builds verdes como gate; marcar
   a feature aqui com ✅ + 1 linha de resumo ao concluir (mesma convenção dos outros roadmaps).
