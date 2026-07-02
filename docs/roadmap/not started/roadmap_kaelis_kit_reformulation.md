# Roadmap — Reformulação dos Kits das Kaelis (v2)

> **Como usar este arquivo.** Cada `KR-NN` é uma unidade de trabalho **auto-contida**: o agente
> começa "frio", então o prompt traz o contexto necessário. Você dispara com
> **"implemente o prompt KR-NN do `docs/roadmap/not started/roadmap_kaelis_kit_reformulation.md`"**.
>
> **Decisão de design já fechada** vive em [`docs/design/kaelis_kit_reformulation.md`](../../design/kaelis_kit_reformulation.md)
> — o *decision record*. Cada prompt aqui referencia a seção (§4X) que tem o racional completo do kit.
> Leia a seção antes de implementar; este roadmap é o **"como/em que ordem"**, o design doc é o **"o quê/porquê"**.
>
> **Não confundir com:** `docs/roadmap/done/roadmap_refactor_kaelis.md` (K-01..K-07, a *primeira*
> fundação do roster — já feita). Este é o **v2**: ajusta os kits/traits que aquela trilha entregou,
> segundo a reformulação de 2026-06/07.

---

## Modelos & quando usar

| Modelo | Papel | Effort |
|---|---|---|
| **Claude Code Opus 4.8** | Todo prompt desta trilha. É game-design de "gosto" + invariantes de engine (determinismo). Errar cascateia. | `high` nos kits pesados, `medium` nos leves |

- `use context7` ao consultar API (ASP.NET Core, SignalR) — pouco provável aqui, é engine interno.
- Nenhum asset novo: as 7 Kaelis já têm arte. Nada de skill `kaeli-asset-prompts`.

## Invariantes inegociáveis (todo prompt respeita)

- **Backend autoritativo.** Frontend só interpola/renderiza.
- **Determinismo.** Dentro do tick, só o `Rng` da run. Nunca `Random`/`DateTime.Now`/iteração instável.
  Seleção de alvo (vizinho de cascade, salto de chain) determinística: menor distância, desempate por **id estável**.
- **Toda constante de simulação em `Domain/GameConfig.cs`.** Nada de número mágico no tick.
- **Skills data-driven por shape** (`single|beam|nova|area|cone|chain|ring|field|barrage|summon|buff`).
  Kit novo = parametrizar shape existente. **Só criar dispatch novo no engine quando o design exigir uma
  mecânica que nenhum shape cobre** (marcado explicitamente nos prompts abaixo).
- **`_traitMult` (maestria).** Toda passiva continua respeitando `_traitMult` no efeito principal, senão o
  ramo Eco morre.
- **IDs `waifu:*` / `skin:*` estáveis.** Skill ids e class ids são **dados de kit, não persistidos por
  conta** (loadout referencia slot; mastery referencia índice de slot) → podem ser repurposados; mantenha
  o namespace da Kaeli (`skill:eloa:*`). **Confirme** que nada persiste skill id antes de renomear.
- `dotnet build` (e `npx ng build` se tocar HUD) limpos ao fim de cada prompt.

---

## Delta por Kaeli (o que muda vs. o código de hoje)

Slots de hoje (ordem `s0 / s1 / s2 / s3 + ult`, de `Domain/Classes.cs`) → alvo (design doc). `D` =
data-only (`Classes.cs`/`GameConfig.cs`); `E` = precisa de engine (`Engine/GameWorld.cs`).

| Kaeli | Hoje | Alvo | Peso |
|---|---|---|---|
| **Eloa** (§4E) | lance·single / judgment·**barrage** / radiance·beam / halo·**field** / +absolution·barrage | Judging Lance·single / **Dawn Ring·ring** / **Zenith Strike·area(novo)** / Sacred Ray·beam(s3) / +Absolution·barrage | leve — quase tudo `D`; `E` só no Sin bônus e (opcional) anel que expande |
| **Velvet** (§4G) | strike·single / curse·field / nightmare·beam / shade·summon / +plague·barrage | Soul Rend·single / Cursed Ground·field / Abyssal Shade·summon(s2) / Nightmare·beam(s3) / +**Reign of Shadows·barrage(detona Death Orbs)** | leve — reorder `D`; `E` só no ult que detona os Death Orbs pendentes |
| **Rin** (§4F) | ember·single / contract·**chain** / hall·**field** / ashwings·beam / +infernal·barrage | Ember Kiss·single(garante ignite) / **Cinder Storm·area(novo)** / **Wildfire Reckoning·nova(consome burn)** / Ashen Breath·beam / +**Infernal Ball·barrage(multiplica burn)** | médio — `E` no consume-burn e no multiplicador de burn |
| **Gaia** (§4D) | arrow·single / monolith·area / roots·field / shards·**cone** / +tectonic·**barrage** | **Hunter's Aim·auto-mod(s0)** / Binding Roots·field / **Coup de Grâce·execute(s2)** / Monolith Fall·area+stun / +**Ricochet·ult(auto vira chain)** | médio — `E` no auto-mod, execute e ult-auto-chain; corta `cone` |
| **Seren** (§4) | cut·single / advance·**chain** / arc·cone / stance·buff / +zenith·nova | **Astral Sweep·auto-mod(s0)** / Star Lance·beam / War Cadence·buff(atkspd+lifesteal) / Duelist's Call·cone taunt(2 pulsos) / +**Zenith·ult(solta o ramp)** | médio — `E` no auto-mod, ult-solta-trait, lifesteal no buff |
| **Lunara** (§4C) | cut·single / frost-leap·**chain** / garden·field / crescent·area / +new-moon·nova | Frostbite·**auto pierce+cascade** / **Moonlight Volley·auto-mod(s0)** / Frozen Garden·field / Lunar Focus·buff / New Moon·nova(s3) / +**Absolute Zero·ult(freeze+mass shatter, novo)** | pesado — `E` no auto pierce+cascade, auto-mod, ult freeze+mass-shatter |
| **Rynna** (§4B) | claw·single / tail·cone / discharge·**chain** / scale·buff / +storm-heart·nova | Voltaic Claw·single / **Chain Lightning·chain+lifesteal** / **Bloodlust·buff(+dano/lifesteal, +dano tomado)** / **Storm Pull·pull+shield** / +**Storm Heart·ult(detona Static Charge em massa)** | pesado — `E` no rework da trait (mark+detonate), pull, vuln-buff, ult mass-detonate |

