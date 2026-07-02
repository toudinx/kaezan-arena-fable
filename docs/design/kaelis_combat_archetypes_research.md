# Pesquisa & Design — Identidade de Combate por Arquétipo (Mage · Archer · Knight)

> **Status:** documento de pesquisa + proposta de design. **Nada implementado.** Serve de base para
> um futuro roadmap (`docs/roadmap/...`). Texto em PT (material de revisão); todo termo de jogo/ID/
> stat fica em inglês, conforme a política de idioma do projeto.
>
> **Objetivo do usuário:** repensar e fortalecer os kits das três classes —
> **Mage** (caos de área/DoT/AoE/beams/retas), **Archer** (foco em auto-attack + attack speed,
> pierce, multishot, mas com algum AoE) e **Knight** (melhor corpo-a-corpo, AoE *melee*, provocar/
> taunt, talvez reflect/berserker). E decidir **o que diferencia duas Kaelis do mesmo papel** além
> do elemento — inspirado nos "tipos de dano" de Wuthering Waves, mas adaptado ao fato de que aqui
> temos **muito mais habilidades** do que o sistema de 5 slots da WuWa.

> **⚠️ DECISÃO DE DIREÇÃO (atualização pós-revisão).** Conversando, o rumo mudou e ficou mais simples
> e melhor: **manter o frame de identidade enxuto = `element` + `role` (Mage/Archer/Knight)**. Os
> eixos novos da §4 (damage-tags estilo WuWa, sub-arquétipo formal) ficam **despriorizados** — não é
> por aí que duas Kaelis do mesmo papel vão se diferenciar. A diferenciação (e a diversão) vem de
> **profundidade e caos da implementação de cada kit**, não de um meta-sistema novo. Prova viva:
> Velvet e Rin são as duas mages, mesmo frame, e a Rin **sente** outra personagem só porque os shapes
> dela se **propagam** e os da Velvet não. O usuário quer **mais caos**, começando por consertar a
> Velvet (e o Abyssal Shade, ótimo conceito / implementação simples demais). Ver a nova **§4bis**, que
> passa a ser a seção principal. As §4–§7 ficam como registro da pesquisa.

---

## 0. Como ler este doc

- **§1** fotografa o sistema atual (papéis, kits, traits, cards) — o ponto de partida real no código.
- **§2** é a pesquisa de mercado (Tibia, Diablo, Hades, Wuthering Waves) com as lições aplicáveis.
- **§3** é o diagnóstico: o que falta hoje para cada arquétipo entregar a fantasia.
- **§4** guarda a pesquisa dos eixos novos (damage-tags + sub-arquétipo + builder/spender) —
  **despriorizada** pela decisão de direção; fica como registro.
- **§4bis** é a **seção principal agora**: caos vem da profundidade do kit. Teardown Velvet × Rin +
  redesign do Abyssal Shade + princípios de caos pras mages.
- **§5–§7** detalham por arquétipo, respondem "o que muda entre duas archers", e listam ideias extras.
- **§8** lista as decisões em aberto para você bater o martelo antes de virar roadmap.

---

## 1. Estado atual (no código, hoje)

### 1.1 Papel é o eixo primário de identidade (`KaeliRole` + `GameConfig.Roles`)

`Domain/GameConfig.cs` já trata **role** (Mage/Archer/Knight) como o eixo mecânico principal. A
dicotomia melee/ranged morreu como conceito — `Weapon` é só cosmético (sprite/missile). Os números
seed (refináveis em admin):

| Role | AutoDmgMult | SkillDmgMult | BaseAutoAttackMs | AutoRange | AoeScale |
|---|---|---|---|---|---|
| **Mage** | 0.75 | **1.15** | 2000 (lento) | 4 | **1.00** (maior) |
| **Archer** | **1.15** | 0.95 | **1400** (rápido) | **5** (maior) | 0.65 (menor) |
| **Knight** | 1.05 | 0.80 | 1700 | 1 (melee) | 0.80 |

Ou seja, o esqueleto da fantasia **já existe**: mage = skill+AoE, auto fraco e lento; archer = auto
forte e rápido, alcance longo, AoE pequeno; knight = auto sólido, melee, AoE médio. O que falta é
**mecânica que faça esses números virarem identidade jogável** (não só multiplicadores invisíveis).

### 1.2 Kits são data-driven por *shape* (`Domain/Classes.cs`)

Cada Kaeli aponta para uma `ClassDef` com 4 slots + 1 ultimate. Cada skill é um `SkillDef` com um
**shape** genérico e riders opcionais — nenhum dispatch novo no engine para skill nova:

- Shapes: `single · area · cone · beam · nova · chain · ring · field · barrage · summon · buff`.
- Riders já existentes: **DoT** (`DotTicks/DotTickMs/DotPower`), **slow** (`SlowFactor/SlowMs`),
  **summon** (constructo que pulsa), **field** (terreno que pode se **alastrar** —
  `FieldSpreadChance/Generations`), **chain** (pula entre alvos), **barrage** (golpes em sequência),
  **ring** (anel oco), **stun** (`StunMs`), **buff** (`aegis/atkspeed/haste/taunt`).

Mapa atual papel→Kaeli→kit:

