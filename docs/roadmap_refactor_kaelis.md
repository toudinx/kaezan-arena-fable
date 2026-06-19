# Roadmap — Refatoração das Kaelis

> **Como usar este arquivo.** Cada `K-NN` abaixo é uma unidade de trabalho **auto-contida**: o
> agente que executa começa "frio", então o prompt já traz o contexto que ele precisa. Você dispara
> com **"implemente o prompt K-NN do `docs/roadmap_refactor_kaelis.md`"** e o agente faz o resto.
>
> Cada prompt declara: **Modelo · Effort · Skill · Depende de · Aceite · Verificação.** Execute em
> ordem — há dependências reais (o roster base precede kits, traits e gacha).
>
> **Não confundir com:** `docs/ROADMAP.md` (Codex, tasks pequenas), `docs/FABLE_TRACK.md` (Fable,
> features grandes do engine) e `docs/FRONTEND_REMAP.md` (remap visual). Este arquivo é a
> refundação do roster/kits/gacha das Kaelis e toca **principalmente o backend** (`Domain`, `Engine`,
> `Meta`), mais limpeza pontual de frontend.

---

## Modelos & quando usar

| Modelo | Papel | Effort típico | Por quê |
|---|---|---|---|
| **Claude Code Opus 4.8** | Design de roster, kits autorais, traits, decisão de nome/lore/ID, balance | `high` / `medium` | São decisões de "gosto" de game design + invariantes de engine (determinismo). Errar aqui cascateia. Vale o modelo premium. |
| **GPT-5.5 (Codex)** | Mudanças bounded com regra já fechada: texto de README, gacha provisório, limpeza de UI | `low` / `medium` | Tarefas com regra explícita e padrão a seguir. Barato e rápido. |

- Use **`use context7`** ao consultar API de biblioteca (ASP.NET Core, EF Core, SignalR) nos prompts de backend.
- Os asset packs das 7 Kaelis **já existem** (`idle-1..3`, `thumb`, `banner`, `wallpaper`,
  `bg-landscape`, `bg-portrait`). A skill **`kaeli-asset-prompts`** só é necessária se for **gerar
  arte de uma Kaeli nova** — não é o caso desta trilha. Não invocar à toa.

### Disponibilidade de skill por agente (importante)

Nenhum prompt desta trilha depende de skill instalada. As decisões de design vivem no próprio prompt
(roster alvo, kits propostos, traits sugeridas abaixo) — tanto Opus quanto Codex leem deste arquivo.

---

## Invariantes inegociáveis (todo prompt respeita)

- **Backend autoritativo.** Frontend nunca simula combate/movimento — só interpola e renderiza.
- **Determinismo do engine.** `GameWorld` usa só o `Rng` da run. Nunca `Random`, `DateTime.Now`, ou
  iteração de coleção instável dentro do tick.
- **Todas as constantes de simulação em `Domain/GameConfig.cs`.** Nada de hardcode no tick.
- Skills são **data-driven por shape** (`single`, `beam`, `nova`, `area`, `cone`, `chain`, `ring`,
  `field`, `barrage`, `summon`, `buff`). Para kit novo, parametrize um shape existente — não crie
  dispatch paralelo no engine.
- **IDs estáveis** persistidos merecem respeito (ver "Cuidado Com IDs"); mudança de ID exige migração.
- `dotnet build` (backend) e `npx ng build` (frontend) passam sem erro ao fim de cada prompt que tocar o respectivo lado.

---

## Tese

Kaeli jogável é sempre personagem de topo 5★. Não existem Kaelis 3★/4★ "de preenchimento": quando o
jogo ganha uma Kaeli nova, ela deve chegar com arte completa, lore, kit autoral, trait, afinidade,
skins e lugar real no mundo.

O gacha continua existindo, mas a raridade deixa de significar "personagem menor". Enquanto a
curadoria de itens não está pronta, qualquer roll que antes entregaria uma Kaeli 3★/4★ entrega
provisoriamente **1 item aleatório**. Depois, esse espaço pode virar pool curado de equipamentos,
presentes, shards, skins ou materiais.

## Decisões Fechadas

- Todas as Kaelis jogáveis são **5★**.
- O roster inicial refatorado terá 7 Kaelis com asset definido: Eloa, Seren, Velvet, Rin, Rynna,
  Lunara e Gaia (Earth).