---

## Seams compartilhados (o que o KR-00 constrói uma vez)

Três Kaelis (**Seren, Lunara, Gaia**) precisam de **auto-modifiers** ("os próximos N autos ganham
efeito X, consome por auto, reset-on-kill opcional"). Duas ults (**Gaia Ricochet, Seren Zenith**) são
**estados temporários** (auto vira chain / ramp destravado), não shapes de dano — encaixam como ult
**shape `buff`** que liga um estado por `BuffMs`, em vez de dispatch novo. Construir isso **uma vez** no
KR-00 evita 3 implementações divergentes.

- **Auto-modifier:** estado no `GameWorld` (`_autoModKind`, `_autoModChargesLeft`/`_autoModUntilMs`),
  aplicado no caminho do autoattack, consumido por auto. Kinds necessários: `cleave` (área no auto),
  `pierce` (linha/split), `lock_pierce` (trava no alvo da trait + fura pro vizinho). Reset-on-kill como flag.
- **Ult-estado (`buff` shape):** um ult pode ligar um estado que modifica auto/trait por um tempo, sem
  causar dano direto. Reutiliza o dispatch de `buff` (`_buffsUntilMs`) + leitura desse estado no caminho
  de auto/trait. Serve Gaia (auto→chain) e Seren (ramp não zera + todo hit Perfect Cut).

> **Por que não é "dispatch paralelo proibido":** o invariante veta *duplicar a resolução de shape*
> (ex.: um segundo caminho de `area`). Auto-modifier e ult-estado são **modificadores do auto/trait já
> existentes**, uma coisa nova e única — é exatamente onde o design manda parametrizar/estender, não clonar.

---

## Mapa de prompts

| Prompt      | Kaeli / tema                                      | Effort | Depende de   |
| ----------- | ------------------------------------------------- | ------ | ------------ |
| **KR-00** ✅ | Seams compartilhados (auto-modifier + ult-estado) | high   | —            |
| **KR-01** ✅ | Eloa (AoE queen)                                  | medium | KR-00*       |
| **KR-02** ✅ | Velvet (necromante)                               | medium | KR-00*       |
| **KR-03** ✅ | Rin (DoT com ritmo)                               | medium | KR-00*       |
| **KR-04** ✅ | Gaia (serial hunter)                              | medium | KR-00        |
| **KR-05** ✅ | Seren (duelist)                                   | medium | KR-00        |
| **KR-06** ✅ | Lunara (frost marksman)                           | high   | KR-00        |
| **KR-07** ✅ | Rynna (vampiric berserker)                        | high   | KR-00*       |
| **KR-08** ✅ | Balance & verificação                             | medium | KR-01..KR-07 |

`*` = dependência **branda**: Eloa/Velvet/Rin/Rynna não usam o auto-modifier, mas todos os 7 kits editam
`Classes.cs` + `GameConfig.cs` + `GameWorld.cs`, então rodar KR-00 primeiro dá o baseline limpo.

### ⚠ Paralelização: **NÃO** (diferente da trilha K-03/K-05)

Todos os 7 kits tocam os **mesmos 3 arquivos** (`Classes.cs`, `GameConfig.cs`, `GameWorld.cs`) →
conflito de merge garantido. **Rode sequencial**, um por vez, na ordem acima (leve→pesado constrói
confiança e valida os seams do KR-00 cedo). Cada prompt fecha com `dotnet build` verde antes do próximo.

### Fora do escopo desta trilha (tracks globais separados — ver design doc)

Estes são sistêmicos, não per-Kaeli — merecem roadmap próprio depois que os kits assentarem:
- **Cortar reactions elementais** (§2.4): remover `Domain/ElementReactions.cs` + chamadas + campos de marca.
- **Berserk global** (§2.6): HP < threshold → +dano, ponto único em `DealDamageToMonster`. A Rynna se
  beneficia mas o kit dela **não depende** disso pra funcionar (KR-07 entrega sem Berserk).
- **Retune global de barrage** (§2.3): piso de raio, quanto cada strike alimenta a trait.
- **Renomear roles** (§3): `KaeliRole` Mage/Archer/Knight → eixos 2×2. Só display, sem migração.

---

# KR-00 — Seams Compartilhados (auto-modifier + ult-estado) ✅

> **[x] Feito (2026-07-01):** auto-modifier (`_autoModKind/_autoModChargesLeft/_autoModUntilMs/
> _autoModResetOnKill`) com kinds `cleave`/`pierce`/`lock_pierce`, armado por skill `buff` via
> `SkillDef.AutoModKind/AutoModCharges/AutoModResetOnKill`, consumido por auto em `TickPlayerCombat`,
> reset-on-kill hookado em `KillMonster`. Ult-estado: `buff` ult já liga estado nomeado via
> `_buffsUntilMs`; keys reservadas `GameConfig.UltStateAutoChain`/`UltStateRampUnlocked` (sem efeito
> ainda, lidas via `IsBuffActive`). Constantes em `GameConfig.cs`. Nenhum kit alterado — dormant até
> KR-04/05/06 fiarem. Determinístico (vizinho por menor dist + id) e `_traitMult` intocado; `dotnet
> build` limpo (0 warnings). Runs existentes byte-idênticas: todo caminho novo é gated por
> `AutoModKind != null`, falso em todo o roster hoje.

**Resumo esperado:** engine ganha (1) um **auto-modifier** temporário no player (próximos N autos / T ms
ganham um efeito, consome por auto, flag reset-on-kill) com kinds `cleave`/`pierce`/`lock_pierce`, e (2)
suporte a **ult shape `buff` que liga um estado** que modifica auto/trait por `BuffMs` (sem dano direto).
Nenhum kit muda ainda — só os seams, exercitados por um teste mínimo. `dotnet build` limpo.

- **Modelo:** Opus 4.8 · **Effort:** high · **Depende de:** — · **Paraleliza:** não

**Objetivo:** criar os dois mecanismos que Seren/Lunara/Gaia (auto-mod) e Gaia/Seren (ult-estado)
reutilizam, para não serem reinventados 3×. Ler [design doc §2.5](../../design/kaelis_kit_reformulation.md)
(modelo directHit — auto e hit primário são `directHit:true`; ticks de DoT/field/summon não) e §2.7.

**Tarefas:**
- Estado de auto-modifier no `GameWorld` (contador de cargas **ou** janela `UntilMs`, `Kind`, flag
  `ResetOnKill`), lido/consumido no caminho do autoattack. Kinds:
  - `cleave` — auto atinge também os adjacentes (área pequena no ponto do alvo);
  - `pierce` — auto perfura em linha / respinga pro vizinho mais próximo com dano reduzido;
  - `lock_pierce` — trava no alvo da trait (Prey/mark) e fura pro vizinho mais próximo.
- Ganho de carga/janela via `buff` shape (uma skill s0 liga o modifier). Reset-on-kill hookado no on-kill.
- Ult-estado: permitir um ult `buff`-shaped ligar um estado nomeado (reusar `_buffsUntilMs`) que outros
  caminhos (auto/trait) consultam. Deixar 1–2 estados-gancho previstos (ex.: `auto_chain`, `ramp_unlocked`)
  sem efeito ainda — os kits ligam nos prompts seguintes.
- Constantes (raio do cleave, alcance do pierce, falloff do 2º alvo, durações) em `GameConfig.cs`.

**Aceite:**
- Auto-modifier liga por um `buff` s0 de teste, consome por auto, expira, e reseta-por-kill quando marcado.
- Determinístico (vizinho por menor distância + id estável). `_traitMult` intocado.
- `dotnet build` limpo. Nenhum kit de Kaeli alterado (só os seams + fiação).

**Verificação:** `dotnet build`. Rodar a mesma seed 2× e confirmar resultado idêntico. Log/inspeção de
que o contador de auto-mod decrementa por auto e zera no tempo/kill.

---

# KR-01 — Eloa (ranged caster / AoE queen) ✅

> **[x] Feito (2026-07-01):** kit migrado pro §4E. `skill:eloa:judgment` `barrage`→`ring` (Dawn Ring,
> `RingInner:1`) — resolve o double-barrage, só o ult é barrage agora. `skill:eloa:halo` (field)
> cortada; novo `skill:eloa:zenith` (Zenith Strike, `area` instantânea à distância) no s2 — nenhum
> field lingering no kit (o dash trail cobre o residual). Beam (`skill:eloa:radiance`, Sacred Ray) no
> s3 (§2.7). `skill:eloa:lance` vira **Judging Lance** e semeia Sin bônus via novo campo genérico
> `SkillDef.TraitChargeBonus` (Eloa: `GameConfig.EloaJudgingLanceSinBonus=1`) — hook leve na trait
> `judgment` (`ApplyTraitPostDamage` recebe `traitChargeBonus`, threaded por `DealDamageToMonster`;
> base vira `1 + bonus`). Ult Absolution grande+rápido (Strikes 5→7, interval 300→180, delay 200→150),
> sem `StrikeLeavesField`. IDs de skill estáveis (só `halo`→`zenith` repurposado; nenhum id persistido
> por conta). `dotnet build` limpo (0 warnings). **Pendência de polish (§4E.2):** anel que expande ao
> longo de ~2-3s — v1 é anel instantâneo, adiado pra KR-08.

**Resumo esperado:** kit da Eloa migrado pro design §4E — Judgment (barrage) vira **Dawn Ring** (`ring`),
Consecrated Halo (field) é cortada e dá lugar a **Zenith Strike** (`area` novo), Sacred Ray (beam) vai
pro **s3**, Light Lance vira **Judging Lance** (Sin bônus), Absolution (ult barrage) fica grande+rápido.
`dotnet build` limpo.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Depende de:** KR-00* · Ref: **§4E**

**Kit alvo:** `Judging Lance`·single(s0) / `Dawn Ring`·ring(s1) / `Zenith Strike`·area(s2) /
`Sacred Ray`·beam(s3) / **ult** `Absolution`·barrage.

**Delta vs. hoje:**
- `D` — `skill:eloa:judgment`: `barrage` → `ring` (Dawn Ring). Resolve o double-barrage (§4E): agora só o
  ult é barrage. `ring` já existe no dispatch (`GameWorld.cs` ~L2069, usa `RingInner`/`Radius`).
- `D` — `skill:eloa:halo` (field) **cortada**; novo `Zenith Strike` `area` no s2 (bomba de luz instantânea
  à distância). O rastro do dash já cobre o "chão reage" residual (§2.7) — não precisa de field dedicado.
- `D` — reordenar `ClassDef` da Oracle pra beam (`radiance`) ficar no **s3** (regra §2.7).
- `E` (leve) — `Judging Lance` aplica **Sin bônus** (acelera o Judged). Toca o hook da trait `judgment`.
- `E` (**opcional**, pode ficar pra KR-08) — anel que **expande** ao longo de ~2-3s. `ring` hoje é
  instantâneo; expandir é engine (strike-como-anel crescente). **v1 aceitável: anel instantâneo**; anotar a
  expansão como pendência de polish (§4E.2) se custar tempo.

**Arquivos prováveis:** `Domain/Classes.cs`, `Domain/GameConfig.cs`, `Engine/GameWorld.cs` (só o Sin bônus).

**Aceite:** os 5 slots disparam sem exceção; só o ult é barrage; nenhum field lingering no kit; Sin sobe
mais rápido com Judging Lance. `dotnet build` limpo. **Verificação:** run tier 1 com Eloa, HUD de Sin sobe,
Judged detona.

---

# KR-02 — Velvet (ranged caster / necromante) ✅

> **[x] Feito (2026-07-01):** kit migrado pro §4G. Passiva Decay/Death Orb e `skill:velvet:shade`
> (summon) intocados; `skill:velvet:curse` mantido mecanicamente, só renomeado **Curse → Cursed
> Ground**. `skill:velvet:strike` vira **Soul Rend** — finisher com bônus vs HP baixo via novos campos
> genéricos `SkillDef.LowHpBonus/LowHpThreshold` (Velvet: `VelvetSoulRendLowHpBonus=0.60` abaixo de
> `VelvetSoulRendLowHpThreshold=0.35`), aplicado em `HitMonster` (independente do threshold da trait,
> "mesma família"). Beam **Nightmare** desce pro s3 (§2.7): slots viram `strike/curse/shade/nightmare`.
> Ult `skill:velvet:plague` renomeada **Reign of Shadows** + novo flag `SkillDef.DetonateDeathOrbs` →
> no cast, `DetonatePendingDeathOrbs()` resolve na hora todo Death Orb pendente do andar via
> `ResolveStrike` (guarda anti-cascata `_resolvingDeathOrb` intacta; sem recursão; cap
> `VelvetDeathOrbMaxPerFloor` segue valendo). IDs de skill estáveis (só display + ordem de slot
> mudaram). Determinístico (iteração back-to-front sobre ordem de inserção estável, sem Rng);
> `_traitMult` intocado. `dotnet build` limpo (0 warnings). **Pendência §4G.2:** bônus de dano por orbe
> detonado junto e revisão de cap adiados — hoje o ult só antecipa o timer.

**Resumo esperado:** kit da Velvet migrado pro design §4G (versão enxuta) — passiva Decay/Death Orb e
Abyssal Shade (summon) **mantidos como estão**, Nightmare (beam) vai pro **s3**, Mortal Strike vira
**Soul Rend** (bônus vs HP baixo), e a ult **Reign of Shadows** ganha a única peça nova: **força a
detonação imediata de todos os Death Orbs pendentes** no cast. `dotnet build` limpo.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Depende de:** KR-00* · Ref: **§4G**

**Kit alvo:** `Soul Rend`·single(s0) / `Cursed Ground`·field(s1) / `Abyssal Shade`·summon(s2) /
`Nightmare`·beam(s3) / **ult** `Reign of Shadows`·barrage.

**Delta vs. hoje:**
- `D` — passiva `decay` + Death Orb (`GameConfig.cs` ~L1002-1021): **sem mudança**. Não inventar "Soul Orb".
- `D` — `skill:velvet:shade` (summon roaming): **sem mudança**. `skill:velvet:curse` (field): mantém como
  terreno; **não** adicionar plantio de orbe.
- `D` — reordenar pra `nightmare` (beam) no **s3**.
- `E` (leve) — `Soul Rend`: bônus de dano vs HP baixo (mesma família do `executioner`/threshold da Decay).
- `E` — **ult detona os Death Orbs pendentes**: no cast de `Reign of Shadows`, forçar a resolução imediata
  de todos os `Death Orb` da região/andar (em vez de esperar `VelvetDeathOrbDelayMs`). Reusar o caminho de
  detonação existente (`SpawnDeathOrb`/`_resolvingDeathOrb`, `GameWorld.cs` ~L2761-2813); respeitar a guarda
  anti-cascata (`fromTrait:true`, §2.5).

**Arquivos prováveis:** `Domain/Classes.cs`, `Domain/GameConfig.cs`, `Engine/GameWorld.cs`.

**Aceite:** kit intacto onde o design manda intacto (Decay/Death Orb/Shade); ult antecipa a detonação dos
orbes pendentes sem recursão/cascata; cap `VelvetDeathOrbMaxPerFloor` segue valendo. `dotnet build` limpo.
**Verificação:** run com Velvet — matar sob Decay dropa orbe; ult com orbes no chão detona todos na hora.

---

# KR-03 — Rin (ranged caster / DoT com ritmo) ✅

> **[x] Feito (2026-07-01):** kit migrado pro §4F. Passiva Contagion intocada. `skill:rin:contract`
> `chain`→`area` (**Cinder Storm**) — semeia ignite em massa via passiva (area hit é `directHit`, fire →
> Contagion acende cada alvo pego); chain cortada. `skill:rin:hall` `field`→`nova` (**Wildfire
> Reckoning**) via novo campo genérico `SkillDef.ConsumeBurnBonus`: lê o burn pendente *antes* do hit
> (pra o reap escalar com o acumulado, não com o re-ignite), detona `RinReckoningConsumeMult`× dele como
> burst instantâneo (`fromTrait` → sem re-acender), **consome** o fogo e deixa só um brasido leve
> (`RinReckoningEmber*`) — hook `ReapBurn` em `HitMonster`. `Ember Kiss` ganha ignite garantido via DoT
> rider (valores da Contagion). Ult **Infernal Ball** ganha `SkillDef.StackBurnMult`: cada impacto da
> barrage empilha `_rinBurnMultStacks` (cap `RinInfernalBurnMultMaxStacks`, +`RinInfernalBurnMultPerStack`/
> stack, decai 1 por `RinInfernalBurnMultDecayMs` em `TickTraitTimers`) — **não consome** (contraste
> explícito com o s2); amplifica todo tick de burn (`TickMonsterDots`) e o reap. Ashen Breath (beam) já
> estava no s3. HUD de Contagion mostra o multiplicador ativo. Determinístico (sem Rng nos novos
> caminhos; iteração/seleção estáveis); `_traitMult` intocado; IDs de skill estáveis (só display +
> shape mudaram). `dotnet build` limpo (0 warnings). Sem toque no frontend (client estampa FX por shape
> genérico; nenhuma ref a skill id de Rin). **Pendência §4F.2:** números finais do multiplicador e da
> ordem ótima Reckoning×ult adiados pra KR-08.

**Resumo esperado:** kit da Rin migrado pro design §4F — Burning Contract (chain) vira **Cinder Storm**
(`area`, semeia ignite em massa), Hall of Flames (field) vira **Wildfire Reckoning** (`nova` que **consome**
burn por dano massivo), Ember Kiss garante ignite, Ashen Breath (beam) fica no s3, e Infernal Ball (ult
barrage) vira **multiplicador de burn empilhado por impacto** (não consome). `dotnet build` limpo.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Depende de:** KR-00* · Ref: **§4F**

**Kit alvo:** `Ember Kiss`·single(s0) / `Cinder Storm`·area(s1) / `Wildfire Reckoning`·nova(s2) /
`Ashen Breath`·beam(s3) / **ult** `Infernal Ball`·barrage.

**Delta vs. hoje:**
- `E` — `skill:rin:contract`: `chain` → `area` (Cinder Storm), aplicando **ignite em todos os pegos**
  (semeia a passiva `contagion` sem esperar o salto lento). Chain sai (redundante com a própria passiva +
  §2.7). Ashen Breath (beam) já está no s3.
- `E` — `skill:rin:hall`: `field` → `nova` (Wildfire Reckoning): dano bônus em **todo mundo queimando**,
  escalando com o burn restante, **consumindo-o** (reset leve). Novo hook: ler/consumir burn no tick.
- `E` (leve) — `Ember Kiss` **garante ignição** mesmo em alvo frio.
- `E` — ult `Infernal Ball`: cada impacto **empilha um multiplicador de dano de burn** na sala por um tempo,
  **sem consumir** (contraste explícito com o s2 que consome, §4F.1). Estado por-Kaeli, decai no tempo.

**Arquivos prováveis:** `Domain/Classes.cs`, `Domain/GameConfig.cs`, `Engine/GameWorld.cs`.

**Aceite:** semeia (s1) → passiva pinga → ult multiplica → s2 colhe é jogável; s2 consome e ult não;
multiplicador decai; determinístico. `dotnet build` limpo. **Verificação:** run com Rin — Cinder Storm
acende vários; ult sobe o dano de burn visivelmente; Reckoning dá o pico e zera o burn.

---

# KR-04 — Gaia (ranged auto / serial hunter) ✅

> **[x] Feito (2026-07-02):** kit migrado para §4D. `skill:gaia:arrow` virou **Hunter's Aim** (`buff`
> que arma `lock_pierce`, 4 cargas); `skill:gaia:roots` ficou no s1; `skill:gaia:shards` foi
> reaproveitada como **Coup de Grace** (`single`) com bônus contra HP baixo e `ConsumePreyRampBonus`
> para cash-out/reset da ramp da Prey; `skill:gaia:monolith` foi para s3 como `area`+stun com dano
> real; `skill:gaia:tectonic` virou **Ricochet** (`buff` ult-state `auto_chain`). Engine: autos em
> `auto_chain` travam na Prey legal, saltam deterministicamente por distância/id para vizinhos com
> dano reduzido, e ganham buff leve de ataque/attack speed; boss sem vizinho não desperdiça. Passiva
> Prey mantida, IDs estáveis, constantes em `GameConfig.cs`; `dotnet build` limpo (0 warnings).