| Kaeli | Role | Element | Kit (shapes) | Trait (kind) |
|---|---|---|---|---|
| **Eloa** | Mage | holy | single · barrage · beam · ring · nova(ult) | judgment (marca+detona, cura) |
| **Velvet** | Mage | death | single · area(DoT) · beam · summon · nova(ult,DoT) | decay (DoT+execução) |
| **Rin** | Mage | fire | single · chain(DoT) · field(spread) · cone · barrage(ult,spread) | contagion (fogo pula) |
| **Lunara** | Archer | ice | single(slow) · chain(slow) · field(slow) · area(slow) · nova(ult,slow) | shatter (bônus vs slowed) |
| **Gaia** | Archer | earth | single · area(stun) · field(root) · cone · barrage(ult,stun) | prey (ramp por tempo de caça) |
| **Seren** | Knight | physical | single · chain · cone(taunt) · buff · nova(ult,stun) | discipline (combo ramp) |
| **Rynna** | Knight | energy | single(stun) · cone(taunt) · chain · buff(atkspeed) · nova(ult) | static_charge (barra→discharge) |

### 1.3 Cards & echoes (`Domain/Cards.cs`)

- **Commons** = multiplicador de stat puro: `atkPercent`, `atkSpeedPercent`, `critChance`,
  `elementPercent`, `lifesteal`, `damageReduction`, etc.
- **Rares** = mecânica (`echo_surge`, `double_strike`, `detonate`).
- **Echoes** = 3 por Kaeli, win-conditions ancoradas na trait (sin/combo/curse/burn/charge/frost/prey).

**Observação-chave:** os cards de stat hoje são **globais** — não existe um card que diga "+X% de dano
de auto-attack" ou "+X% de dano de DoT" ou "autos perfuram". Toda a granularidade que a WuWa tem
("dano de skill", "dano de ultimate") **não existe** no nosso sistema de cards ainda. Esse é o gancho
mais barato para enriquecer build (ver §4.1).

### 1.4 Verificação ao vivo (Training Room)

Subi backend (`:5210`) + frontend e testei a **Velvet** na Training Room: disparar Curse (area DoT) +
Nightmare (beam) + Abyssal Shade (summon) + Eternal Plague (nova/ult) cria, de fato, **caos de área**
legível — anel de praga, shade invocada, números de DoT, barra inteira em cooldown. A fantasia de
"maga = caos" já lê. O loop de sandbox funciona (dummy passivo, regen, sem recompensa). O gargalo
não é o mage — é **archer e knight não terem ainda um loop mecânico tão satisfatório quanto o caos do mage**.

---

## 2. Pesquisa de mercado

### 2.1 Tibia — as vocações mapeiam quase 1:1 no nosso projeto

| Vocação | Fantasia | Onde encaixa aqui |
|---|---|---|
| **Sorcerer** | Magia ofensiva de área (fire/energy/death), AoE puro | **Mage caótico** (Rin/Velvet) |
| **Druid** | Ice/earth, controle, slow, suporte/cura | **Mage de controle** (Lunara é archer-ice hoje, mas o *kit* druídico de slow/field é de mage) |
| **Paladin** | Distance fighting (bow/crossbow), **auto-attack é o foco**, solo-eficiente, holy | **Archer** — exatamente a fantasia que o usuário quer (auto-attack-cêntrico) |
| **Knight** | Melee, maior HP/capacity, segura aggro (`exeta res` = challenge/taunt em área) | **Knight tank/provocador** |
| **Monk** (2025) | Melee **builder/spender**: *Harmony* (stacks 7%→112%) acumulada por builders, gasta em spenders; *Virtues*; estado *Serene* (poder máximo) | **Frame builder/spender** para Knight (e reaproveitável p/ todos) |

Lições Tibia:
- **Paladin valida o pedido do usuário**: existe uma classe inteira cuja graça é o tiro automático à
  distância, com magia só de apoio. É o norte do nosso Archer.
- **Monk dá o esqueleto de "combo/recurso"**: Harmony = builder (acumula) → spender (gasta num golpe
  devastador). É o que falta para Archer e Knight terem um *loop* (hoje só o mage tem o "loop" via
  condições/fields).
- **Knight `exeta res`** = taunt em área que força o alvo a focar você — o "provocar" que o usuário pediu.

### 2.2 Diablo IV — Rogue é o blueprint do nosso Archer

A Rogue do D4 é praticamente o documento de design do Archer que o usuário descreveu:

- **Penetrating Shot** — projétil que **perfura em linha reta** (pierce). → rider `Pierce` num
  `single`/`beam`.
- **Barrage** — dispara **vários projéteis num cone** (multishot), variante com 7 projéteis
  perfurantes de alta velocidade. → multishot "em área" (leque) **ou** sequencial (já temos `barrage`).
- **Rapid Fire** — spam de flechas que **ricocheteia** (chain) para burst e geração de stacks.
- **Attack speed como núcleo**: aspects *Ferocious* (AS flat) e *Accelerating* (crit concede buff
  **global** de attack speed) — "o attack speed sozinho é o que faz o build sentir bom no começo".
- **Combo Points** (specialization): *builder/spender* — atira 3 Forceful Arrows (builder, o auto mais
  rápido) e libera um Penetrating Shot supercarregado com crit garantido. → **builder/spender de archer**.

E para o Knight: **Thorns Berserker** (Barbarian) — **reflete** o dano recebido (*Thorns*) e usa
*Challenging Shout* (taunt em área **+** redução de dano) para juntar inimigos e matá-los com o
próprio reflexo. → exatamente "reflect damage / berserker / taunt" que o usuário cogitou.

### 2.3 Hades — a lição é "cadência define identidade, e o buff segue a cadência"

- Boons se separam por **slot**: Strike (attack), Flourish (special), Shot (cast), Flare (cast AoE).
- Princípio de design: **armas/golpes lentos** querem boons de **% multiplicativo** (Aphrodite,
  Artemis, Athena); **golpes rápidos** querem boons que **adicionam uma fonte nova de dano** (Zeus,
  Dionysus, Ares).