- As Kaelis antigas não precisam ser preservadas como jogáveis. Elas podem virar NPCs, skins,
  memórias ou simplesmente sair durante esta refundação.
- Não criar 4★ só para "valorizar" 5★. A valorização vem do gacha dar item comum na maior parte
  das vezes e Kaeli jogável quando acerta personagem.
- Kits podem e devem ser ajustados/criados por arquétipo, mas continuam usando os shapes
  data-driven existentes (`single`, `beam`, `nova`, `area`, `cone`, `chain`, `ring`, `field`,
  `barrage`, `summon`, `buff`). Não criar um dispatch paralelo no engine.
- Backend continua autoritativo; frontend só renderiza/interpola.
- Constantes de simulação novas entram em `Domain/GameConfig.cs`.

## Roster Alvo

| Kaeli | Elemento | Alcance | Função de fantasia |
|---|---|---:|---|
| **Eloa** | Holy | ranged | anjo/serafim de luz, julgamento e absolvição |
| **Seren** | Physical | melee | cavaleira astral, duelo e disciplina |
| **Velvet** | Death | ranged | pesadelo, maldição, DoT e execução |
| **Rin** | Fire | ranged | súcubus, pacto, charme e burn |
| **Rynna** | Energy | melee | dragoa guerreira de raio, engage e stun |
| **Lunara** | Ice | melee | lebre lunar, mobilidade e slow |
| **Gaia** | Earth | ranged | arqueira da terra, raízes, caça e monólitos (ranger mineral, não druida floral) |

Distribuição inicial: 4 ranged (`Eloa`, `Velvet`, `Rin`, `Gaia`) e 3 melee (`Seren`, `Rynna`,
`Lunara`). A Gaia puxa para arqueira mineral/raízes, dando um ranged físico-místico diferente das
magas puras.

## Assets Autorais

| Pasta atual | Destino proposto | Ação |
|---|---|---|
| `frontend/public/assets/kaelis/kaelis-1` | `eloa` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/astra` | `seren` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/velvet` | `velvet` | manter |
| `frontend/public/assets/kaelis/kaelis-2` | `rin` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/kaelis-3` | `rynna` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/kaelis-4` | `lunara` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/kaelis-5` | `gaia` | renomear pasta após consolidar ID |

Cada pasta nova, incluindo `kaelis-5`, já tem o pacote visual completo: `idle-1..3`, `thumb`,
`banner`, `wallpaper`, `bg-landscape` e `bg-portrait`. O `manifest.json` precisa ser
regenerado/atualizado para todos os IDs finais, porque hoje só registra a Velvet.

## Cuidado Com IDs

O projeto ainda é jovem, então podemos refatorar com mais liberdade, mas IDs persistidos ainda
merecem respeito. Existem duas opções válidas:

1. **Conservadora:** manter IDs antigos quando a personagem é uma substituição direta. Exemplo:
   `waifu:aurora` passa a se chamar Eloa. Isso preserva contas locais sem migração.
2. **Limpa:** usar IDs finais (`waifu:eloa`, `waifu:seren`, etc.) e adicionar sanitização/migração
   para contas existentes. Como o projeto tem menos de uma semana, esta provavelmente é a opção
   melhor se quisermos nomes limpos para sempre.

Decisão recomendada: usar IDs finais e criar uma migração simples no `AccountSanitizer`.

---

## Mapa de prompts (escopo)

| Prompt | Tema | Modelo | Effort | Depende de | Onda |
|---|---|---|---|---|---|
| K-01 | [x] Congelar a nova direção (README) | GPT-5.5 (Codex) | low | — | 1 |
| K-02 | [x] Reescrever o roster base (7× 5★) | Opus 4.8 | high | K-01* | 1 |
| K-03 | [x] Classes/kits autorais por Kaeli | Opus 4.8 | high | K-02 | 2 |
| K-04 | [x] Traits assinatura | Opus 4.8 | high | K-03 | 3 |
| K-05 | [x] Gacha provisório sem 3★/4★ Kaeli | GPT-5.5 (Codex) | medium | K-02 | 2 |
| K-06 | [x] Limpeza de UI e texto | GPT-5.5 (Codex) | medium | K-02, K-05 | 3 |
| K-07 | [x] Balance e verificação | Opus 4.8 | medium | K-02–K-06 | 4 |

> Pratos cheios (K-02, K-03, K-04) no **Opus 4.8 high** — é onde mora a decisão de game design + os
> invariantes de engine. Conversões bounded (K-01, K-05, K-06) no **GPT-5.5 medium/low**.

---

## Execução paralela ⭐ (faça isto em vez de sequencial)

Hoje a trilha é rodada um prompt por vez. Mas o grafo de dependências + análise de conflito de
arquivos permite rodar vários ao mesmo tempo. **Regra de ouro:** dois prompts só rodam em paralelo se
(a) as dependências dos dois já fecharam **e** (b) eles **não editam o mesmo arquivo** (senão dão
conflito de merge). Como Opus e Codex são agentes diferentes, o casamento natural é **1 Opus + 1
Codex por onda**.

```
Onda 1   K-02 (Opus · roster)    ‖  K-01 (Codex · README)
              │                         arquivos disjuntos: backend vs docs
              ▼