**Resumo esperado:** kit da Gaia migrado pro design §4D — passiva Prey **mantida**, Mineral Arrow vira
**Hunter's Aim** (auto-mod `lock_pierce` do KR-00), Stone Shards (cone) sai e entra **Coup de Grâce**
(execute que consome a ramp), Monolith Fall vira `area`+stun com **dano real**, e a ult Tectonic Rain
(barrage) vira **Ricochet** (ult-estado: auto vira chain, cheio na Prey + reduzido nos vizinhos + buff leve).
`dotnet build` limpo.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Depende de:** KR-00 · Ref: **§4D**

**Kit alvo:** `Hunter's Aim`·auto-mod(s0) / `Binding Roots`·field(s1) / `Coup de Grâce`·execute(s2) /
`Monolith Fall`·area+stun(s3) / **ult** `Ricochet`.

**Delta vs. hoje:**
- `D` — passiva `prey` (`GameConfig.cs` ~L1051): **sem mudança**.
- `E` — `Hunter's Aim` (s0): usa o **auto-modifier `lock_pierce`** do KR-00 (autos travam na Prey + furam
  pro vizinho com dano reduzido). Ensina o Ricochet em escala pequena.
- `D` — `Binding Roots` (field): mantém (renomear/ajustar). `Stone Shards` (cone) **cortada**.
- `E` — `Coup de Grâce` (s2): tiro pesado, **bônus vs HP baixo**, **consome a ramp da Prey** num burst
  (execute — motor do snowball).