- Aplicação aqui: **Archer (rápido)** deve querer **procs on-hit / fontes adicionais** (pierce extra,
  ricochete, dano elemental por flecha), porque multiplicar % num auto rápido escala demais. **Mage
  (lento, por cast)** deve querer **amplificadores % por cast/condição**. Isso justifica linhas de
  card diferentes por papel (ver §4.1) e impede que "+12% attack" seja o card ótimo para todo mundo.

### 2.4 Wuthering Waves — o que adotar e o que **não** adotar

- WuWa separa o dano em **tipos** buffáveis independentemente: Basic Attack / Heavy Attack /
  Resonance Skill / Liberation (ult) / Echo. Energia gerada por dano e por esquiva alimenta a ult.
- **O usuário está certo**: replicar o sistema de 5 slots fixos (auto/heavy/skill/echo/ult) não cabe,
  porque aqui cada Kaeli tem 5 habilidades + auto + trait — habilidades demais para amarrar em 5 tipos.
- **O que adotar (barato e poderoso):** a ideia de que **todo dano carrega uma *tag*** e que
  buffs/cards/equip miram a tag. Em vez de 5 slots fixos, usar um conjunto pequeno de **damage-tags
  derivadas de *como* o dano foi causado** (não de um slot fixo): `auto`, `skill`, `dot`, `aoe`,
  `ultimate`. Reaproveita o pipeline de dano que já distingue `AutoDmgMult` vs `SkillDmgMult`. Ver §4.1.

---

## 3. Diagnóstico — o que falta por arquétipo

| Arquétipo | Fantasia alvo | Já tem | **Falta** |
|---|---|---|---|
| **Mage** | Pinta o mapa de hazards, condições e beams; caos crescente | Fields/spread, DoT, beams, nova, summon, contagion | Pouco: amarrar melhor "condição → detonação"; diferenciar as 3 escolas (burst/DoT/spread) explicitamente |
| **Archer** | Auto-attack é o kit; skills são *enablers* (haste, marca, pierce, multishot) | Auto rápido (1400ms), alcance 5, haste situacional (trait) | **Pierce**, **multishot**, **steroid de attack speed dedicado**, **builder/spender de combo**, **cards de auto/AS**, **status que premia tiro sustentado (armor break)** |
| **Knight** | Melhor melee, AoE *melee*, **provoca**, bruiser sustentado | Cones com `taunt`, novas com stun, combo ramp (Seren), charge (Rynna) | **Taunt/threat de verdade** (aggro), **reflect/thorns**, **berserker mode**, **sustain melee** (lifesteal/shield), formalizar builder/spender |

---

## 4. Proposta central — dois eixos novos de identidade

### 4.1 Eixo A — **Damage-tags** (a adaptação enxuta da WuWa)

**Tese:** toda instância de dano ganha um conjunto de **tags** derivado de como foi causada. Cards,
echoes e equipamento passam a poder amplificar **uma tag**. Isso entrega a sensação de "tipo de dano"
da WuWa **sem** um sistema de 5 recursos separados, e reusa o pipeline atual.

Tags propostas (pequeno conjunto, derivado, não slot fixo):

- `auto` — veio do auto-attack.
- `skill` — veio de uma habilidade ativa (slots 1–4).
- `ultimate` — veio do slot R.
- `dot` — tick de condição (burn/curse/poison...).
- `aoe` — shape de área (`area/cone/nova/ring/field`) atingindo 2+ alvos.

Uma instância pode ter **mais de uma tag** (ex.: o ult de área é `ultimate`+`aoe`+`skill`; um tick de
Curse é `dot`). Buffs miram tags: "+X% `auto` damage" (card de archer), "+X% `dot` damage" (card de
mage), "+X% `ultimate` damage" (universal premium).

**Por que isso resolve o pedido do usuário:**
- Dá ao Archer um eixo de build próprio (empilha `auto` + attack speed) sem virar "mage com flecha".
- Dá ao Mage um eixo próprio (`dot`/`aoe`) — caos escalável por build.
- Segue a lição de Hades (§2.3): linhas de card distintas por papel, "+12% attack" deixa de ser o
  pick ótimo universal.

**Custo de implementação (estimado):** baixo-médio. O dano já passa por um ponto central que conhece
auto vs skill (`RoleTuning`). É anexar um enum de flags na instância de dano e ler no cálculo
final + adicionar cards novos. **Sem** dispatch novo de skill.

### 4.2 Eixo B — **Sub-arquétipo / "escola"** (o que diferencia duas Kaelis do mesmo papel)

Hoje duas Kaelis do mesmo papel diferem por **elemento + trait + 3 echoes**. O usuário quer mais. A
proposta é tornar **explícito** um eixo de *sub-arquétipo* — um estilo mecânico dentro do papel, que a
trait e o kit já insinuam mas que ninguém nomeou:

| Papel | Sub-arquétipo A | Sub-arquétipo B | (vaga futura) |
|---|---|---|---|
| **Mage** | **Affliction** (DoT/execução) — Velvet | **Wildfire/Spread** (contágio/fields) — Rin | **Smite/Burst** (marca+detona) — Eloa |
| **Archer** | **Skirmisher** (kite, autos leves rápidos, single+slow) — Lunara | **Marksman/Sniper** (autos pesados que **perfuram**, ramp estacionário) — Gaia | **Volley** (multishot em leque) — vaga |
| **Knight** | **Duelist** (combo ramp, foco single + cleave, defesa/parry) — Seren | **Berserker/Brawler** (charge, paralisia AoE, **thorns**, fica mais forte apanhando) — Rynna | **Guardian** (taunt/escudo puro) — vaga |

