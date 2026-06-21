# Roadmap — Meta-Gameplay: Papéis (Knight · Mage · Archer) e Balanceamento

> **Como usar este arquivo.** Cada `MG-NN` abaixo é uma unidade de trabalho **auto-contida**: o
> agente que executa começa "frio", então o prompt já traz o contexto que ele precisa. Você dispara
> com **"implemente o prompt MG-NN do `docs/roadmap_meta_gameplay.md`"** e o agente faz o resto.
>
> Cada prompt declara: **Modelo · Effort · Skill · Depende de · Aceite · Verificação.** Execute em
> ordem — há dependências reais (o simulador precede tudo; o modelo de papéis precede o tuning).
>
> **Não confundir com:** `docs/roadmap_refactor_kaelis.md` (refundação do roster — **já concluída**,
> é a base disto), `docs/ROADMAP.md` (Codex, tasks pequenas) e `docs/FABLE_TRACK.md` (features
> grandes de engine). Este arquivo é a **identidade de papel + balanceamento por número** e toca
> **principalmente o backend** (`Domain`, `Engine`, `Content`, `Api`), mais um editor no admin e o
> novo `tools/BalanceSim`.

---

## Modelos & quando usar

| Modelo | Papel | Effort típico | Por quê |
|---|---|---|---|
| **Claude Code Opus 4.8** | Modelo de papéis, split auto/skill, conversão da Lunara, tuning por número, calibração de mobs/itens/cartas | `high` / `medium` | É decisão de game design + invariantes de engine (determinismo). Errar cascateia em todo o balanceamento. |
| **GPT-5.5 (Codex)** | Mudanças bounded com regra fechada: endpoints CRUD do admin seguindo padrão existente, página de editor no front, texto/README | `low` / `medium` | Tarefas com padrão a seguir (ContentStore + `/admin/content/*` + página admin já existem). Barato e rápido. |

- Use **`use context7`** ao consultar API de biblioteca (ASP.NET Core, SignalR, Angular) nos prompts.
- O simulador (MG-01) é o **backbone de medição**: todo prompt de tuning re-roda o sweep e compara com a baseline. Nada de balancear no olho.

---

## Invariantes inegociáveis (todo prompt respeita)

- **Backend autoritativo.** Frontend nunca simula combate/movimento — só interpola e renderiza.
- **Determinismo do engine.** `GameWorld` usa só o `Rng` da run. Nunca `Random`, `DateTime.Now`,
  `Guid.NewGuid()` ou iteração de coleção instável dentro do tick. Desempate sempre por id estável.
- **Todas as constantes de simulação em `Domain/GameConfig.cs`.** Nada de hardcode no tick.
- Skills são **data-driven por shape** (`single`, `beam`, `nova`, `area`, `cone`, `chain`, `ring`,
  `field`, `barrage`, `summon`, `buff`). Para ajustar um kit, mexa em dado/config — não crie dispatch paralelo.
- **IDs estáveis** (`waifu:*`, `card:*`, `monster:*`, ids de skill/classe): não renomear — quebra
  persistência de conta. Migre dado, não renomeie ID.
- `dotnet build` (backend) e `npx ng build` (frontend) passam sem erro ao fim de cada prompt que tocar o respectivo lado.

---

## Tese

A refundação do roster (`roadmap_refactor_kaelis.md`) deu a cada Kaeli arte, lore, kit e trait —
mas **todas jogam como maga**: auto-attack e skills usam o mesmo `PlayerAttack()`, a velocidade de
auto é uma constante global única e não existe identidade mecânica de papel. Além disso o jogo
quebra em dois pontos de balance: AOEs gigantescas e **hit-kill com set completo** (boss morre rápido demais).

Esta trilha resolve isso introduzindo **papel** como o eixo primário de identidade e recalibrando
mobs/itens/cartas por número, com um simulador headless medindo tudo. A graça herdada do Tibia tem
de continuar viva: **maga limpando sala com AOE** e **knight "fechando box"** no endgame devem ser
gostosos — sem nunca virar hit-kill trivial.

## Decisões Fechadas

- **O eixo primário de design é o PAPEL: Knight · Mage · Archer.** A velha dicotomia melee/ranged
  **morre** como conceito de design — papel passa a dirigir dano, velocidade, range e AOE. O campo
  `Weapon` da Kaeli vira só cosmético (sprite/missile/visual de auto).