- `D`/`E` — `Monolith Fall` (s3): `area`+stun com **dano real** (não peel puro, §4D.1) — hoje já é
  `area`+stun, só garantir Power real.
- `E` — ult `Ricochet`: **ult-estado `auto_chain`** (KR-00) por `BuffMs` — autos saltam pros vizinhos,
  **dano cheio na Prey**, **reduzido** nos demais; + buff leve de atk/atkspeed. Sem vizinho (boss) = auto
  normal, nunca desperdiça (§4D.1).

**Arquivos prováveis:** `Domain/Classes.cs`, `Domain/GameConfig.cs`, `Engine/GameWorld.cs`.

**Aceite:** foco serial preservado; ult vira AoE sem largar a Prey; execute pula a marca e acelera. Boss:
Ricochet não desperdiça. `dotnet build` limpo. **Verificação:** run com Gaia — Prey rampa até o cap;
Coup de Grâce fecha kill e pula a marca; ult faz auto saltar em pack.

---

# KR-05 — Seren (melee auto / duelist)

> **[x] Feito (2026-07-02):** kit migrado para §4. `skill:seren:cut` virou **Astral Sweep**
> (`buff` que arma `cleave`, 3 cargas, reset-on-kill); `skill:seren:advance` virou **Star Lance**
> (`beam`); slots s2/s3 ficaram **War Cadence** (attack speed + lifesteal via buff) e
> **Duelist's Call** (cone taunt + 2 pulsos agendados ao redor da Seren); ult **Zenith** virou
> `buff` com `ramp_unlocked`, levando Discipline ao cap, sem reset por troca de alvo, e com Perfect
> Cut em todo hit. Constantes em `GameConfig.cs`, passiva Discipline preservada, sem gap-closer novo;
> `dotnet build` limpo (0 warnings).