A diferença entre, por exemplo, **Lunara e Gaia** deixa de ser "ice vs earth": é **Skirmisher móvel de
autos leves e slow** vs **Marksman estacionário de autos pesados que perfuram e rampam**. Elemento
vira a camada cosmética/de reação; o sub-arquétipo é a camada mecânica; trait+echo é a camada de
win-condition. Três camadas empilhadas = duas Kaelis do mesmo papel sentem genuinamente diferentes.

### 4.3 Eixo C (opcional, alto impacto) — **builder/spender por papel** (frame Monk/Combo Points)

Unifica os três papéis sob um mesmo *frame* ("acumula um recurso atacando, gasta num golpe forte"),
mas com **fantasia diferente** em cada — dá um *loop* a quem hoje não tem (archer/knight) sem copiar o caos do mage:

- **Archer** acumula em **autos** → gasta num **multishot/pierce** supercarregado (Combo Points do D4).
- **Knight** acumula em **golpes melee** → gasta num **cleave/nova** (Harmony do Monk; a `discipline`
  da Seren e a `static_charge` da Rynna já são quase isso — formalizar).
- **Mage** acumula em **condições aplicadas** → gasta numa **detonação** (já temos o card `detonate`).

Isso é o ingrediente mais estrutural do doc — **decidir se entra é decisão de escopo** (§8).

---

## 4bis. DIREÇÃO PRINCIPAL — caos vem da profundidade do kit (não de meta-sistema novo)

> Esta é a seção que vale, dada a decisão de direção (manter `element` + `role`). O alvo é **mais
> caos**, e o estudo de caso é **Velvet × Rin**.

### 4bis.1 A primitiva de caos do engine: o *spreading field*

O engine tem **uma** primitiva de caos auto-propagante: o `GroundField` com `SpreadChance > 0`
(`Engine/GameWorld.cs` → `TickFields` / `MakeSpreadChild`). A cada tick, um tile aceso rola pra
**acender um vizinho cardinal livre**, decrementando um orçamento de gerações, até o cap de
`FieldMaxTilesPerFloor = 80`. **Um cast vira uma frente que rasteja pelo mapa** — é isso que lê como caos.

### 4bis.2 Teardown — por que a Rin é caótica e a Velvet não

**Rin usa a primitiva 3× e ainda empilha fogo que pula:**

| Skill | Shape | Fonte de caos |
|---|---|---|
| `contract` | chain | 5 jumps + **burn DoT em cada** → 5 inimigos queimando |
| `hall` | field | seed + **spread 45%/tick, 3 gerações** → fogo que rasteja |
| `infernal-ball` (ult) | barrage | 4 meteoros, **cada um `StrikeLeavesField`** (spread 38%, 2 ger) → 4 incêndios se alastrando |
| `contagion` (trait) | — | burn pula pro inimigo não-queimando mais próximo (na morte / a cada 2s) |

**Velvet usa a primitiva 0×.** Tudo dela é instância única, lugar único, **não se propaga**:

| Skill | Shape | Por que é "contida" |
|---|---|---|
| `curse` | area, raio 1 | uma poça pequena + DoT, **não se alastra** |
| `nightmare` | beam | reta instantânea, **nada fica no chão** |
| `shade` | summon | **um** pulser estático raio 1 (ver 4bis.3) |
| `plague` (ult) | nova, raio 3 | um burst único + DoT |
| `decay` (trait) | — | threshold de execução **invisível**, single-target, zero caos visual |

**A causa-raiz é uma frase:** a Velvet **não tem nenhum field que se alastra** e o summon dela é
**estático**. Ela tem as peças (DoT, summon, nova) mas **nenhuma cresce sozinha**. Não é dano — é
propagação.

### 4bis.3 Abyssal Shade — conceito ótimo, implementação de torre

`TickPlayerSummons` (GameWorld.cs ~L2189) é minimalista: o `PlayerSummon` fica **preso ao tile do
caster** (`X = Player.X, Y = Player.Y`, nunca move), pulsa **dano cru** em raio 1
(`fromSkill:false, canCrit:false, canLifeSteal:false`), **não aplica DoT, não deixa field, não invoca
nada**, e existe **um** só. "Sombra abissal que assombra o campo" virou, no código, uma caveira parada
dando ping raio-1 a cada 0.8s. É o oposto de assombrar.

**Upgrades (sem dispatch novo de shape):**

*Só dado (zero engine):*
- Converter `curse` de `area` → **`field` com spread** (apodrecimento que rasteja, tipo `hall` mas FX
  de morte). A Velvet ganha hazard rasteja na hora.
- Tornar `plague` (ult) um **`barrage` com `StrikeLeavesField`** (igual `infernal-ball`): a praga
  chove e semeia podridão que se alastra onde cai, em vez de uma nova única.
- Subir raio/power/duração do shade (barato, mas não tira a sensação de torre estática).

*Extensão pequena e limitada no `PlayerSummon` — é o que conserta de fato o Shade* (uma mudança no
struct + `TickPlayerSummons`, sem shape novo):
- **A — Shade errante (manchete):** dar ao summon um *drift* em direção ao inimigo mais próximo a cada
  tick + **deixar um field de corrosão que se alastra nos tiles que cruza**. Vira um gerador de caos
  **móvel** que apodrece o chão atrás de si — um sabor de caos **diferente** do fogo de origem-fixa da
  Rin. É a resposta mais fiel ao conceito.