- **Mapa de papéis:**
  - **Mage** — Eloa, Velvet, Rin
  - **Archer** — Gaia, **Lunara**
  - **Knight** — Rynna, Seren
- **Lunara converte de melee para arqueira de gelo** (Weapon → `bow`, kit puxa para single-target
  ranged com algum AOE). Mantém id `waifu:lunara`, trait `shatter` e elemento `ice`.
- **Identidade por papel (ordens-alvo):**
  - auto-attack **dano**: archer / knight **>** mage
  - **skill** dano: mage **>** archer **>** knight
  - **attack speed**: archer **>** knight **>** mage
  - auto-attack **range**: archer **>** mage **>** knight
  - **AOE** (tamanho/cobertura): mage **>** knight **>** archer
  - Fantasia: mage = forte AOE mas com single-target (skill+auto) p/ boss; archer = single-target + algum AOE p/ limpar sala; knight = equilíbrio, AOE curta.
- **Alvos de TTK** (gear×mob no **mesmo tier**, medido em **ciclos de ação** = autos+skills): comum
  **~3**, elite **~6**, boss **~12** (aproximado, refinado pelo sim). **Restrição dura: sem hit-kill;
  boss nunca morre em < 8 ciclos.**
- **A tabela de tuning por papel é editável pela tela admin** (runtime, sem recompilar): persistida
  no `ContentStore`, seedada dos defaults em `GameConfig`, exposta em `/admin/content/*` e editada
  numa página em `frontend/src/app/pages/admin/`.
- **As 4 alavancas em escopo:** identidade por papel, tamanho de AOE, HP/dano de mob (5 tiers), escala de item/carta.
- Backend continua autoritativo; constantes novas em `GameConfig.cs`; builds verdes.

## O modelo de papéis (referência técnica)

`PlayerAttack()` ([GameWorld.cs:809](backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs)) hoje é
compartilhado por auto (linha ~1394) **e** skills (linha ~1443) e termina em `* PlayerDamageMult`.
A separação se faz **nos call sites** (não dentro de `PlayerAttack()`, ou aplica duas vezes):

| Eixo | Onde aplica | Como |
|---|---|---|
| Dano de auto | auto-hit (`~1394`) | `PlayerAttack() * RoleAutoMult()` |
| Dano de skill | skill-dmg (`~1443`) + procs de trait/echo/carta | `PlayerAttack() * RoleSkillMult()` |
| Velocidade de auto | `AutoAttackInterval()` (`~839`) | base por papel no lugar do global `PlayerAutoAttackMs` |
| Range de auto | `CanPlayerAutoAttack` / targeting | `AutoRangeOverride ?? WeaponRange(Weapon)` |
| Tamanho de AOE | emissão de `area/nova/cone/ring/barrage` | `ScaledRadius(skill.Radius)` por papel |

**Valores SEED da tabela de tuning** (a serem refinados por MG-06/MG-07 via simulador; entram em
`GameConfig` como defaults e viram editáveis no admin em MG-05):

| Role | AutoDmgMult | SkillDmgMult | BaseAutoAttackMs | AutoRange | AoeScale |
|---|---:|---:|---:|---:|---:|
| Mage | 0.75 | 1.15 | 2000 | 4 | 1.00 |
| Archer | 1.15 | 0.95 | 1400 | 5 | 0.65 |
| Knight | 1.05 | 0.80 | 1700 | 1 | 0.80 |

---

## Mapa de prompts (escopo)

| Prompt | Tema | Modelo | Effort | Depende de | Onda |
|---|---|---|---|---|---|
| MG-01 | Simulador headless (`tools/BalanceSim`) + baseline | Opus 4.8 | high | — | 1 |
| MG-02 | Modelo de papéis (enum, RoleTuning, split auto/skill, interval, range) | Opus 4.8 | high | MG-01 | 2 |
| MG-03 | Conversão da Lunara → arqueira de gelo | Opus 4.8 | medium | MG-02 | 3 |
| MG-04 | Resize de AOE (`ScaledRadius`, rounding, cap de ult) | Opus 4.8 | medium | MG-02 | 3 |
| MG-05 | Tuning por papel editável no admin (ContentStore + endpoints + página) | GPT-5.5 (Codex) | medium | MG-02 | 3 |
| MG-06 | Tuning por-Kaeli (paridade intra-papel) | Opus 4.8 | high | MG-03, MG-04 | 4 |
| MG-07 | Normalização entre papéis (hunt time + dano dado/sofrido) | Opus 4.8 | high | MG-06 | 5 |
| MG-08 | Calibração de mobs/itens/cartas (5 tiers, TTK, anti one-shot) | Opus 4.8 | high | MG-06 | 5 |
| MG-09 | Verificação final + docs | Opus 4.8 | medium | MG-02–MG-08 | 6 |