**Resumo esperado:** kit da Seren migrado pro design §4 — passiva Discipline **mantida**, Precise Cut vira
**Astral Sweep** (auto-mod `cleave` reset-on-kill), Astral Advance (chain) vira **Star Lance** (`beam`),
Sword Arc e Zenith Stance trocam de slot pra **War Cadence** (buff atkspeed+lifesteal, s2) e
**Duelist's Call** (cone taunt + 2 pulsos, s3), e a ult **Zenith** vira ult-estado que **solta a trava da
Discipline** (ramp não zera + todo hit Perfect Cut). `dotnet build` limpo.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Depende de:** KR-00 · Ref: **§4**

**Kit alvo:** `Astral Sweep`·auto-mod(s0) / `Star Lance`·beam(s1) / `War Cadence`·buff(s2) /
`Duelist's Call`·cone taunt(s3) / **ult** `Zenith`.

**Delta vs. hoje:**
- `D` — passiva `discipline` (`GameConfig.cs`): **sem mudança**.
- `E` — `Astral Sweep` (s0): auto-modifier **`cleave` com reset-on-kill** (KR-00) — próximos 3 autos em
  área, kill devolve carga (cap 3). Motor de clear de pack via auto.
- `D` — `Astral Advance` (chain) → `Star Lance` `beam` (estocada em linha, área pequena/dano alto).
- `E` (leve) — `War Cadence` (s2): buff **atkspeed + lifesteal**. Confirmar se há lifesteal-em-buff; se
  não, adicionar o gancho (sustain de box).