- **B — Enxame:** um param `SummonCount` pra erguer 2–3 shades de uma vez (pulsos sobrepostos = zona
  assombrada). Casa com o echo `harvest` dela (ergue espectro na morte, máx 5).
- **C — Pulso aplica decay + semeia podridão:** o pulso empilha o DoT `decay` e larga um field que se
  alastra embaixo. Amarra o shade à trait.

**Recomendação:** **A (errante + trilha de corrosão)**, opcionalmente + B. É a única extensão limitada
que também **destrava todo summon futuro**, e faz o Shade ler como assombração, não torre.

### 4bis.4 Princípio geral pra "mais caos" nas mages

1. **Toda mage deve ter ≥1 hazard que se alastra** (usar a primitiva spreading-field). Hoje só a Rin tem.
2. **Camadas persistentes sobrepostas** (field + summon + DoT ativos ao mesmo tempo) leem como caos —
   a Velvet tem as peças, mas nenhuma persiste/rasteja.
3. **Condição → detonação** (o card `detonate` já existe): reação em cadeia = caos. Bom tema pra decay.
4. **Multi-instância** (barrage / múltiplos summons) > instância única.
5. **Cuidado com o cap** (`FieldMaxTilesPerFloor = 80`) — mais fontes que se alastram dividem o mesmo
   orçamento; é o regulador que impede a simulação de explodir. Caos **dentro** do cap.

### 4bis.5 As três mages = três **sabores** de caos (não o mesmo caos)

> Atualização: a Eloa **não** vai ficar como "burst simples". O usuário notou que o kit dela parece o
> da Velvet e ainda mais simples — então ela ganha **caos próprio**, de mecânica **diferente**. O
> princípio passa a ser: as três mages são caóticas, mas por **engines distintos**.

| Mage | Engine de caos | Sensação | Primitiva no engine |
|---|---|---|---|
| **Rin** (fire) | **Terreno que se alastra** — fogo rasteja tile a tile | o mapa pega fogo | `GroundField` com spread (já existe) |
| **Velvet** (death) | **Cascata de morte** — mortes geram orbes que matam → mais orbes | a morte bola de neve | spawn-on-kill (`OnMonsterKilledCard` já existe) |
| **Eloa** (holy) | **Cadeia de julgamento** — marcas detonam e pulam para outras marcas + chão consagrado | reação em cadeia de luz | trait `judgment` + `StrikeLeavesField` (já existem) |

Mesmo frame "mage = caos", **três mecanismos diferentes**. É a diferenciação sem meta-sistema novo.

### 4bis.6 Velvet — "Soul Detonation" (a ideia de orbes de morte, expandida)

**Núcleo:** inimigo que morre **sob Decay** (ou pela ultimate) larga um **Death Orb**. A infra já
existe — o echo `harvest` (`OnMonsterKilledCard`, GameWorld ~L2815) **já** ergue um `PlayerSummon` na
morte (máx 5). Death Orb = esse mesmo caminho, promovido a kit base e feito **explodir e encadear**.

Variantes criativas:
- **A — Explosivo (corpse-explosion em cascata) [manchete]:** o orbe paira ~0.6s e **estoura** numa
  AoE de morte. Se o estouro mata mais mobs decaídos, **eles** largam orbes → **reação em cadeia** que
  rola pela pilha. Pico de caos necromante e o mais divertido. Limitado por um **cap de orbes/andar**
  (mesma ideia do cap de fields) pra não explodir a sim. Reusa `_pendingStrikes`/`ScheduledStrike`
  (nova com atraso) — barato.
- **B — Wisps teleguiados:** a alma se divide em 1–3 wisps que voam pro inimigo mais próximo, dão dano
  e **espalham Decay** — a morte infectando as próximas vítimas. Ótima como **echo** (orbes que buscam).
- **C — Coletor (gravity well):** orbes derivam até a Velvet e **carregam a ult / curam** — loop sombrio
  de sustain/snowball (casa com a fantasia de pacto de morte).

**Recomendação:** **A** como assinatura, **B** como upgrade de echo. Mapeia exatamente "mobs que morrem
summonam orbes que dão dano ou explodem".

**Kit Velvet reenquadrado em "death feeds death":**
- `curse` → **field de podridão que se alastra** (chão que apodrece e rasteja)
- `Abyssal Shade` → errante + trilha de corrosão (§4bis.3)
- `plague` (ult) → mortes semeiam **Death Orbs** → cascata
- `decay` (trait) → quanto mais stacks de Decay no cadáver, **maior o orbe / mais orbes**
- `harvest` (echo) → orbes também sobem como espectros errantes = campo cheio de morte

### 4bis.7 Eloa — por que está ainda mais simples, e a identidade dela

**Diagnóstico:** o kit da Eloa é **100% burst instantâneo** — lance (single), judgment (3 lanças num
ponto), radiance (beam), halo (ring), absolution (nova). **Nada fica no chão, nada se propaga, sem
summon, sem field, sem DoT.** É o kit mais "limpo" do jogo — por isso lê chapado ao lado da Velvet. A
trait `judgment` (marca Sin → detona) é um gancho ótimo que o kit **não aproveita**.

**Identidade nova = cadeia de julgamento (caos de luz que *cascateia*, não rasteja):**
- **Detonação em cadeia:** quando um inimigo Julgado detona, **semeia Sin nos próximos e pode
  detonar em cadeia** outras marcas de 3 stacks → cascata de explosões sagradas. (O echo
  `chain_judgment` já semeia Sin — trazer uma versão leve pro base + adicionar o encadeamento.)