---

## Execução paralela ⭐

**Regra de ouro:** dois prompts só rodam em paralelo se (a) as dependências fecharam **e** (b) não
editam o mesmo arquivo. Casamento natural: **1 Opus + 1 Codex por onda**.

```
Onda 1   MG-01 (Opus · simulador, solo)            → captura BASELINE
              ▼
Onda 2   MG-02 (Opus · modelo de papéis, solo)     → toca GameWorld/GameConfig/Waifus
              ▼
Onda 3   MG-03 (Opus · Lunara, Waifus/Classes) ‖ MG-05 (Codex · admin: Content/Api/front)
         MG-04 (Opus · AOE) entra após MG-03  ⚠ MG-04 × MG-03/MG-06 conflitam em GameWorld
              ▼
Onda 4   MG-06 (Opus · tuning por-Kaeli, solo)
              ▼
Onda 5   MG-07 (Opus · normalização entre papéis) → depois MG-08 (mobs/itens/cartas)
              ▼                                       ⚠ MG-07 × MG-08 mexem em GameConfig — sequencial
Onda 6   MG-09 (verificação final, solo)
```

**Conflitos que forçam sequencial:**
- **MG-02 × MG-03 × MG-04 × MG-06** — todos tocam `Engine/GameWorld.cs` e/ou `Domain/GameConfig.cs`. Rode em ordem.
- **MG-07 × MG-08** — ambos ajustam constantes em `GameConfig.cs`. Sequencial.
- **MG-05** é disjunto (Content/Api/frontend admin) — pode rodar em paralelo com MG-03/MG-04.

---

# MG-01 — Simulador Headless (`tools/BalanceSim`)  ⭐ backbone de medição