- `D`/`E` — `Duelist's Call` (s3): cone + taunt (já existe) + **2 tempos de dano** ao redor. Multi-pulso é
  `E` leve; v1 pode ser 1 pulso + anotar.
- `E` — ult `Zenith`: **ult-estado `ramp_unlocked`** (KR-00) — por `BuffMs`, a ramp da Discipline vai ao
  **máximo e não zera ao trocar de alvo** + **todo hit é Perfect Cut**. Fecha o loop (serve pilha e boss, §4.1).

**Arquivos prováveis:** `Domain/Classes.cs`, `Domain/GameConfig.cs`, `Engine/GameWorld.cs`.

**Aceite:** Astral Sweep liga sozinha em pack (kills resetam) e apaga em boss; ult destrava o ramp em
pilha; sem gap-closer novo (dash cobre). `dotnet build` limpo. **Verificação:** run com Seren (HP 240) —
ramp da Discipline no HUD; Astral Sweep cleava e reseta por kill; ult mantém ramp ao trocar de alvo.

---

# KR-06 — Lunara (ranged auto / frost marksman)

> **[x] Feito (2026-07-02):** kit migrado para §4C: Moonlight Volley arma `pierce`, Lunar Focus dá attack speed + range, New Moon virou s3 de peel, Absolute Zero hard-freeze + mass shatter, e Frostbite agora acumula/cascadeia stacks por auto/gelo; `dotnet build` e `npx ng build` limpos.