- **Chão consagrado:** `judgment` (que é `barrage`) **deixa chão sagrado onde as lanças caem** —
  **puro dado** via `StrikeLeavesField` (field holy, sem/baixo spread). A Eloa ganha persistência:
  zonas consagradas que ferem inimigos (e podem curá-la).
- **Absolution (ult):** em vez de uma nova única, **pilares de luz chovem numa área** (espalhar o
  barrage) + deixam chão consagrado.
- **Opcional — Seraph Blade:** um summon sagrado errante que fulmina (o análogo de luz do Abyssal
  Shade), reusando a mesma extensão de summon errante.

→ Eloa = **burst + marcas que detonam em cadeia + consagração**. Distinta da cascata de cadáveres da
Velvet e do fogo rastejante da Rin.

### 4bis.8 Ajustes finos de skill (feedback direto)

> Refinamentos por skill levantados na revisão. O direcionamento geral já está fechado; isto detalha
> skills específicas que leem fracas/genéricas hoje.

**Beam universal + card "Beam Width" (ótimo gancho).** O case `beam` (GameWorld ~L1969) é uma **linha
de 1 tile** — só checa `MonsterAt(tx,ty)` por passo, **sem param de largura**. Decisão: **manter beam
em todas as mages** e criar um card **"Beam Width"** (param `BeamWidth`, default 1; o card soma) que
faz a beam atingir os tiles **perpendiculares** (±w) — uma lança vira uma **parede** varrendo a pista.
Um card que escala em **todas** as mages = card de build memorável. Extensão pequena e limitada (somar
os tiles perpendiculares no loop da beam). **Implica:** a **Rin não tem beam hoje** (slots
single·chain·field·cone·ult) → ela precisa ganhar uma (ver Ashen Wings abaixo) pra o card ser universal.

**Eloa — `Halo` (raio pequeno, sem impacto).** Hoje: ring instantâneo (raio 2, inner 1), nada fica.
Vira **Consecrated Halo persistente**: zona sagrada que pulsa dano, **cura a Eloa enquanto ela fica
dentro**, e **detona/semeia Sin** na área. Conserta o "sem impacto" **e** alimenta a identidade de
cadeia de julgamento (reusa a primitiva de chão consagrado, §4bis.7).

**Rin — `Hall of Flames` (gosta do spam, mas muitos ticks fracos + área larga demais → fraca).** Hoje:
seed raio 1, tick 700ms por 5s (~7 ticks) a só **0.40/tick**, spread 45% por 3 gerações (alastra rumo
ao cap de 80 tiles). É **diluída** — cobre meio mapa mas cada tile mal arranha. Fix = **concentrar**:
menos ticks mais **fortes** (subir power/tick, alargar o intervalo) + spread mais **apertado** (menos
gerações / chance menor) → um **incêndio denso e quente** que ainda spamma fogo visualmente mas
**ameaça**. Footprint menor, impacto maior.

**Rin — `Ashen Wings` (cone chapado, "tosco").** Converter na **beam da Rin**: uma **"Ashen Breath"**
(beam de fogo) que **incendeia o chão que cruza** (deixa fogo que se alastra). Uma mudança resolve três
coisas: (a) dá à Rin a beam pro card de largura, (b) mata o cone genérico, (c) reforça a identidade de
fogo rastejante. *Alternativa (se quiser manter um cone): o cone deixa fogo no footprint — mas a
conversão pra beam é a jogada mais forte, dado o card de largura.*

**Velvet — `Abyssal Shade`:** refactor **mantido e travado** (errante + trilha de corrosão,
§4bis.3 / §4bis.6). Não esquecido.

---

## 5. Detalhe por arquétipo

### 5.1 MAGE — "Chaos / Zone control" (linhagem Sorcerer + Druid)

**Identidade:** auto fraco e lento; o poder está em **pintar o mapa** com fields, DoTs, beams e novas
sobrepostas. Dano flui para `dot` + `aoe`. Win condition = empilhar hazards/condições até a tela derreter.

**Manter:** fields que se alastram (contágio da Rin), DoT que rampa (decay da Velvet), beams/retas,
novas, summon.

**Empurrar:**
- **Engine de detonação** como tema de mage: condições (burn/curse) podem ser **detonadas** em burst
  AoE (o card `detonate` vira mecânica de escola, não só um rare avulso).
- Cada mage deixa **algum hazard de chão** temático ao elemento (já quase universal — formalizar).
- Card line de mage: "+% `dot`", "+% `aoe`", "condição dura +X", "detona ao expirar", "fields se
  alastram +1 geração".

**Diferenciar as 3 mages (escolas explícitas):**
- **Eloa = Smite/Burst** (holy): marca Sin → detona em nova sagrada + cura. Burst limpo, sustain.
- **Velvet = Affliction** (death): decay/curse field/summon, execução por threshold. DoT paciente.
- **Rin = Wildfire/Spread** (fire): fogo que pula e fields que crescem. Caos que se auto-propaga.

### 5.2 ARCHER — "Auto-attack / sustained DPS" (linhagem Paladin)

**Identidade:** **o auto-attack É o kit.** As skills são *enablers* do auto (haste, marca, pierce,
multishot) + algum AoE. Dano flui para `auto`. Cadência rápida, maior alcance. Segue Hades: archer
quer **procs/fontes adicionais on-hit**, não % multiplicativo (que escalaria demais num auto rápido).

**Adicionar (o coração do pedido do usuário):**
1. **Steroid de attack speed dedicado** em todo kit de archer (um slot `buff` de haste próprio, além
   do haste situacional da trait). Inspiração: *Ferocious/Accelerating* do D4.