- **Modelo:** Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7` p/ IHostEnvironment) · **Depende de:** — · **Paraleliza com:** — (solo, Onda 1)
- **[x] Concluído.** `tools/BalanceSim` (console net8 + `ProjectReference` ao Api) roda o sweep 7×5×N com piloto-automático, mede TTK (ciclos)/hunt/dano/one-shots do snapshot, imprime pivô + resumo, canário de determinismo PASS, e gerou `docs/balance/baseline.csv` (1750 runs). Único toque no engine: accessor read-only `GameWorld.MonsterRank`. Baseline confirma o problema atual (boss morre em <1–3 ciclos, milhares de one-shots).

**Objetivo:** dar ao balanceamento um instrumento de medição. O engine é determinístico e já tem
piloto-automático completo (`TickAutoHelper`: auto-target, auto-skill, auto-ult, auto-move, auto-nav
até a saída). Um console que constrói `GameWorld`, liga o piloto e tica até a run acabar mede TTK,
tempo de hunt e dano por Kaeli — sem hub, sem frontend, sem DB.

**Contexto técnico (confira antes de codar):**
- Ctor: `GameWorld(long seed, DungeonTier tier, WaifuDef waifu, int ascension, GameData data, MonsterRegistry monsterRegistry, IReadOnlyDictionary<string,long> bestiaryKills, EquipmentStats? equipmentStats=null, KaeliLoadout? loadout=null, ItemRegistry? items=null, string? helperProfile=null)`.
- `GameData(IWebHostEnvironment env)` lê `Data/monsters.json` + `items.json` de `env.ContentRootPath` → aponte para `backend/src/KaezanArenaFable.Api`.
- Piloto: `world.Enqueue(new Command(CommandKind.ToggleAutoHelper, A: 1|2|4|8|16, B: 1, S: "nearest|loot|50"))`, depois loop `var (snap, map) = world.Tick();` até `snap.Run.Ended != null`.
- Tick = 100ms (`GameConfig.TickMs`). Sem conceito de "turno" — converta ms→ciclos dividindo pelo `AutoAttackInterval` da Kaeli.
- Padrão de projeto standalone: `tools/AssetExtractor` (console que referencia o Api). Não há `.sln`.

**Tarefas:**
- Criar `tools/BalanceSim/BalanceSim.csproj` (net8.0, exe, nullable + implicit usings) com
  `ProjectReference` p/ `../../backend/src/KaezanArenaFable.Api/KaezanArenaFable.Api.csproj` e
  `FrameworkReference Microsoft.AspNetCore.App`.
- Stub `SimHostEnvironment : IWebHostEnvironment` (ContentRootPath resolvido por `--content-root` ou subindo de `AppContext.BaseDirectory`).
- `Program.cs`: sweep `{7 Kaelis} × {5 tiers} × {N seeds}` (N default 50, `--seeds`). Para cada
  run: ligar piloto, ticar até acabar, coletar métricas. Gear = recomendado do mesmo tier (via
  `ItemAuthoring`/`EquipmentStatAggregator.Aggregate`), opção `--cards full|none`.
- **Métricas** (lidas do snapshot por tick — evite hooks no engine p/ não quebrar determinismo; se o
  HP por-monstro não estiver no snapshot, adicione só um accessor read-only): TTK por monstro
  (ticks → ms → ciclos), tempo de hunt, dano dado/sofrido por Kaeli, deaths, **flag de one-shot**
  (qualquer hit único ≥ HP atual do alvo).
- Saída: CSV (`--out`, uma linha por kill + resumo por run) e um **pivô no console** (linhas
  tier×rank, colunas Kaeli, célula = TTK mediano em ciclos).
- **Canário de determinismo:** rodar a mesma seed 2× → ticks de kill idênticos.
- Gerar e commitar `docs/balance/baseline.csv` (estado atual, antes de qualquer mudança).

**Aceite:**
- `dotnet run --project tools/BalanceSim -- --out docs/balance/baseline.csv` roda sem exceção.
- Pivô de TTK impresso p/ as 7 Kaelis × 5 tiers.
- Canário de determinismo passa (mesma seed = mesmos números).
- `baseline.csv` commitado.

**Verificação:** `dotnet build` limpo (a ProjectReference compila o engine junto). Inspecionar o pivô e confirmar que reflete o problema atual (hit-kill em mob/boss com gear cheio).

---

# MG-02 — Modelo de Papéis (Knight · Mage · Archer)  ⭐ identidade

- **Modelo:** Opus 4.8 · **Effort:** high · **Skill:** nenhuma (`use context7`) · **Depende de:** MG-01 · **Paraleliza com:** — (solo, Onda 2)

**Objetivo:** introduzir **papel** como eixo primário e separar dano de auto vs skill, velocidade e
range por papel. A dicotomia melee/ranged deixa de dirigir design — `Weapon` fica só cosmético.

**Tarefas:**
- `Domain/Waifus.cs`: `enum KaeliRole { Mage, Archer, Knight }` + campo `Role` em `WaifuDef`. Mapear:
  Eloa/Velvet/Rin = Mage; Gaia/Lunara = Archer; Rynna/Seren = Knight.
- `Domain/GameConfig.cs`: `record RoleTuning(double AutoDmgMult, double SkillDmgMult, int BaseAutoAttackMs, int AutoRange, double AoeScale)` + `IReadOnlyDictionary<KaeliRole, RoleTuning> Roles` com os valores SEED da tabela acima.
- `Engine/GameWorld.cs`: helpers `RoleAutoMult()`/`RoleSkillMult()` lendo `GameConfig.Roles[Waifu.Role]`. Aplicar:
  - auto-hit (`~1394`): `* RoleAutoMult()`.
  - skill-dmg (`~1443`) **e** os ~13 procs de trait/echo/carta que chamam `PlayerAttack()` (`~2045,2089,2235,2256,2275,2285,2304,2328,2352,2369,2394,2415,2434`): `* RoleSkillMult()`. Documentar a convenção num comentário no topo de `PlayerAttack()`.
  - `AutoAttackInterval()` (`~839`): trocar `PlayerAutoAttackMs` por `Roles[Waifu.Role].BaseAutoAttackMs`, mantendo divisores de carta/buff/Gaia e o piso `Max(.,400)`.
  - range de auto: `CanPlayerAutoAttack`/targeting usam `Roles[Waifu.Role].AutoRange` no lugar de `WeaponRange(Weapon)`.
- **NÃO** embutir multiplicador de papel dentro de `PlayerAttack()` (aplicaria duas vezes).
- Conferir `Engine/GameDtos.cs` + `frontend/src/app/core/types.ts`: se `Role` vazar no catálogo, ou marcar `[JsonIgnore]` (server-only) ou sincronizar o tipo no front. Decidir e documentar.

**Aceite:**
- As 5 ordens-alvo se confirmam no simulador (auto archer/knight>mage; skill mage>archer>knight; spd archer>knight>mage; range archer>mage>knight; aoe será validada em MG-04).
- Determinismo intacto (canário do MG-01 passa).
- `dotnet build` limpo; front buildando se o DTO mudou.

**Verificação:** re-rodar o sweep do MG-01 e diferir contra a baseline; confirmar as ordens por papel. Canário de determinismo.

---

# MG-03 — Conversão da Lunara → Arqueira de Gelo

- **Modelo:** Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** MG-02 · **Paraleliza com:** MG-05 (Onda 3) — ⚠ **não** com MG-04 (conflito em `GameWorld.cs` indireto via teste)

**Objetivo:** tornar a Lunara uma arqueira de gelo (single-target ranged + algum AOE), mantendo id
`waifu:lunara`, trait `shatter` e elemento `ice`. Padrão idêntico à Gaia (`Weapon="bow"` + classe
caster): elegibilidade de item é por classe→tipo-de-arma (`ItemAuthoring.DefaultClasses`), desacoplada
de `WaifuDef.Weapon` — `cryomancer` já está no grupo wand, então **não** precisa mexer em item authoring.

**Tarefas:**
- `Domain/Waifus.cs` (`waifu:lunara`): `Weapon "melee" → "bow"`, `Role = Archer` (já feito em MG-02; confirmar). Considerar baixar `BaseHp 205` p/ a faixa archer (Gaia=170) — decidir no MG-06.
- `Domain/Classes.cs` (classe `cryomancer`, exclusiva dela): tornar o kit ranged —
  - `cut` (single, range 1) → range 5 + MissileId de gelo; mantém slow.
  - `frost-leap` (chain, range 2, ChainRange 3) → range 5, ChainRange 4 (= "algum AOE").
  - `garden` (field, range 4) → range 5 (kite).
  - `crescent` (ring em volta, raio 2) → `area` no alvo, range 5, raio 1 (puxa p/ single-target).
  - `new-moon` (nova ult, raio 3) → mantém; o `ScaledRadius` (MG-04) traz a ~2 (burst-AOE dela).
- Confirmar que o front renderiza o arco da Lunara (sprite/missile).

**Aceite:**
- Lunara faz kite a range 5 no simulador (auto + skills disparam a distância).
- id/trait/elemento preservados; contas antigas não quebram.
- `dotnet build` + `npx ng build` limpos.

**Verificação:** rodar a Lunara no sim e confirmar comportamento ranged + TTK medido. Preview: render do arco.

---

# MG-04 — Resize de AOE

- **Modelo:** Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** MG-02 (e MG-03 se rodarem juntos) · **Paraleliza com:** MG-05 (Onda 3)

**Objetivo:** acabar com AOEs gigantes. Hoje `CircleTiles/ConeTiles/RingTiles` inflam a área com um
corte `raio * 1.5` (vira quase quadrado) e toda ultimate é `nova/barrage` raio 3.

**Tarefas:**
- `GameWorld.cs`: helper `int ScaledRadius(int raw) => Math.Max(1, (int)Math.Round(raw * Roles[Waifu.Role].AoeScale))`. Aplicar em vez de `skill.Radius` nos call sites de `area/nova/cone/ring/barrage` (`~1472,1483,1569`, casos nova/barrage).
- Trocar o `* 1.5` de `CircleTiles` (`~3993`), `ConeTiles` (`~4011`) e `RingTiles` (`~1742`) por `GameConfig.AoeRoundingFactor` (começar 1.25) — diamante mais honesto, sem tocar call sites.
- Ultimates: `GameConfig.UltimateRadiusCap = 3`; raio de ult = `Math.Max(2, ScaledRadius(Math.Min(skill.Radius, UltimateRadiusCap)))` → mage ult fica 3, archer ~2, mas ult ainda "estoura".
- Revisar raios de trait/echo/reação (EchoHolocaustRadius, CardDetonateRadius, EloaJudgmentRadius, reaction.Radius) e cortar **só** os maiores se o sim mostrar wipe trivial de pacote elite. **Não cortar AOE de mage abaixo de ~−20%.**

**Aceite:**
- Cobertura de AOE reduzida e diferenciada por papel (mage > knight > archer), confirmada por uma contagem de tiles antes/depois.
- Fantasia de limpar sala (mage) preservada — não over-nerfar.
- `dotnet build` limpo; determinismo intacto.

**Verificação:** tabela de cobertura antes/depois por shape/raio/papel; re-rodar sweep e comparar "ciclos p/ limpar pacote".

---

# MG-05 — Tuning Por Papel Editável No Admin

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma · **Depende de:** MG-02 · **Paraleliza com:** MG-03/MG-04 (Onda 3 — arquivos disjuntos: Content/Api/front)

**Objetivo:** permitir rebalancear a tabela `RoleTuning` em runtime, sem recompilar — exatamente como
tiers/monstros/itens já são editáveis. Persistir no `ContentStore`, seedar dos defaults de `GameConfig`,
expor em `/admin/content/*` e dar uma página de edição no admin.

**Padrão a seguir (já existe, copie):**
- `Content/ContentStore.cs`: tiers são carregados de `.data/content/tiers.json`, seedados de `KaezanContentSeed.Tiers`, com getter + `ReplaceTiers`. Replique para `RoleTuning` (`role-tuning.json`, seed dos defaults `GameConfig.Roles`, getter `RoleTunings` + `ReplaceRoleTunings`).
- `Api/MetaEndpoints.cs` (grupo `/admin`, `~317`): `GET /admin/content/role-tuning` + `PUT /admin/content/role-tuning` espelhando `/admin/content/tiers` (`~392`).
- `frontend/src/app/pages/admin/`: nova página/aba (ex. `role-tuning-editor.ts`) no padrão de `monster-editor.ts`/`item-editor.ts`, plugada no `admin.ts`.
- **Leitura na run:** `GameWorld` deve receber a tabela vigente na construção (como já recebe `DungeonTier`), não ler `GameConfig` direto — assim a edição afeta a próxima run. Ajustar o ponto onde a run é criada (Hub/RunManager) p/ passar o `RoleTuning` do `ContentStore`. **O simulador (MG-01) continua usando os defaults de `GameConfig`** (ou aceita `--role-tuning <json>`), p/ medir contra um baseline estável.

**Aceite:**
- Admin lista e edita os 3 papéis (5 campos cada); salvar persiste em `.data/content/role-tuning.json`.
- Run nova reflete a edição; arquivo corrompido cai no seed sem derrubar o boot (como os outros loaders).
- `dotnet build` + `npx ng build` limpos.

**Verificação:** editar AutoDmgMult de um papel no preview, iniciar run e confirmar mudança no comportamento; reiniciar backend e confirmar persistência.

---

# MG-06 — Tuning Por-Kaeli (Paridade Intra-Papel)

- **Modelo:** Opus 4.8 · **Effort:** high · **Skill:** nenhuma · **Depende de:** MG-03, MG-04 · **Paraleliza com:** — (solo, Onda 4)

**Objetivo:** dentro de cada papel, ajustar `skill.Power`/`BaseAtk`/cooldowns individuais p/ que as
Kaelis do mesmo papel fiquem próximas em tempo de hunt e dano dado (sem dominador óbvio), preservando
a assinatura de cada trait.

**Tarefas:**
- Rodar o sweep do MG-01 e ler dano dado / tempo de hunt por Kaeli, por tier.
- Ajustar **uma alavanca por vez** (Power de skill, BaseAtk, cooldown) p/ trazer trio mage / par
  archer / par knight a ±10% entre si. Tudo em dado/config, nunca hardcode no tick.
- Não quebrar as traits (a passiva continua respeitando `_traitMult`).

**Aceite:**
- Dentro de cada papel, hunt time e dano dado dentro de ±10% entre as Kaelis (tabela de paridade no PR).
- Nenhuma Kaeli trivializa nem fica inviável.
- Determinismo intacto; `dotnet build` limpo.

**Verificação:** tabela de paridade intra-papel (antes/depois) gerada pelo sim.

---

# MG-07 — Normalização Entre Papéis

- **Modelo:** Opus 4.8 · **Effort:** high · **Skill:** nenhuma · **Depende de:** MG-06 · **Paraleliza com:** — (Onda 5) — ⚠ sequencial antes do MG-08 (ambos em `GameConfig`)

**Objetivo:** afinar os multiplicadores de `RoleTuning` p/ que o **tempo total de hunt** seja
comparável entre os 3 papéis, preservando a identidade (mage não vira melhor auto-attacker, etc.).

**Tarefas:**
- Sweep por papel: tempo de hunt, dano dado, dano sofrido, deaths.
- Ajustar `RoleTuning` (e, se preciso, AoeScale) p/ aproximar o tempo de hunt entre papéis mantendo
  as ordens-alvo do MG-02. Atualizar os defaults em `GameConfig` (e o seed do MG-05).

**Aceite:**
- Tempo de hunt entre papéis dentro de uma janela acordada (ex. ±15%), ordens-alvo preservadas.
- Tabela de paridade de papel no PR.

**Verificação:** sweep comparando os 3 papéis; ordens-alvo reconfirmadas.

---

# MG-08 — Calibração de Mobs / Itens / Cartas (5 Tiers)

- **Modelo:** Opus 4.8 · **Effort:** high · **Skill:** nenhuma · **Depende de:** MG-06 (idealmente após MG-07) · **Paraleliza com:** — (Onda 5, sequencial)

**Objetivo:** bater os alvos de TTK (comum ~3, elite ~6, boss ~12 ciclos, gear×mob no mesmo tier)
**sem hit-kill** — boss nunca < 8 ciclos. Calibrar mobs, itens e cartas (as alavancas globais), não salas/dungeons.

**Tarefas (loop dirigido pelo sim, uma alavanca por vez):**
- **HP de mob** (`GameConfig.MonsterStatLines["tier:rank"]`, 15 células): escalar HP por `observado/alvo` rumo a comum≈3 / elite≈6 / boss≈12 ciclos.
- **Restrição anti one-shot**: após o solve de HP, garantir `maior hit único (crit × ult × burst de trait) < HP do boss` em todo tier; se violar, subir piso de HP do boss e/ou capar `UltimatePowerMult`/`EchoHolocaustDamageMult`. Boss nunca < 8 ciclos.
- **Dano de mob** (`.Damage` por célula + global `MonsterDamageTuning=0.26`): deaths ~0 com gear do mesmo tier, mas jogador termina o boss com ~30-60% HP (não trivial).
- **Itens**: se um tier balança TTK demais, ajustar `EquipmentAttackScale` (0.25) e/ou as faixas por-tier "attack"/"skillPower" (`GameConfig.cs:382-474`) p/ degrau ~+15-25%/tier (monotônico).
- **Cartas** (`Cards.cs`): aparar `Value` por stack se full-stack empurra p/ one-shot. Alvo: set natural ≤ +40% total.
- Iterar até as 15 células ficarem dentro de ±1 ciclo, sem flag de one-shot.

**Aceite:**
- 15 células dentro de ±1 ciclo dos alvos; zero flag de one-shot; deaths ~0 com gear do mesmo tier; boss termina com jogador em 30-60% HP.
- Fantasia de mage (limpar sala) e knight (fechar box) preservadas no endgame.
- Cada constante final justificada por número do sim (tabela de diff antes/depois no PR).

**Verificação:** sweep final das 15 células; relatório de TTK + flags de one-shot + deaths.

---

# MG-09 — Verificação Final + Docs

- **Modelo:** Opus 4.8 · **Effort:** medium · **Skill:** nenhuma · **Depende de:** MG-02–MG-08 · **Paraleliza com:** — (solo, Onda 6)

**Objetivo:** fechar a trilha de ponta a ponta e registrar o estado novo.

**Verificação mínima:**
- `dotnet build` + `npx ng build` limpos.
- `dotnet run --project tools/BalanceSim` → sweep final commitado em `docs/balance/final.csv`; diff vs `baseline.csv` no PR.
- Preview: 1 hunt por papel (mage, archer, knight) confirmando feel (range/velocidade/AOE) e render do arco da Lunara.
- Canário de determinismo passa.
- Editar a tabela de papel no admin e confirmar efeito na run.

**Aceite:**
- Builds verdes; sim sem flag de one-shot; TTK dentro dos alvos.
- README atualizado se o comportamento visível mudou (convenção do CLAUDE.md raiz).
- Marcar os MG-NN concluídos com `[x]` + 1 linha de resumo.

---

## Depois

- Pesos de drop de item por papel (gear que casa com a Kaeli).
- Curva de progressão de tier (XP/level) alinhada ao novo TTK.
- Modos de dificuldade (heroic/mythic) reusando a tabela de papel + multiplicadores de mob.
- Telemetria de runs reais alimentando o mesmo CSV do simulador p/ validar o sim contra jogadores.