Onda 2   K-03 (Opus · kits)      ‖  K-05 (Codex · gacha)
              │                         disjuntos: Classes/Engine vs GachaService/recruit
              ▼
Onda 3   K-04 (Opus · traits)    ‖  K-06 (Codex · UI/texto)
              │                         disjuntos: Waifus/Engine vs kaelis/recruit/types
              ▼
Onda 4   K-07 (verificação final, solo)
```

**Conflitos que forçam sequencial (não paralelize estes pares):**
- **K-03 × K-04** — ambos mexem em `Engine/GameWorld.cs` + `Domain/GameConfig.cs`. Traits assentam
  sobre os kits; rode K-04 depois de K-03.
- **K-05 × K-06** — ambos mexem em `pages/recruit/recruit.ts`. Rode K-06 depois de K-05.

**Ganho:** 7 passos sequenciais → **4 ondas**. Com um agente Opus e um Codex rodando juntos por
onda, o caminho crítico vira K-02 → K-03 → K-04 → K-07 (4 prompts), e K-01/K-05/K-06 "saem de graça"
em paralelo.

> *K-02 lista K-01 como dependência por convenção (congelar a direção primeiro), mas a direção já
> está **inteira neste documento** — então K-02 e K-01 podem rodar em paralelo sem risco (arquivos
> disjuntos). O `*` na tabela marca essa dependência "branda".

---

# [x] K-01 — Congelar a Nova Direção

Resumo: README atualizado para declarar Kaelis jogáveis como premium 5★ e rolls não-Kaeli como 1 item aleatório provisório.

- **Modelo:** GPT-5.5 (Codex) · **Effort:** low · **Skill:** nenhuma · **Depende de:** — · **Paraleliza com:** K-02 (Onda 1)

**Objetivo:** registrar no `README.md` a tese "Kaelis jogáveis são sempre 5★" e abandonar a noção de
3★/4★ jogáveis. Tarefa só de texto/documentação — **não muda gameplay**.

**Arquivos prováveis:** `README.md`, este documento.

**Tarefas:**
- Descrever no README o modelo novo sem falar em roster 3/4/5★.
- Deixar claro que rolls não-Kaeli dão **1 item aleatório** por enquanto.

**Aceite:**
- README descreve o modelo novo sem falar em roster 3/4/5★.
- O texto deixa claro que rolls não-Kaeli dão item aleatório por enquanto.
- Não há mudança de gameplay.

**Verificação:** revisão de texto; nenhum build necessário (só docs).

---

# [x] K-02 — Reescrever o Roster Base  ⭐ fundação

Resumo: roster agora são 7 Kaelis 5★ (Eloa, Seren, Velvet, Rin, Rynna, Lunara, Gaia) com IDs
finais `waifu:*`, lore/personalidade/presentes/skins provisórios e traits provisórias (kinds já
suportados pelo engine; K-04 reescreve). Cada Kaeli aponta para a classe existente que casa o
elemento (kits autorais ficam para K-03). Pastas de asset renomeadas para os slugs finais e
`manifest.json` cobre as 7. AccountSanitizer migra contas antigas (IDs removidos → refund + starter).
Starter = `waifu:seren`. `dotnet build` e `npx ng build` limpos.

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ APIs) · **Depende de:** K-01* (branda) · **Paraleliza com:** K-01 (Onda 1)

**Objetivo:** substituir o roster atual pelas 7 Kaelis — Eloa, Seren, Velvet, Rin, Rynna, Lunara e
Gaia — todas 5★. Esta é a fundação — define os IDs e a forma que K-03..K-07 assumem.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Domain/Waifus.cs`,
`backend/src/KaezanArenaFable.Api/Meta/AccountSanitizer.cs`,
`frontend/public/assets/kaelis/manifest.json`.