**Resumo esperado:** kit da Lunara migrado pro design §4C — Frostbite ganha **auto baseline que
perfura/respinga + cascade de shatter** nos frosted vizinhos, Lunar Cut vira **Moonlight Volley**
(auto-mod `pierce`), slow limpo pra ficar só em s1/s3, New Moon (nova) desce pro **s3** e entra uma ult
nova **Absolute Zero** (hard-freeze + mass shatter escalando com stacks). `dotnet build` limpo.

- **Modelo:** Opus 4.8 · **Effort:** high · **Depende de:** KR-00 · Ref: **§4C**

**Kit alvo:** `Moonlight Volley`·auto-mod(s0) / `Frozen Garden`·field(s1) / `Lunar Focus`·buff(s2) /
`New Moon`·nova(s3) / **ult** `Absolute Zero`.

**Delta vs. hoje:**
- `E` — passiva `shatter`: **auto baseline perfura/respinga** um pouco (espalha frost sozinho) + o shatter
  **cascateia** nos frosted vizinhos (podem estilhaçar também; consome stacks). Sem hard-freeze aqui — só
  dano + cascade + slow curto. Cascade determinística (raio/saltos/falloff em `GameConfig.cs`, §4C.2).
- `E` — `Moonlight Volley` (s0): auto-modifier **`pierce`** (KR-00) + frost extra.
- `D` — `Frozen Garden` (field): mantém (slow+frost, zona de kite).
- `E`/`D` — `Lunar Focus` (s2): buff **atkspeed (+ range)**. Se range-buff não existir, `E` leve pra somar
  o gancho; senão só atkspeed.
- `D` — `New Moon` (nova) sai da ult e vira **s3** (nova de slow/root, peel anti-cerco).
- `E` — **nova ult `Absolute Zero`**: **hard-freeze** num raio (o único do kit) + **mass shatter** de toda
  a frost de uma vez, escalando com stacks. Fecha o loop (a run empilha, a ult cobra, §4C).
- `D` — **limpar slow** das skills que não são kite: hoje as 5 lentam; manter só em s1 (garden) e s3 (new-moon).

**Arquivos prováveis:** `Domain/Classes.cs`, `Domain/GameConfig.cs`, `Engine/GameWorld.cs`.

**Aceite:** dano mora no auto + Frostbite; auto espalha frost e cascateia; hard-freeze só na ult; slow
enxuto (s1/s3). Boss: frost empilha nele → estilhaços grandes. `dotnet build` limpo. **Verificação:** run
com Lunara — autoar o trem acende cascade; ult congela + mass-shatter; conferir que não virou a melhor
clear do jogo (é boss-leaning).

---

# KR-07 — Rynna (melee caster / vampiric berserker)

> **[x] Feito (2026-07-02):** kit migrado para §4B: Static Charge virou mark+detonate single-target com carga por hit/dano recebido; Chain Lightning tem lifesteal, Bloodlust dá dano/lifesteal com vulnerabilidade, Storm Pull puxa e concede Echo Shield, e Storm Heart agenda 3 ondas que recarregam Charge e detonam marcas em massa; `dotnet build` limpo (0 warnings).