2. **Pierce** — rider novo `Pierce` para `single`/`beam`: o projétil atravessa N alvos em linha
   (Penetrating Shot). Identidade da Gaia/Marksman.
3. **Multishot** — disparar N projéteis num **leque/cone** (Barrage) **ou** sequencial (shape
   `barrage` já faz sequencial). Identidade da Volley (vaga futura) e do *spender* de archer.
4. **Builder/spender de combo** (§4.3): autos geram combo points → spender = multishot/pierce com
   crit garantido.
5. **Status que premia tiro sustentado** — ex.: **Sunder/Armor-break**: cada auto reduz a armadura do
   alvo por X s (stack), então quanto mais você atira no mesmo alvo, mais dói. Recompensa a fantasia
   de "metralhar" e dá ao archer um status próprio (frost já é da Lunara; sunder seria da Gaia/Marksman).

**Card line de archer (nova):** "+% `auto`", "+% attack speed", "autos perfuram +1", "todo N-ésimo
auto vira multishot", "+attack speed ao matar", "+attack speed rampa enquanto atira o mesmo alvo",
"armor-break aplica +1 stack".

**Diferenciar Lunara × Gaia (a pergunta direta do usuário):**
- **Lunara = Skirmisher** (ice): autos **leves e os mais rápidos**, foco single-target + **slow**,
  hit-and-run; attack speed vem de **mobilidade** (haste no dash/shatter). Status: **frost (slow)**.
- **Gaia = Marksman/Sniper** (earth): autos **pesados que perfuram** (pierce em linha), **ramp
  estacionário** (prey: quanto mais tempo caçando, mais dano + multishot quando a prey morre).
  Status: **sunder/armor-break**.

→ Resposta à pergunta "é só elemento ou incluímos status?": **incluir um status por sub-arquétipo**
(frost vs sunder) além do elemento. O elemento fica para reação/cosmético; o **status** é o que
amarra o kit ao loop de auto-attack.

### 5.3 KNIGHT — "Melee bruiser / frontline" (linhagem Knight + Monk)

**Identidade:** melhor em melee; AoE **só de perto** (cleaves/cones/novas ao redor de si — nunca área
à distância); **provoca** para puxar ranged ao corpo-a-corpo; sustenta-se na linha de frente. Dano
flui para `skill`/melee. Cadência média, range 1.

**Manter:** cones com `taunt`, novas com stun ao redor, combo ramp (Seren), charge/discharge (Rynna).

**Adicionar (o que o usuário cogitou):**
1. **Taunt/threat de verdade** — hoje o `taunt` força ranged a marchar; falta um **sistema de aggro**
   para o taunt *segurar* o foco (inimigos continuam batendo no knight por X s). `exeta res` do Tibia
   é o modelo. Pode ser um **slot de provocação AoE** dedicado + um threat curto.
2. **Reflect / Thorns** — stance defensiva que **reflete** % do dano melee recebido como burst AoE ao
   redor (Thorns Berserker do D4). Combina mais com a **Rynna** (apanhar carrega a `static_charge`).
3. **Berserker mode** — modo de raiva (HP baixo *ou* recurso acumulado) que sobe attack speed/dano +
   lifesteal. Combina com a Rynna (impetuosa). A Seren prefere **defesa/parry** (Immortal Stance já é
   echo dela).
4. **Sustain melee** — lifesteal/escudo enquanto está em melee (fantasia de bruiser que aguenta a linha).
5. Formalizar **builder/spender** (§4.3): golpes melee acumulam → spender = cleave/nova grande.

**Card line de knight (nova):** "+% melee/`skill`", "reflete +% do dano recebido", "taunt dura +X",
"-% dano recebido enquanto provoca", "lifesteal em melee", "berserker abaixo de X% HP".

**Diferenciar Seren × Rynna:**
- **Seren = Duelist** (physical): combo ramp (discipline), single + cleave, **defesa/parry** (reduz
  dano com combo alto). Bruiser de **precisão**.
- **Rynna = Berserker/Brawler** (energy): charge/discharge, **paralisia AoE**, **thorns** (apanhar
  carrega), berserker mode. Bruiser de **caos/controle**.

---

## 6. Resposta consolidada: "o que muda entre duas Kaelis do mesmo papel?"

Quatro camadas, da mais barata à mais definidora:

1. **Element** — cosmético + reações elementais + FX de field. (já existe)
2. **Sub-arquétipo / escola** — o estilo mecânico dentro do papel (§4.2). **(eixo novo a formalizar)**
3. **Status/keyword aplicada** — atrelada ao sub-arquétipo (frost/sunder/decay/burn/charge/sin/prey).
   **(parcialmente existe via trait; tornar deliberado)**
4. **Signature trait + 3 echoes** — a win-condition da run. (já existe)

E, **transversal a tudo**, o **damage-tag** (§4.1) define em qual "tipo de dano" a build daquela Kaeli
investe (auto/skill/dot/aoe/ultimate) — o equivalente enxuto aos "tipos de dano" da WuWa.

---

## 7. Ideias extras (backlog de brainstorm)

- **Attack speed como stat de primeira classe** com cap de retornos decrescentes; archer encosta nele,
  mage/knight quase não. (lição Hades)
- **Sistema de threat/aggro** mínimo para o taunt do knight ter dente (sem ele, "provocar" é cosmético).
- **Detonation engine** para mage (condição expira/é consumida → explode). Vira tema de escola.
- **Sunder/armor-break** como status de archer-sniper (premia tiro sustentado).
- **Counter/parry** para o duelist (Seren) — janela de reflexo em vez de thorns passivo.
- **Overcharge/Serene-like**: estado de pico temporário (Focus Serenity do Monk) como ult alternativa
  de knight — reseta cooldowns + recurso cheio por X s.