**Tarefas:**
- Criar/ajustar `WaifuDef` para as 7 Kaelis (todas 5★).
- Remover Kaelis antigas do pool jogável.
- Definir descriptions, personalities, 4 ecos de memória e presentes favoritos provisórios.
- Atualizar `StarterWaifuId` (sugestão: `waifu:seren` p/ começo melee simples, ou `waifu:eloa` p/
  apresentação mais premium).
- Atualizar `FeaturedFiveStarId` para o banner inicial.
- Renomear as pastas de asset para os slugs finais e atualizar `manifest.json` com as artes das 7.
- Adotar IDs finais (`waifu:eloa`, `waifu:seren`, ...) e sanitizar contas locais no
  `AccountSanitizer`: trocar Kaelis removidas por compensação ou pelo starter.

**Aceite:**
- Catálogo mostra só as 7 Kaelis 5★.
- Página Kaelis renderiza arte autoral para todas (todas as pastas renomeadas para os slugs finais).
- Fecha a matriz elemental: Holy, Physical, Death, Fire, Energy, Ice, Earth.
- Conta nova inicia com uma Kaeli válida.
- Contas antigas não quebram ao carregar.

**Verificação:** `dotnet build` + `npx ng build` limpos. Subir e confirmar no preview que o roster
mostra as 7 com arte (nenhuma cai no placeholder por falta de manifest).

---

# [x] K-03 — Definir Classes/Kits Autorais por Kaeli  ⭐ identidade de combate

Resumo: as 7 classes do roster ganharam kits autorais (1 por Kaeli) seguindo a tabela proposta,
ainda data-driven por shape — zero mudança no engine. Cada classe foi renomeada para a fantasia da
dona (Serafim/Eloa, Cavaleira Astral/Seren, Arauto do Pesadelo/Velvet, Súcubus do Pacto/Rin, Dragoa
do Trovão/Rynna, Bailarina Lunar/Lunara, Arqueira dos Monólitos/Gaia) e os skill ids passaram a usar
o namespace da Kaeli (`skill:eloa:*`, `skill:seren:*`, …). IDs de classe ficaram estáveis (não são
persistidos por conta) para não tocar Waifus.cs nem o mapa arma→classe do ItemAuthoring; Sentinel e
Barbarian seguem como classes de reserva. Kits antigos órfãos removidos. `dotnet build` limpo.

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ APIs) · **Depende de:** K-02 · **Paraleliza com:** K-05 (Onda 2) — ⚠ **não** com K-04 (conflito em `GameWorld.cs`/`GameConfig.cs`)