**Resumo esperado:** kit da Rynna migrado pro design §4B — a passiva Static Charge é **reformulada** para
mark+detonate (o AoE espalha marca; Charge cheia → próximo golpe **detona a marca num alvo** = burst
single-target + stun), Short Discharge (chain) vira **Chain Lightning** (AoE + lifesteal), Conductive Scale
(buff) vira **Bloodlust** (+dano/lifesteal, +dano tomado), Thundering Tail (cone) dá lugar a **Storm Pull**
(puxa + shield via `GainEchoShield`), e a ult **Storm Heart** **detona a Static Charge em massa** e recarrega
a Charge por onda. `dotnet build` limpo.

- **Modelo:** Opus 4.8 · **Effort:** high · **Depende de:** KR-00* · Ref: **§4B**

**Kit alvo:** `Voltaic Claw`·single(s0) / `Chain Lightning`·chain(s1) / `Bloodlust`·buff(s2) /
`Storm Pull`·pull+shield(s3) / **ult** `Storm Heart`·nova.

**Delta vs. hoje:**
- `E` — passiva `static_charge` **reformulada**: hits aplicam **marca** (o AoE dela espalha marca na pilha);
  Charge enche em melee (mais rápido cercada/ao apanhar); cheia → o **próximo golpe detona a marca num
  alvo** = **dano single-target + stun curto** (§4B.1: detona num alvo, não exori — é o single-target que
  falta). Difere do atual (chain discharge + paralyze).
- `E` (leve) — `Voltaic Claw` (s0): single + enche Charge.
- `E` — `Chain Lightning` (s1): `chain` (mantém) + **lifesteal** (dano de pack + sustain).
- `E` — `Bloodlust` (s2): buff **+dano + lifesteal, mas toma mais dano** (vulnerabilidade). Novo tipo de
  buff — derruba o HP pro comeback (gancho pro Berserk global futuro, mas **não depende** dele).
- `E` — `Storm Pull` (s3): **puxa** inimigos pra perto (mecânica nova de pull) + **shield** no engate
  (reusar `GainEchoShield`). Substitui a cone `tail`.
- `E` — ult `Storm Heart`: mantém `nova` em pulsos, mas **cada onda detona a Static Charge em massa** em
  todo marcado pego (dano+stun+lifesteal) e **recarrega a própria Charge** por onda (§4B fix de auditoria).

**Nota:** o **pull** é a única mecânica genuinamente nova do roster (nenhum shape puxa hoje). Confirmar se
vale um mini-seam ou é local à Rynna. Preset de helper dela (auto-heal off/baixo, §4B.2) é ajuste de
`ApplyHelperProfile` — pode ficar pra KR-08 se apertar.

**Arquivos prováveis:** `Domain/Classes.cs`, `Domain/GameConfig.cs`, `Engine/GameWorld.cs`,
`Domain/Waifus.cs` (descrição da trait).

**Aceite:** marca espalha via AoE e detona single-target; Bloodlust derruba HP e devolve via lifesteal;
Storm Pull junta o box; ult descarrega toda marca de uma vez. Determinístico. `dotnet build` limpo.
**Verificação:** run com Rynna (HP 220) — Charge enche ao apanhar; detonação single-target; ult limpa a
sala detonando as marcas espalhadas.

---

# KR-08 — Balance & Verificação ✅

> **[x] Feito (2026-07-02):** Dawn Ring expansivo e preset de helper da Rynna fechados; 7 Training runs via hub dispararam slots 0-4 sem exceção, Lunara/Gaia reproduziram hashes idênticos na mesma seed, `dotnet build` limpo e `npx ng build` sem erros (warnings de budget CSS existentes).

**Resumo esperado:** os 7 kits reformulados verificados de ponta a ponta — builds verdes, determinismo
(mesma seed 2×), nenhum dominador óbvio, e os pontos de polish adiados (anel que expande da Eloa, 2º pulso da
Duelist's Call, range-buff da Lunara, preset de helper da Rynna) resolvidos ou registrados como pendência.

- **Modelo:** Opus 4.8 · **Effort:** medium · **Depende de:** KR-01..KR-07 · **Paraleliza:** não (solo)

**Verificação mínima:**
- `dotnet build` + `npx ng build` limpos.
- Rodar 1 run por Kaeli (7) — cada kit dispara os 5 slots + ult sem exceção, HUD da passiva evolui.
- Mesma seed 2× em pelo menos 2 Kaelis → resultado idêntico (determinismo dos novos hooks: cascade da
  Lunara, chain do ult da Gaia, detonação de orbe da Velvet, multiplicador de burn da Rin).
- Sanidade de números: nenhum ult/kit deleta a run; ranged segue frágil se cercada, melee tanka o box.

**Aceite:**
- Builds verdes; 7 runs sobem sem erro; determinismo confirmado.
- Estado vivo das passivas visível no HUD (Sin, ramp, Prey, Charge, frost stacks, Decay).
- Pendências de polish (§4X.2 de cada) fechadas **ou** anotadas no design doc.

---

## Depois (tracks globais — não desta trilha)

- Cortar reactions elementais (§2.4) · Berserk global (§2.6) · retune global de barrage (§2.3) ·
  renomear roles (§3). Cada um vira seu próprio `KR-`/roadmap quando os kits assentarem.
- Armas assinatura por Kaeli; números finais da maestria por passiva nova.