- **Boon-style card synergy**: cards que mudam de valor conforme a cadência (cards "%" valem mais para
  mage lento; cards "proc on-hit" valem mais para archer rápido) — empurra picks divergentes.
- **Dash já é por papel** (Knight cleave / Archer sprint / Mage trail) — manter; é um ótimo precedente
  de "mesma tecla, identidade por papel" que o damage-tag e o sub-arquétipo estendem.

---

## 8. Decisões em aberto (bater o martelo antes de virar roadmap)

**Direção fechada:** manter `element` + `role`; investir em **caos/profundidade de kit** (§4bis); foco
**só nas mages por enquanto** (Velvet e Eloa primeiro). As três mages ganham **engines de caos
distintos** (§4bis.5): Rin=terreno, Velvet=cascata de morte, Eloa=cadeia de julgamento.

1. **Death Orb da Velvet — qual variante (§4bis.6)?** **A** explosivo/cascata (recomendado),
   **B** wisps teleguiados (melhor como echo), **C** coletor que cura/carrega ult. Pode ser A no base + B no echo.
2. **Disparo do orbe:** só em morte **pela ultimate**, ou em **qualquer morte sob Decay** (mais caótico,
   amarra na trait)? Recomendação: qualquer morte sob Decay, com **cap de orbes/andar**.
3. **Extensão do `PlayerSummon`:** topa a mudança limitada (Shade errante: drift + leaves-field, e
   talvez `SummonCount`)? É a peça que **não** é puro dado — e destrava o Death Orb (B/C), o Seraph
   Blade da Eloa e todo summon futuro.
4. **Eloa — escopo (§4bis.7):** detonação-em-cadeia + chão consagrado (recomendado) — entra junto com a
   Velvet ou logo depois? O chão consagrado é **puro dado**; o encadeamento de marcas é lógica nova na
   trait.
5. **Cap de caos:** `FieldMaxTilesPerFloor = 80` e um futuro cap de orbes são por-andar/compartilhados.
   Em co-op/raid isso precisa virar por-jogador? (anotar; fora de escopo agora.)
6. **Archer/Knight ficam pra depois** (confirmado: foco nas mages). Viram ondas seguintes.

---

## 9. Próximos passos (sem código ainda)

1. Você fecha as decisões da §8 (principal: o nível de upgrade da Velvet + se topa a extensão do summon).
2. Com isso fechado, converto num roadmap executável (`docs/roadmap/...`, prompts NN auto-contidos) via
   a skill `roadmap-from-plan`.
3. Ordem sugerida de ondas (foco mages primeiro):
   - **(W0) Velvet — cascata de morte:** `curse`→spreading rot field, `plague` ult→barrage-com-field,
     **Abyssal Shade errante + trilha**, e o **Death Orb** (Soul Detonation). Inclui a extensão do `PlayerSummon`.
   - **(W1) Eloa — cadeia de julgamento:** chão consagrado (puro dado via `StrikeLeavesField`),
     detonação-em-cadeia de marcas Sin, pilares no ult, **`Halo` → Consecrated Halo persistente**.
     (Seraph Blade opcional, reusa a extensão da W0.)
   - **(W2) Rin — apertar o fogo:** `Hall of Flames` **concentrado** (menos ticks/mais fortes, spread
     apertado), `Ashen Wings` → **beam "Ashen Breath"** que incendeia o chão. Dá beam à Rin.
   - **(W3) Card "Beam Width" + universal:** extensão `BeamWidth` no case `beam` + card novo; escala em
     todas as mages (Eloa/Velvet/Rin já com beam após W2). Depois: balance das três mages na Training
     Room + BalanceSim; revisar teto da Rin agora que não é a única caótica.
   - **(W4+) Archer** (auto-attack: pierce/multishot/attack-speed steroid) → **Knight** (taunt/threat
     real + thorns/berserker). Fora do foco atual.

---

### Fontes (pesquisa de mercado)

- Tibia — vocações: [TibiaWiki Vocation](https://tibia.fandom.com/wiki/Vocation), [Monk vs Druid (TibiaBuddy)](https://www.tibiabuddy.com/blog/monk-vs-druid-guide)
- Tibia — Monk/Harmony (builder-spender, Serene): [TibiaWiki Monk](https://tibia.fandom.com/wiki/Monk), [TibiaWiki Harmony](https://tibia.fandom.com/wiki/Harmony), [Monk Guide 2026 (TibiaBuddy)](https://www.tibiabuddy.com/blog/monk-vocation-guide)
- Diablo IV — Rogue (pierce/multishot/attack speed/combo points): [Penetrating Shot (Icy Veins)](https://www.icy-veins.com/d4/guides/penetrating-shot-rogue-build/), [Barrage Rogue (Maxroll)](https://maxroll.gg/d4/build-guides/barrage-rogue-guide)
- Diablo IV — Thorns Berserker (reflect/taunt): [Thorns Berserker (GameRant)](https://gamerant.com/diablo-4-thorns-berserker-build-skills-role-gameplay-bosses/)
- Hades — boons/cadência/identidade de arma: [Best Boons Every Weapon (GameRant)](https://gamerant.com/hades-best-boons-every-weapon/), [Hades build guide (Reamsnyder)](https://www.leereamsnyder.com/hades-build-guide)
- Wuthering Waves — tipos de dano/combate: [Combat System Guide (Game8)](https://game8.co/games/Wuthering-Waves/archives/452894), [All Damage Types (Game8)](https://game8.co/games/Wuthering-Waves/archives/599638)