**Objetivo:** parar de depender de classes genéricas quando elas não servem à fantasia. Cada Kaeli
5★ ganha um arquétipo claro, **ainda data-driven por shape** (não criar dispatch paralelo no engine).

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Domain/Classes.cs`,
`backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`,
`backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` **somente** se algum shape existente precisar
de suporte já previsto.

**Kits propostos:**

| Kaeli | Arquétipo | Slots sugeridos |
|---|---|---|
| Eloa | Holy ranged | `single` lança de luz, `barrage` julgamento, `beam` raio sacro, `ring` halo, ult `nova` absolvição |
| Seren | Physical melee | `single` corte preciso, `chain` avanço entre alvos, `cone` arco de espada, `buff` postura, ult `nova` zênite |
| Velvet | Death ranged | `single` death strike, `area` maldição/DoT, `beam` pesadelo, `summon` sombra, ult `nova` praga |
| Rin | Fire ranged | `single` beijo de brasa, `chain` contrato ardente, `field` salão em chamas, `cone` asas de cinza, ult `barrage` baile infernal |
| Rynna | Energy melee | `single` garra elétrica, `cone` cauda trovejante, `chain` descarga curta, `buff` escama condutora, ult `nova` coração da tempestade |
| Lunara | Ice melee | `single` corte lunar, `chain` saltos de geada, `field` jardim congelado, `ring` crescente, ult `nova` lua nova |
| Gaia | Earth ranged | `single` flecha mineral, `area` queda de monólito, `field` raízes aprisionantes, `cone` estilhaços de pedra, ult `barrage` chuva tectônica |

**Aceite:**
- Cada Kaeli joga de forma diferente.
- Nenhuma Kaeli nova é apenas "elemento trocado" de outra.
- Cooldowns, dano, range e FX estão em dados/config, não hardcoded no tick.
- `dotnet build` passa.

**Verificação:** `dotnet build` limpo. Rodar uma run e confirmar que os shapes disparam sem exceção
e respeitam o `Rng` da run (determinismo).

---

# [x] K-04 — Traits Assinatura

Resumo: as 7 Kaelis ganharam passivas assinatura de arquétipos distintos — `judgment` (Eloa,
marca+detona), `discipline` (Seren, combo+crit garantido), `decay` (Velvet, stacks→limiar de
execução), `contagion` (Rin, burn que se propaga + cura), `static_charge` (Rynna, barra→descarga
que paralisa, mantém o overcharge de gauge), `shatter` (Lunara, bônus vs lento + haste + estilhaço)
e `prey` (Gaia, marca de presa com ramp por tempo de caça e salto na morte). Tudo data-driven pela
`TraitDef` (Value/Param × `_traitMult`) + constantes em `GameConfig.cs`; estado por-alvo no `Actor`
e por-Kaeli no `GameWorld`, com hooks pré/pós-dano e on-kill determinísticos (só `Rng`/`NowMs`,
desempate por id, guarda `Killed` contra dupla morte). Estado vivo exposto no HUD (chip da passiva
em `game.ts` + marcas por-alvo no `renderer.ts`) e descrições novas no dossiê. `dotnet build` e
`npx ng build` limpos.

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ APIs) · **Depende de:** K-03 · **Paraleliza com:** K-06 (Onda 3) — ⚠ **não** com K-03 (conflito em `GameWorld.cs`/`GameConfig.cs`)

**Objetivo:** dar a cada Kaeli uma **passiva assinatura única** que crie um "mini-game" dentro da
run — algo que o jogador acompanha e em que toma decisões (em quem focar, quando estourar stacks,
como posicionar), não um bônus passivo invisível. **Cada uma usa um arquétipo diferente** — nenhuma
é "+X% de dano contra Y". Referência de altitude: passivas de campeão de MOBA (League of Legends,
etc.), adaptadas pro combate autobatalha do Kaezan.

**Princípios (todos obrigatórios):**
- **Única por arquétipo.** As 7 passivas abaixo são deliberadamente de famílias diferentes
  (marca+detonar, combo, stacks+execução, contágio, barra de carga, condicional+reposição,
  presa+reset). Não converta duas pra mesma mecânica.
- **Cria decisão de gameplay.** A passiva deve mudar como o jogador joga aquela Kaeli (foco vs
  espalhar, paciência vs burst, hit-and-run, etc.).
- **Determinística.** Só o `Rng` da run; nada de `Random`/`DateTime.Now`. Contadores, marcas,
  limiares e seleção de alvo têm de ser estáveis (desempate por id, não por ordem de coleção).
- **Evitar passiva nicho.** Nada de "dano extra contra undead" ou condições que quase nunca
  acontecem numa run.
- **Constantes de simulação em `GameConfig.cs`** (stacks, limiares, %, durações, raios).

**Seam existente (REUTILIZE, não reinvente).** O engine já tem um sistema de trait:
- `TraitDef(Id, Name, Kind, double Value, double Param, string Tag, Description)` em
  [Waifus.cs](backend/src/KaezanArenaFable.Api/Domain/Waifus.cs:13). Só **2 números** (`Value`,
  `Param`) + `Tag`. Use `Value`/`Param` para os tunáveis principais (eles são amplificados pela
  maestria); todo o resto (stacks, limiares, durações, raios, % de cura) vai em `GameConfig.cs`.
- Kinds já implementados em [GameWorld.cs](backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs):
  `executioner` (dano vs HP baixo, `:1499`), `slayer` (vs classe de bestiário, `:1501`), `deadeye`
  (crit por distância, `:1494`), `chiller` (slow no gelo, `:1556`), `skill_lifesteal` (`:1554`),
  `overcharge` (bônus de gauge, `:263`), `pack_hunter` (`:695`), `fortress`/`bulwark` (redução de
  dano, `:2266`). As traits atuais do roster usam esses kinds e são **provisórias** — é o que o K-04
  reescreve.
- **Maestria amplifica a trait** via `_traitMult` (ramo Eco). Toda passiva nova tem de continuar
  respeitando `_traitMult` no efeito principal, senão o ramo de maestria fica morto.

**O que reaproveita vs. o que é kind novo:**
- *Estendem um kind existente:* **Velvet** (`executioner` + escala por stacks), **Rynna**
  (`overcharge` p/ a sinergia de ult), **Lunara** (`chiller` já aplica o slow; falta o bônus/haste/
  shatter), **Gaia** (`deadeye` vira a marca de presa).
- *Kind novo + estado no tick:* **Eloa** (marca+detonar), **Seren** (combo cadence), **Rin**
  (contágio). Esses precisam de estado por-alvo/por-Kaeli no `GameWorld` + handling no tick.

**Arquivos prováveis:** `Domain/Waifus.cs` (nova `TraitDef` por Kaeli + descrição), `Engine/GameWorld.cs`
(novos kinds + estado por-alvo/por-Kaeli + efeito no tick, preservando `_traitMult`),
`Domain/GameConfig.cs` (constantes), HUD de batalha (`pages/game/game.ts`) p/ exibir o estado vivo.

**Passivas assinatura (uma por Kaeli):**

| Kaeli | Passiva | Mecânica | Mini-game / decisão |
|---|---|---|---|
| **Eloa** (Holy) | **Selo de Julgamento** | habilidades aplicam stacks de *Pecado* no alvo; ao chegar a N (ex. 3) o alvo é *Julgado* — o próximo acerto consome os stacks num burst sacro em área pequena e cura Eloa/aliado por uma fração do dano | espalhar marcas em vários alvos (sustain/AoE) **vs** focar um pra detonar rápido (burst) |
| **Seren** (Physical) | **Disciplina** | acertos consecutivos no **mesmo** alvo escalam o dano em degraus (ex. +8%/acerto até +40%); trocar de alvo ou ficar Xs sem bater zera. Cada 3º acerto vira *Corte Perfeito* (crit garantido, determinístico) | comprometer-se num duelo single-target **vs** limpar adds (perde o ramp) |
| **Velvet** (Death) | **Maldição Acumulada** | cada habilidade aplica um stack de *Decadência* (DoT empilhável); o **limiar de execução** de Velvet escala com os stacks no alvo (ex. executa <15%, +2%/stack até <25%) | empilhar maldição com paciência e então **executar** — quanto mais investiu, mais cedo o alvo estoura |
| **Rin** (Fire) | **Contágio** | quando um inimigo *queimando* morre (ou a cada Xs), o burn salta pro inimigo não-queimando mais próximo, espalhando o incêndio; Rin curativa uma fração leve do dano de burn (pacto) | gerenciar um incêndio que se propaga — posicionar pra encadear queimadura num grupo; mais alvos queimando = mais sustain |
| **Rynna** (Energy) | **Carga Estática** | acertos enchem uma barra de *Carga*; cheia, o próximo ataque vira *Descarga* (chain curto + paralyze breve). Aplicar paralyze acelera a recarga da ultimate | ritmar os acertos pra liberar a descarga no pico de valor (vários alvos / antes do engage com a ult) |
| **Lunara** (Ice) | **Estilhaçar** | atacar alvo *lento/congelado* dá dano bônus de gelo + concede a Lunara haste breve; bater de novo no lento *estilhaça* (consome o slow) num burst | hit-and-run: aplicar slow, mergulhar, bater com haste, reposicionar — recompensa mobilidade |
| **Gaia** (Earth) | **Presa** | marca um alvo como *Presa*; o dano de Gaia contra a Presa cresce quanto mais a caça dura, e suas raízes (`field`) renovam/estendem a marca. Quando a Presa morre, a marca salta pro próximo alvo (a caça continua) e Gaia ganha bônus breve | escolher e perseguir o alvo-prioridade; prender com raízes pra maximizar o ramp; cadeia de execuções |

**Valores iniciais sugeridos (provisórios — vão pra `GameConfig.cs`).** Ancorados na escala atual do
jogo, pra começarem "no campo certo" e o K-07 só afinar (referências reais: crit 5%/×1.5; buffs de
dano existentes 1.10–1.20×; DoT a cada 2000ms, máx 10 ticks; slow floor 0.40 / dur cap 6000ms; haste
cap 1.5× / dur 5000ms; marca elemental 4000ms; gauge máx 100, +8/kill):

| Kaeli | `Value` / `Param` da `TraitDef` | Constantes em `GameConfig.cs` |
|---|---|---|
| **Eloa** | `Value`=1.2 (burst = 120% do acerto-gatilho) · `Param`=0.25 (cura = 25% do burst) | stacks p/ julgar **3**, raio do burst **1**, duração da marca **4000ms** |
| **Seren** | `Value`=0.08 (+8%/acerto) · `Param`=0.40 (cap +40%) | reset após **3000ms** sem bater / troca de alvo; *Corte Perfeito* a cada **3º** acerto (= ×1.5) |
| **Velvet** | `Value`=0.25 (mantém o bônus de `executioner`) · `Param`=0.15 (limiar base 15%) | +**2%**/stack até **25%**; DoT por stack pequeno, tick **2000ms**, cap **5** stacks |
| **Rin** | `Value`=0.06 (lifesteal do burn = 6%) · `Param`=3 (raio do salto) | intervalo de salto **2000ms**; salta p/ não-queimando mais próximo |
| **Rynna** | `Value`=0.30 (mantém o bônus de `overcharge`) · `Param`=0 | carga **+20**/acerto (5 enchem 100); descarga ~**150%** + chain **2–3** alvos + paralyze **800ms**; paralyze dá **+8** de gauge |
| **Lunara** | `Value`=0.25 (bônus vs alvo lento) · `Param`=2000 (mantém dur. do slow do `chiller`) | haste **1.2×** por **2000ms**; *shatter* no **3º** acerto no lento (~**150%**) |
| **Gaia** | `Value`=0.05 (ramp +5%/s vs Presa) · `Param`=0.30 (cap +30%) | salto da marca ao morrer; bônus de caça **+20%** atk speed por **3000ms** |

> Os números acima são ponto de partida, não lei — o agente afina e **tudo de simulação fica em
> `GameConfig.cs`**. Regras de seleção de alvo (próxima presa, alvo do contágio) devem ser
> determinísticas: ex. menor distância e, no empate, menor id estável.

**Aceite:**
- Cada Kaeli tem **uma** passiva assinatura de arquétipo distinto — nenhuma repete a mecânica de outra.
- A passiva muda a forma de jogar a Kaeli (há uma decisão real associada).
- O **estado vivo** da passiva (stacks/carga/marca/presa) é visível no HUD de batalha; a descrição
  aparece no dossiê da Kaeli.
- Engine determinístico (só `Rng` da run); seleção de alvo estável.
- Constantes em `GameConfig.cs`.

**Verificação:** `dotnet build` + `npx ng build` limpos. Rodar a mesma run/seed duas vezes e confirmar
resultado idêntico (determinismo). Confirmar no preview que o estado da passiva aparece e evolui
durante a run (ex. stacks de Pecado da Eloa subindo, barra de Carga da Rynna enchendo).

---

# [x] K-05 — Gacha Provisório Sem 3★/4★ Kaeli

Resumo: gacha agora só entrega Kaeli em resultado 5★; demais rolls concedem 1 item aleatório real na Mochila, com reveal misto de Kaeli/item.

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** K-02 · **Paraleliza com:** K-03 (Onda 2) — ⚠ **não** com K-06 (conflito em `recruit.ts`)

**Objetivo:** remover Kaelis 3★/4★ do resultado do gacha. Enquanto não existe curadoria de itens,
roll não-Kaeli entrega **1 item aleatório**. Regra fechada — tarefa bounded.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Meta/GachaService.cs`,
`backend/src/KaezanArenaFable.Api/Meta/AccountState.cs`,
`frontend/src/app/pages/recruit/recruit.ts`.

**Regras provisórias:**
- 5★ continua sendo chance de Kaeli.
- Resultado não-5★ vira item aleatório com quantidade 1.
- Pity de 5★ continua funcionando.
- Pity/garantia de 4★ deve ser removido, ignorado ou convertido para "item raro" depois.
- UI do reveal precisa mostrar item e sprite quando o resultado não for Kaeli.

**Aceite:**
- Pull nunca entrega Kaeli 3★/4★.
- Pull não-5★ adiciona 1 item real ao inventário.
- Pull 5★ entrega Kaeli ou dupe convertido conforme regra existente.
- O frontend não quebra ao revelar resultados mistos de Kaeli/item.

**Verificação:** `dotnet build` + `npx ng build` limpos. Fazer um 10-pull no preview e confirmar item
aleatório no inventário + eventual Kaeli, sem erro de console.

---

# [x] K-06 — Limpeza De UI E Texto

Resumo: telas de Kaelis/recruit agora tratam o elenco como Kaelis jogaveis e o reveal separa item comum de Kaeli.

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** K-02, K-05 · **Paraleliza com:** K-04 (Onda 3) — ⚠ **não** com K-05 (conflito em `recruit.ts`)

**Objetivo:** remover linguagem antiga de raridade baixa nas telas de Kaelis/recruit. Tarefa bounded
de frontend — preservar a lógica.

**Arquivos prováveis:** `frontend/src/app/pages/kaelis/kaelis.ts`,
`frontend/src/app/pages/recruit/recruit.ts`, `frontend/src/app/core/types.ts`.

**Aceite:**
- UI não vende 3★/4★ como personagens jogáveis.
- Filtros, chips e textos de roster refletem "Kaelis jogaveis".
- Reveal distingue claramente item comum vs Kaeli.

**Verificação:** `npx ng build` limpo + screenshots de Kaelis/recruit no preview + console limpo.

---

# [x] K-07 — Balance E Verificação

Resumo: trilha das Kaelis verificada de ponta a ponta. `dotnet build` e `npx ng build` limpos
(só os warnings pré-existentes de budget de CSS no front). Roster, kits e traits revisados por
número — sem dominador óbvio: básicos ~1.15–1.35 de power, ults nova 2.50–2.70 vs barrage ~4.4
diluído, melee mais HP (205–240) e ranged menos (150–170), traits dentro das âncoras do K-04. As 7
passivas assinatura estão de fato ligadas no engine (`judgment/discipline/decay/contagion/
static_charge/shatter/prey`) e respeitam `_traitMult`. Verificação em preview: as 7 Kaelis 5★
renderizam arte autoral (nenhum placeholder); run melee (Seren, "Disciplina", HP 240) e wand-caster
(Velvet, "Maldição Acumulada", sombra invocada ativa) sobem sem erro; run da Gaia (ranged/Terra,
fecha a matriz) matou mobs, subiu de nível e a "Presa" rampou até o cap +30% no HUD; 10-pull entregou
só itens comuns na Mochila (sem Kaeli 3★/4★), reveal correto. Console limpo em todas as telas.

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** K-02–K-06 · **Paraleliza com:** — (solo, Onda 4 — verificação precisa de tudo fechado)

**Objetivo:** garantir que o novo roster passa por uma run real e que nenhum kit domina por erro
óbvio de número. Exige julgamento de balance, por isso fica no modelo premium.

**Verificação mínima:**
- `dotnet build`.
- `npx ng build`.
- Testar pelo menos uma run tier 1 com Seren ou Lunara.
- Testar uma run com uma ranged (Eloa, Rin ou Velvet).
- Testar uma run com a Gaia (fecha a matriz elemental).
- Fazer 10-pull e confirmar item aleatório + eventual Kaeli.

**Aceite:**
- Builds verdes.
- Run inicia com cada Kaeli do roster.
- Gacha adiciona itens ao inventário sem erro.
- Nenhum asset autoral cai no placeholder por falta de manifest.

---

## Depois

- Curadoria real do pool de itens do gacha.
- Armas assinatura por Kaeli.
- Skins premium/afinidade.
- Banner por personagem com história curta.
- Eventual segunda Kaeli 5★ por elemento para variar melee/ranged sem criar personagens menores.
