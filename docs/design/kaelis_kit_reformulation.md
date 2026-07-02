# Reformulação dos Kits das Kaelis — Decisões de Design

> **Status:** decisões de design **travadas** nesta conversa (2026-06-30). **Nada implementado ainda.**
> Este doc é o *decision record* que fecha as "decisões em aberto" do
> [research doc](kaelis_combat_archetypes_research.md) (§8) e vira a base do redesign Kaeli-a-Kaeli.
> Texto em PT (material de revisão); **todo nome/termo/ID de jogo fica em inglês** (política de idioma).
> Os nomes de referência usados na discussão (Olaf, Master Yi, Tiamat, etc.) **não entram no jogo** —
> são só atalho de conversa.
>
> Docs relacionados: [research por arquétipo](kaelis_combat_archetypes_research.md) ·
> [snapshot dos kits atuais](kaelis_kits_summary.md).

---

## 0. TL;DR — o que mudou

1. **Identidade = `element` + modelo 2×2 (`range` × `pattern`).** Os roles `Mage/Archer/Knight`
   saem; o que define a Kaeli é o cruzamento de dois eixos ortogonais.
2. **`range` (melee/ranged) volta a ser mecânico** — mas como **modelo de sobrevivência e
   posicionamento**, não como alavanca de dano. Mapa é mobado, então "melee bate mais / ranged bate
   mais" não funciona.
3. **`pattern` (auto/caster)** = onde o valor se concentra: single-target/boss vs AoE/pack.
4. **Slot 0 deixa de ser um single-target genérico** (redundante com o auto).
5. **Traits = passiva-assinatura da Kaeli: simples, uma coisa, dá identidade.** A ult fecha o loop da
   trait.
6. **Reactions elementais: cortadas.**
7. **Barrage:** separar *cadência de impactos* de *tamanho de cada impacto*.
8. **Berserk = mecânica global (nova, estilo Wakfu):** HP < Y% → +X% de dano. Vira o "sistema
   universal" do combate (no lugar das reactions); a Rynna é a especialista.
9. **Casos-piloto prontos:** **Seren** (melee auto), **Rynna** (melee caster / vampiric berserker),
   **Lunara** (ranged auto / frost marksman), **Gaia** (ranged auto / serial hunter), **Eloa** (ranged
   caster / AoE queen), **Rin** (ranged caster / DoT com ritmo) e **Velvet** (ranged caster / necromante
   das almas) — **quadrantes `auto` e `ranged caster` fechados** (7/7 do roster atual desenhado).
10. **`auto` ≠ single-target.** "auto" = o dano vem do **autoattack** (não das casts); "forte em boss" é
    *tendência*, não definição. Um auto pode ser **AoE-leaning** (cleave/pierce/splash) e seguir sendo auto.
11. **Casters ranged têm regras estruturais próprias** (§2.7): beam universal sempre no s3, barrage
    confinado ao ult, chain/cone fora do vocabulário delas, field não é obrigatório (o dash já dá um
    rastro elemental fraco de graça).

---

## 1. Modelo de identidade — 2×2 (`range` × `pattern`)

Dois eixos ortogonais. Identidade = `element` + (`range` × `pattern`).

| | **auto** (single-target) | **caster** (AoE) |
|---|---|---|
| **ranged** | marksman / sniper → **Lunara, Gaia** | artillery mage → **Eloa, Velvet, Rin** |
| **melee** | duelist / skirmisher → **Seren** | brawler / battlemage → **Rynna** |

- Não precisa popular os 4 quadrantes igualmente. Personagens futuros preenchem.
- `Vanguard` **não** era um terceiro role — era só **melee + caster** (Rynna).

### 1.1 Eixo `pattern` — auto vs caster (onde o valor se concentra)

- **auto** = dano vem do **autoattack** (não das casts). Forte em **boss** é *tendência*, **não**
  sinônimo de single-target — os autos podem ser **AoE-leaning** (cleave/pierce/splash/cascata) e ainda
  ser "auto". O que muda do caster é a **fonte** do dano (golpe vs cast), não a forma. Ferramentas:
  on-hit, attack speed, lifesteal, steroids, **auto-modifiers**. Pode ter 1–2 habilidades caster.
  - **Armadilha (corrigida com a Lunara):** "auto-Kaeli" tende a sair single-target demais. Num mapa
    mobado e com o **helper que orbita e despeja AoE** (`TickHelperMobbing`), um auto single-target
    **briga com o próprio autopilot**. O clear de pack do auto-Kaeli tem que vir do **próprio auto**
    (Seren: cleave; Lunara: pierce + cascata de frost), não de virar caster.
- **caster** = dano vem das **skills/casts/AoE**; forte em **pack**. Ferramentas: áreas, fields,
  beams, setup→payoff, cooldowns. Auto fraco de propósito.
- **Cada um precisa de um modo secundário** pra não penar fora do nicho:
  - auto num pack → as 1–2 habilidades de AoE/clear (sem isso, auto puro sofre em trash);
  - caster num boss → **uma** ferramenta single-target (o cast do slot 0 + a trait).

### 1.2 Eixo `range` — melee vs ranged (sobrevivência + posição, **não** dano)

Correção importante: como o mapa é **mobado**, todo mundo acaba no meio — não dá pra balancear por
dano de melee/ranged. A diferença é **como cada um sobrevive e se posiciona na pilha**:

- **Melee = fecha box e tanka/ataca no box.** Planta, deixa os mobs virem, segura e cleava no meio.
  Recompensa **ficar parado, tankar e estar cercada**: sustain, cleave que escala com nº de inimigos
  em volta, retaliação, durabilidade. **HP alto.**
- **Ranged = moba e lura enquanto caça os mobs.** Orbita, puxa o trem, derrete da borda, nunca deixa
  encostar. Recompensa **mobilidade e não ser cercada**: slow pra manter o trem agrupado, AoE que cai
  no aglomerado. **HP baixo**, e **morre se for encurralada** — essa é a fragilidade dela.

**Regra de ouro:** o kit tem que **premiar o que o helper já faz com aquele range**. O engine já
implementa os dois comportamentos:
- melee → `TickHelperBox` (planta e cleava) — `Engine/GameWorld.cs` (~L1278);
- ranged → `TickHelperMobbing` (orbita o centro a raio 3 e despeja AoE) — `Engine/GameWorld.cs` (~L1767).

Ferramentas que já existem espalhadas e que viram **contrato de role**: `deadeye` (crit por
distância, ~L2455) é a semente do "ranged premia distância"; `discipline` da Seren (ramp por ficar no
mesmo alvo) é a semente do "melee premia permanência".

---

## 2. Regras de kit (valem pra todas as Kaelis)

### 2.1 Slot 0 — função, não "single genérico"
Um single-target de CD baixo é redundante com o autoattack (slot morto, ainda mais num jogo de
**helper/autopilot** onde o "reset de auto" é invisível). Slot 0 vira **função por arquétipo**:
- **auto (Striker):** um **auto-modifier** (autos empoderados / ganham área / efeito on-hit), não um nuke.
- **caster:** um **cast single-target modificado** (que faz algo — aplica a marca da trait, executa
  marcado), não um spam.
- **brawler melee:** idem cast modificado; **sem gap-closer** (o **dash/blink é universal**, já cobre engage).

### 2.2 Traits = passiva-assinatura, **uma coisa só**
- Cada Kaeli tem **UMA** passiva que é o motor da rotação e **dá identidade**. Riders extras descem
  pras skills ou são cortados. (Hoje várias fazem 3 coisas — ex.: Velvet `decay` = DoT + sobe
  threshold de execução + Death Orb.)
- **Simplicidade obrigatória.** Nada de passiva complexa estilo Wuthering Waves.
- A **ult fecha o loop da trait** (consome/amplifica o estado dela), em vez de ser "um nova maior".

### 2.3 Barrage — separar cadência de tamanho
A graça da barrage é a **sequência de impactos**, não cada impacto ser uma bomba. Hoje as áreas são
grandes demais (ult tem **piso de raio 2** em `SkillRadius`, `Engine/GameWorld.cs` ~L1004 → diamante
de ~13 tiles, repetido **por strike**). Direção:
- impactos **pequenos/precisos** (raio 0–1); o diamante grande fica reservado a shapes dedicadas (area/nova) e em menor número;
- rever **quanto a barrage alimenta a trait** — hoje **cada strike conta como hit direto** e empilha a
  passiva N vezes por inimigo (`ResolveStrikeBody` usa `fromSkill:true`, `Engine/GameWorld.cs` ~L2314).

### 2.4 Reactions elementais — **cortadas**
Some o sistema inteiro: `Domain/ElementReactions.cs`, as chamadas
`ApplyElementMarkAndReactions`/`TriggerReaction`/`DealReactionDamage` (`Engine/GameWorld.cs` ~L3221+),
os campos de marca no `Actor` (`ElementMark*`), e revisar cards/equipamento que referenciem
elemento/marca pra não ficar referência morta.

**Por quê:** uma Kaeli é **mono-elemento** (todas as skills + auto são o mesmo elemento), então ela
**nunca dispara reaction sozinha** — só renova a marca. A única forma hoje é equipar uma **arma de
elemento diferente** do kit (o auto usa `EquipmentStats.WeaponElement`, ~L1881). Ou seja, o sistema
já era **inerte numa run solo** e dependia de um item específico. Mais simples cortar.
Os elementos seguem existindo (cor, FX, resistência de mob) — só some a **interação entre elementos**.

### 2.5 Nota técnica — modelo de "hit direto" (proc)
`directHit = (fromSkill || canCrit) && !fromTrait` (`Engine/GameWorld.cs` ~L2447). Quem alimenta trait:
- **auto** e **hit primário de skill** → `directHit = true` (1 proc por inimigo por cast — ok);
- **barrage** → `directHit = true` **por strike** (N procs — ver §2.3, ponto de atenção);
- **pulsos de summon/field e ticks de DoT** → `directHit = false` (**não** alimentam trait — bom, evita cascata);
- **bursts de trait** usam `fromTrait:true` → não se re-disparam (sem recursão; Death Orb tem teto/andar).
> Conclusão: o medo de **cascata/recursão** já está em grande parte **blindado**. O problema real é
> identidade, não bug.

### 2.6 Berserk — mecânica global (nova)
Entra como o **sistema universal** do combate, no lugar das reactions cortadas. Regra simples estilo
**Wakfu**: **qualquer Kaeli com HP < `BerserkHpThreshold` ganha +`BerserkDamageBonus`% de dano.** Mora
em `Domain/GameConfig.cs` e aplica num **único ponto** (caminho de dano do player, `DealDamageToMonster`).
Pra maioria das Kaelis é um **comeback** quando caem de vida; a **Rynna** é a especialista (kit + preset
de helper tunados pra *viver* nele). Knobs a decidir:
- **threshold único** (ex.: < 40% → +X%) **vs gradiente** (bônus cresce conforme o HP cai — mais "Sacri");
- `X` modesto, pra ser empurrão e não virar playstyle obrigatório pra todas;
- o **threshold de auto-heal do helper por Kaeli** decide quem **acampa** o Berserk (só a Rynna) e quem
  só cai nele sem querer.

### 2.7 Casters ranged — regras estruturais (Eloa / Rin / Velvet)
Surgiram pensando as 3 magas juntas (não uma a uma) — evita o "chassis clone" dentro do próprio
quadrante `ranged caster`:
- **Beam é universal, sempre no slot s3.** As três mantêm um beam de linha; convenção de posição fixa
  (facilita padronização entre elas).
- **Barrage confinado ao ult.** Nenhuma caster ranged tem barrage fora do ult (Eloa tinha 2 — Judgment
  virou `ring` pra resolver; ver §4E).
- **Chain e cone ficam fora do vocabulário das 3 magas** (decisão de mesa, não regra rígida de
  framework): `chain` já é a assinatura da Rynna (melee caster) — repetir tira identidade; `cone`
  descartado por preferência.
- **Field não é obrigatório em toda caster.** O dash já deixa um **rastro de field fraco** no elemento
  da própria Kaeli pra qualquer maga (`DashScorchTrail`, `GameConfig.cs` ~L152-172: dano fraco
  `DashTrailFieldAtkScale=0.18` vs ~0.40 de um field de cast, não-espalhante, curto — "dash nunca
  substitui o cast"). Isso libera quem **não** quer ser "terreno" (Eloa foi pro burst puro, sem field
  dedicado) — só quem tem terreno como identidade central (Velvet) dobra a aposta com slot(s) próprios.
- **Eixo tamanho×cadência do barrage-ult — 3 variações descobertas nas 3 magas** (evolução do §2.3):
  - **grande + rápido** (exceção deliberada): Eloa — a "AoE queen" quebra a proporção inversa clássica
    de propósito, pra ser o polo mais espetacular ("tudo explode junto"); compensa no ajuste fino de
    custo/cooldown, não nesta fase de design.
  - **multiplicador ao longo do multi-hit** (não tamanho): Rin — cada impacto da barrage empilha um
    multiplicador que amplifica o dano de burn da sala por um tempo, sem consumir o burn.
  - **grande + lento, setup→payoff**: Velvet — poucos impactos; o ult força a detonação imediata de
    todos os Death Orbs pendentes (gerados pela passiva Decay a run toda), cobrando de uma vez o que
    normalmente só estouraria com o timer (§4G).

---

## 3. Renomear os roles

`Mage/Archer/Knight` saem em favor do 2×2. **É seguro renomear o enum:** `WaifuDef.Role` é
`[property: JsonIgnore]` (`Domain/Waifus.cs` ~L53) → **não é persistido por conta**, não precisa de
migração. (Nomes finais a definir: `Striker/Caster` no eixo pattern, `melee/ranged` no eixo range, ou
o que soar melhor. IDs `waifu:*`/`skill:*` continuam estáveis — só o display muda.)

Reclassificação que **resolve o par idêntico Seren×Rynna** (hoje os dois kits são quase o mesmo
chassis `single → cone(taunt) → chain → buff → nova`):
- **Seren → melee auto (duelist).**
- **Rynna → melee caster (brawler).**

---

## 4. Caso-piloto — Seren (melee auto / duelist)

**Identidade:** duelista que se compromete com um alvo e cresce; sobrevive **fechando box** e
limpando pack com janelas de cleave. Forte em **boss** (single-target). Sem gap-closer (dash cobre),
**sem "sobreviver ao golpe letal"** (nada de undying). Referência de *feel* (só conversa): agressão
pura tipo Olaf/Master Yi.

| Slot | Nome (placeholder, EN) | Shape/efeito | Papel no kit |
|---|---|---|---|
| **Passive** | **Discipline** | bate no mesmo alvo → ramp de dano; trocar de alvo **zera** | identidade: premiar foco *(termo já existe no código)* |
| **s0** | **Astral Sweep** | os próximos **3 autos** golpeiam em **área**; **reset por kill** (volta a 3 ao abater, cap 3) | motor de **clear de pack** |
| **s1** | **Star Lance** | **estocada em linha** de 3 sqm; área pequena → **dano alto** | poke direcional / furar fila |
| **s2** | **War Cadence** | **attack speed + lifesteal** por alguns segundos | **sobrevivência de box** (sustain) |
| **s3** | **Duelist's Call** | **cone que provoca (taunt)** + **2 tempos** de dano ao redor dela | puxa a pilha pro box + recompensa estar cercada |
| **Ult** | **Zenith** | por alguns segundos: ramp vai ao **máximo e não zera ao trocar de alvo** + **todo hit é Perfect Cut** | fecha o loop da Discipline; serve em pilha **e** boss *(nome já existe no código)* |

### 4.1 Racional das decisões-chave da Seren
- **s0 reset por kill** é o que resolve o "sempre ligada no endgame": a Astral Sweep **se liga sozinha
  em pack** (kills resetam) e **se desliga sozinha em boss** (ninguém morre → cargas acabam) →
  reforça automaticamente "auto = single/boss, cleave = pack".
- **Ult não pode ser single-target puro** (seria inútil em sala de mob = 90% da run). Solução: o ult
  **tira a trava da passiva** (ramp não zera ao trocar de alvo). Boss = deletador; pilha = a sala leva
  o dano de boss dela, cleavando com a Astral Sweep no dano máximo.
- **Ult ≠ s2:** eixos diferentes — s2 = *attack speed + lifesteal* (velocidade/sustain), ult = *crit
  garantido + ramp solta* (dano/burst). Os dois funcionam em pilha e boss.
- **Loop fechado:** dano extra do ult → mais kills → mais resets da Astral Sweep → mais cleave.
- **Os três se amarram:** s0 entrega o cleave, a passiva dá a ramp (presa a 1 alvo), o ult **solta** a
  ramp pra bater forte em todos.

### 4.2 Pendências menores da Seren
- Lifesteal só no s2, ou um pouquinho também na passiva (pra não derreter quando s2 está em cooldown)?
- Querer ou não um **status no cast do ult** pra ele *sentir* impacto numa sala (candidato preferido:
  *kills durante o ult refrescam a Astral Sweep*; alternativa: stagger/expose em volta). Não obrigatório.

---

## 4B. Caso-piloto — Rynna (melee caster / vampiric berserker)

**Identidade:** brawler vampírica inspirada no **Sacrier de Dofus** — quer o caos, **abraça** o dano,
suga vida da multidão e fica mais forte ferida. `caster` = o dano mora nas skills/AoE; o **buraco de
single-target é preenchido pela própria passiva** (a marca detona num alvo). Vive em **Berserk** (§2.6).
Elemento: energy. Referência de *feel* (só conversa): Sacrieur de Dofus.

| Slot | Nome (placeholder, EN) | Efeito | Papel no kit |
|---|---|---|---|
| **Passive** | **Static Charge** | hits aplicam **marca** (o AoE dela espalha marca por toda a pilha); a Charge enche em melee (mais rápido **cercada** / ao **apanhar**); cheia → o próximo golpe **detona a marca num alvo** = **dano single-target** (+ stun curto) | dá o single-target/boss que faltava, **alimentado pelo próprio AoE** |
| **s0** | **Voltaic Claw** | golpe single-target CD baixo + enche Charge | basic / charge-builder |
| **s1** | **Chain Lightning** | **salta** entre inimigos — **dano AoE principal**, com lifesteal | dano de pack + sustain |
| **s2** | **Bloodlust** | buff: **+dano + lifesteal**, mas **toma mais dano** | pico berserker glass-cannon; derruba o HP dela pro Berserk |
| **s3** | **Storm Pull** | **puxa** os inimigos pra perto + **shield** ao engatar (reusa `GainEchoShield`) | junta o box + buffer de mergulho |
| **Ult** | **Storm Heart** | **exori radial em pulsos** (2-3 ondas) em volta; **cada onda detona a Static Charge de todo marcado que pegar** (dano + stun + lifesteal, o mesmo payoff da detonação normal, agora em massa) e **recarrega a própria Charge** a cada onda | fecha o loop de verdade: descarrega de uma vez toda marca que ela espalhou pela sala |

### 4B.1 Racional das decisões-chave da Rynna
- **Discharge = dano single-target, não exori:** ela já tem AoE de sobra (s1 + ult); a passiva detonar
  a marca **num alvo** dá o single-target/boss que faltava — e quanto mais ela espalha marca com AoE,
  maior o payoff da detonação. (Outro exori seria redundante.)
- **Storm Heart fecha o loop da Static Charge (correção de auditoria):** a versão anterior era um exori
  genérico (dano+cura) sem falar da trait — inconsistente com Seren/Lunara/Gaia, cujos ults sempre
  **consomem/amplificam a própria passiva** diretamente. Agora cada onda do ult **detona a Static Charge
  em massa** em vez de só no próximo golpe, e recarrega a Charge a cada onda — a tempestade em ondas vira
  literalmente "descarregar de uma vez toda marca espalhada pela sala", e a cura vem do mesmo lifesteal
  da detonação normal, só que multiplicada pelo número de marcados pegos.
- **Lifesteal mora nas skills de dano** (Chain Lightning + Storm Heart), não numa linha inata — é o que
  a mantém viva no loop "apanhar = Charge".
- **s2 Bloodlust amarra tudo:** toma mais dano → enche Charge mais rápido **e** derruba o HP → entra em
  **Berserk** (§2.6) → o lifesteal raspa de volta. O yo-yo do Sacri num botão só.
- **Contraste com a Seren:** Seren gruda em 1 alvo e **evita** dano (duelista saudável); Rynna fica no
  meio, **abraça** o dano e suga a multidão (Sacri vampírico). Mesmo range (melee), pattern oposto.

### 4B.2 Helper / autopilot (o que destrava o Berserk dela)
A passiva de HP baixo briga com o auto-heal padrão. O preset de helper já carrega o **threshold de
cura** e o **liga/desliga do auto-heal** (`ApplyHelperProfile`/`EncodeHelperProfile`,
`Engine/GameWorld.cs` ~L1385). Preset da Rynna: **auto-heal off** (ou `healPct` ~15-20%) + movimento de
**box/follow**. O lifesteal das skills é a rede de segurança; ficar isolada sem alvo = morte (o risco do Sacri).
**KR-08:** implementado como default da Rynna (`autoHeal=false`, `healPct=20`); perfis salvos pelo jogador
continuam sobrescrevendo esse preset.

### 4B.3 Pendências menores da Rynna
- A **marca** detona só no alvo do golpe (reforça o single-target que faltava) ou também nos marcados
  adjacentes (mais AoE)? — prefiro single-target.
- Números da Berserk (§2.6) e % de lifesteal/curva são knobs de implementação.

---

## 4C. Caso-piloto — Lunara (ranged auto / frost marksman)

**Identidade:** a marksman de gelo — o **dano mora no autoattack**, o **slow é a cola de kite** (não um
rider em toda skill), e a trait **Frostbite** é o motor. **Boss-leaning de propósito:** especialista de
boss/elite; clareia trash "ok, mas não é a mais rápida" (a caster ganha nisso) e **nunca fica indefesa**
graças aos autos que espalham. Elemento: ice. Referência de *feel* (só conversa): Ashe / utito tempo san.

> **Correção de framework que veio com ela:** `auto` ≠ single-target (§0 item 10, §1.1). Lunara **era o
> caso mais quebrado do roster** — "Archer" cujo dano vinha 100% das casts (chain/field/area/nova) e o
> auto era decorativo, com **slow em todas as 5 skills**. A trait dela (Shatter, dá **haste** = attack
> speed) já gritava "auto-attacker" e o kit a desperdiçava castando.

| Slot | Nome (placeholder, EN) | Shape/efeito | Papel no kit |
|---|---|---|---|
| **Passive** | **Frostbite (Shatter)** | autos/gelo **acumulam frost**; o auto baseline **perfura/respinga** um pouco (espalha frost na pilha sozinho); bater em frosted dá **haste**; ao encher → o próximo acerto **estilhaça** (burst que escala com stacks + **cascateia** nos frosted vizinhos, que podem estilhaçar também; consome stacks). **Sem hard-freeze** aqui — só dano + cascata + slow curto | motor de auto; o AoE espalha, os autos colhem a cascata |
| **s0** | **Moonlight Volley** | os próximos N autos **perfuram/dividem** + aplicam frost extra | **auto-modifier**: clear de pack **via auto** (não cast), espalha frost |
| **s1** | **Frozen Garden** | `field` que lenta + frosta — zona de kite pra puxar o trem por cima | **mob-and-lure** (posição/kite) |
| **s2** | **Lunar Focus** | buff: **+attack speed + range** (o "utito tempo") | esteroide de **auto** |
| **s3** | **New Moon** | `nova` de slow/root **ao redor dela** — quebra o cerco e re-estabelece o kite | **peel anti-encurralada** (a fragilidade ranged) |
| **Ult** | **Absolute Zero** | **congela** tudo num raio (o único hard-freeze do kit) e **estilhaça toda a frost de uma vez** (mass shatter escalando com stacks) | fecha o loop: a run inteira empilhando frost, a ult cobra tudo |

### 4C.1 Racional das decisões-chave da Lunara
- **Dano volta pro auto.** Só s1 (kite) e s3 (peel) são casts, e os dois têm **job não-dano** (zona /
  peel) → restrição contra "mais AoE de cast que já temos demais". O dano é auto + Frostbite.
- **AoE-via-auto, não single-target:** o auto baseline perfura/respinga e a **cascata de shatter** propaga
  pelos frosted → autoar o trem acende o pack inteiro, com **textura de auto sustentado** (não burst de
  cast). Casa com o helper que orbita e espalha (`TickHelperMobbing`).
- **Boss = a mesma mecânica concentrada:** num alvo sozinho toda a frost empilha **nele** → estilhaços
  gigantes. Boss-capable **sem** um loop single-target dedicado — força de boss é "o pack tinha 1 inimigo".
- **Hard-freeze só na ult:** a passiva estilhaça com dano + cascata + slow curto; o **congelamento de
  verdade** fica reservado pra ult, pra ela não roubar o clímax.
- **Slow enxuto:** frost = **stacks da trait**; o **slow** mora só onde serve ao kite (s1 + s3). Sai das
  outras (hoje as 5 skills lentam — redundância pura).
- **Paralelo com a Seren (intencional):** ambas auto, mas o **s0 expressa o eixo `range`** — Seren s0 =
  autos viram **área** (cleave de box, melee); Lunara s0 = autos **perfuram em linha** (multishot, ranged).
  Mesmo papel de slot, range oposto. Contraste de pattern: Seren **gruda em 1 alvo** e rampa (foco/box);
  Lunara **espalha frost** e colhe estilhaços enquanto kita (spread/harvest).

### 4C.2 Pendências menores da Lunara
- Quão forte a **cascata** propaga (raio / quantos saltos / falloff) — knob de implementação; tem que
  clarear pack sem virar a melhor clear do jogo (ela é **boss-leaning**, não pack-queen).
- s3 ficou **peel** (não boss-nuke) porque a força de boss já vem dos autos + ult; se faltar punch de
  elite, a alternativa é um **tiro pesado que consome stacks**. Mantido peel por ora.

---

## 4D. Caso-piloto — Gaia (ranged auto / serial hunter)

**Identidade:** a sniper paciente — caça **uma** Prey de cada vez, rampa, executa; a kill **pula a
marca** e acelera a cadência pra próxima. Onde a **Lunara é spray paralelo** (espalha frost na pilha
toda de uma vez, derrete enxame), a **Gaia é cadeia serial** (crava numa prioridade, executa, repete —
sala acelera conforme esvazia). Boss-leaning: a ramp maxa numa caçada longa. Elemento: earth. Referência
de *feel* (só conversa): sniper paciente — o lore dela já é isso ("esperaria uma vida pelo tiro certo").

> **Mesma armadilha da Lunara, ao contrário:** a trait real dela (**Prey**, `Waifus.cs` ~L314,
> lógica em `GameWorld.cs` ~L2657 — dano vs alvo marcado rampa +5%/s até +30%, kill pula a marca +
> ganha attack speed) já é a melhor trait de foco/execução do roster. O kit em volta é **puro
> AoE/CC** (`area` stun, `field` root, `cone`, `barrage` de stun) — a rampa da Prey era desperdiçada
> empurrando a jogadora a pintar a sala em vez de caçar. (Comentário em `Classes.cs` ~L248, "Mineral
> Eye — rewards keeping distance", está **stale** — não existe esse trait kind; a trait real é `prey`.)

| Slot | Nome (placeholder, EN) | Efeito | Papel no kit |
|---|---|---|---|
| **Passive** | **Prey (Hunt)** | marca um alvo; dano vs Prey **rampa com o tempo de caçada** (+5%/s até +30%); kill → marca **pula** + janela de **attack speed** | *sem mudança de fundo — só o kit em volta é reconstruído pra servi-la* |
| **s0** | **Hunter's Aim** | por alguns seg, os autos **travam na Prey** e **perfuram pro vizinho mais próximo** (dano reduzido no 2º alvo) | **auto-modifier**: uptime de caçada + ensina o motivo do chain em escala pequena (setup pro ult) |
| **s1** | **Binding Roots** | `field` que enraíza/lenta | **mob-and-lure**: prende a Prey, peela perseguidores, mantém a caçada viva |
| **s2** | **Coup de Grâce** | tiro pesado, **bônus vs HP baixo**, consome a ramp num burst pra **fechar a kill** | execute — motor do snowball (kill → pula marca → próxima) |
| **s3** | **Monolith Fall** | `area` com **dano real** + stun | piso de dano em pack (não só utilitário) + anti-encurralada |
| **Ult** | **Ricochet** | por alguns seg, os autos **saltam pros vizinhos** (dano **cheio na Prey**, **reduzido** nos demais) + **bônus leve de attack/attack speed** | fecha o loop: a caçada serial vira paralela sem perder o foco no alvo principal |

### 4D.1 Racional das decisões-chave da Gaia
- **Sem slot de esteroide dedicado** (diferente de Seren/Lunara, que têm buff no s2): o esteroide dela
  **é a própria trait** — attack speed e dano vêm de **executar**, não de apertar um botão. O budget
  que seria de buff foi pro **s2 Coup de Grâce** (execute) e pro **s3** (dano real, não só controle).
- **s3 corrigido pra ter dano real:** archer já tem o dash universal como mobilidade/utilidade "de
  graça" — um kit com muito controle e pouco dano obrigaria a compensar tudo no auto. Monolith Fall
  volta a ser uma AoE que **dana e estuna**, não peel puro.
- **Ult = chain no autoattack, não "mass Prey":** a primeira ideia (todo mundo vira Prey) diluía o
  foco que é a graça dela. `Ricochet` resolve melhor: o **autoattack ricocheteia** — dano **cheio** no
  alvo principal (a Prey continua sendo o payoff), **reduzido** nos vizinhos. **Nunca desperdiça em
  boss** (sem vizinho pra saltar, o chain simplesmente não ocorre — dano cheio segue normal). Em pack,
  ela vira AoE **sem abandonar a identidade serial**.
- **s0 ensina o motivo do ult em escala pequena:** Hunter's Aim já perfura pro vizinho mais próximo
  (1 salto); o ult escala esse mesmo padrão pra vários vizinhos + bônus de attack/attack speed. Mesma
  lógica de "kit ensina antes do clímax" da Astral Sweep→Zenith da Seren.
- **Paralelo com a Seren (auto-Kaelis):** os dois ults **removem a trava da trait pra funcionar em
  pack** — Seren (Zenith: ramp não zera ao trocar de alvo), Gaia (Ricochet: deixa de ser "uma Prey por
  vez" sem abrir mão do foco). Mesma linguagem de design pro arquétipo `auto`.
- **Contraste com a Lunara (as duas ranged-auto):** Lunara espalha e colhe cascata (paralelo, enxame);
  Gaia foca e executa em cadeia (serial, prioridade). Mesmo quadrante do 2×2, texturas opostas.

### 4D.2 Pendências menores da Gaia
- **Ult:** kills durante a janela **estendem a duração** (reforça o snowball, como o "kills refrescam a
  Astral Sweep" cogitado pra Seren) ou duração fixa? Não fechado — knob de implementação.
- Quanto o **s0** reduz o dano no 2º alvo e quanto o **ult** reduz nos alvos secundários — knobs de
  balanceamento (o principal deve continuar sendo nitidamente mais valioso que a sobra).

---

## 4E. Caso-piloto — Eloa (ranged caster / AoE queen)

**Identidade:** dano concentrado em explosões grandes e simultâneas — nunca pinga, sempre estoura.
Elemento: holy. Corta a zona lingering (a antiga Consecrated Halo saiu) pra proteger a identidade de
burst puro; o rastro elemental do dash (§2.7) já cobre o "chão reage" residual sem precisar de slot
dedicado. Referência de *feel* (só conversa): julgamento divino, pilares de luz caindo do céu.

| Slot | Nome (placeholder, EN) | Shape | Efeito | Papel no kit |
|---|---|---|---|---|
| **Passive** | **Judgment** | — | Sin acumula por hit; no cap, alvo fica Judged; próximo hit **detona** | *sem mudança — já era 1 coisa só* |
| **s0** | **Judging Lance** | single | dano + **Sin bônus** | acelera o Judged num alvo prioritário (não é spam — §2.1) |
| **s1** | **Dawn Ring** | `ring` | anel de luz **se expandindo** ao redor dela ao longo de ~2-3s (hollow no centro), dano na faixa + Sin | burst self-centered, gradual — evento único, não zona parada |
| **s2** | **Zenith Strike** | `area` | bomba de luz instantânea numa área-alvo + Sin | burst à distância, instantâneo — contraste com o s1 |
| **s3** | **Sacred Ray** | beam | linha | *universal, sempre no s3 (§2.7)* |
| **Ult** | **Absolution** | barrage | **grande E rápido** — muitos impactos, área ampla, tudo estourando junto | o polo "tudo explode ao mesmo tempo" — exceção deliberada ao eixo tamanho×cadência (§2.7) |

### 4E.1 Racional das decisões-chave da Eloa
- **Double-barrage resolvido:** Judgment (era `barrage` no s1) virou Dawn Ring (`ring`, shape que
  nenhuma outra Kaeli usa) — agora só o ult é barrage, cumprindo §2.7.
- **Dawn Ring evoluiu de burst instantâneo pra anel que se expande** ao longo do tempo (mesma técnica
  cogitada pra Velvet como "Ritual Circle" — reaproveitada aqui). Ainda lê como **evento único** que
  acontece e acaba (não uma zona parada tipo Curse da Velvet), preservando a identidade de burst puro.
- **Consecrated Halo cortada:** um field self-centered lingering contradizia "burst puro"; o dash já dá
  o elemento residual no chão (§2.7) sem precisar de slot dedicado.
- **Zenith Strike (`area`, novo)** dá a segunda textura de explosão — instantânea, à distância — em
  contraste com o anel (self-centered, gradual). Duas texturas de burst diferentes, não duas repetidas.
- **Ult Absolution quebra de propósito** o eixo tamanho×cadência clássico do §2.3: fica **grande E
  rápido** (não um ou outro) — o polo mais espetacular, "tudo explode junto". É a exceção que confirma
  a regra: as outras casters seguem a proporção inversa; a Eloa é a queen do caos simultâneo.
- **s0 Judging Lance** segue a regra do §2.1 (cast single-target modificado): aplica Sin bônus, acelera
  o Judged num alvo prioritário — não é spam.

### 4E.2 Pendências menores da Eloa
- **KR-08 fechado:** Dawn Ring expande em 3 bandas agendadas (`radius=4`, delay 250ms, intervalo 650ms);
  smoke test via hub confirmou efeitos em tempos distintos.
- Custo/cooldown do ult pra compensar ele ser grande+rápido ao mesmo tempo — a exceção ao eixo tem que
  doer em algum lugar (mana/CD mais alto), senão ela fica estritamente melhor que as outras casters.

---

## 4F. Caso-piloto — Rin (ranged caster / DoT com ritmo)

**Identidade:** fogo que gruda e se espalha sozinho — mas com botões de agência pra **não virar
"aplica e espera"** (o risco de todo DoT). Elemento: fire. Loop do kit: **semeia** (s0/s1) → a passiva
pinga e salta sozinha → **ult multiplica** o valor de tudo que está pingando → **s2 colhe** o burst no
pico → semeia de novo. Referência de *feel* (só conversa): pacto de fogo, incêndio que cresce sozinho.

| Slot | Nome (placeholder, EN) | Shape | Efeito | Papel no kit |
|---|---|---|---|---|
| **Passive** | **Contagion** | — | hit ignita; burn cura ela (pact); a cada intervalo, o fogo **salta sozinho** entre alvos queimando | *sem mudança* |
| **s0** | **Ember Kiss** | single | dano rápido, **garante ignição** mesmo em alvo frio | starter confiável — não depende de já estar queimando |
| **s1** | **Cinder Storm** | `area` | burst instantâneo numa zona — **ignita todo mundo pego de uma vez** | **seed em massa**: alimenta a passiva sem esperar o salto lento dela |
| **s2** | **Wildfire Reckoning** | `nova` | explosão ao redor dela — dano bônus em **todo mundo já queimando**, escalando com o burn restante, **consumindo-o** (reseta um brasido leve) | **reap**: o botão de "cash in" — dá o momento de agência que o DoT puro não tem |
| **s3** | **Ashen Breath** | beam | linha | *universal, sempre no s3 (§2.7)* |
| **Ult** | **Infernal Ball** | barrage | cada um dos poucos impactos **empilha um multiplicador de dano de burn** em todo mundo queimando na sala; **não consome** — só amplifica o que já está pingando | motor de escalada: o multi-hit da barrage vira o multiplicador (§2.7) |

### 4F.1 Racional das decisões-chave da Rin
- **Chain (Burning Contract) saiu:** era redundante com a própria passiva (que já salta fogo sozinha
  entre alvos queimando) — mesma skill fazendo o que a trait já fazia. Also fora do vocabulário das
  magas por decisão de mesa (§2.7): chain é a assinatura da Rynna.
- **Cinder Storm (`area`, novo) resolve o "DoT chato":** em vez de esperar o salto lento da passiva, o
  jogador pode semear várias tiles de uma vez — agência proativa em vez de timer passivo.
- **Wildfire Reckoning (`nova`, novo) é o botão de "cash in":** dano bônus escalando com o burn
  restante, consumindo-o. Dá o momento de decisão que falta ao DoT puro (quando cobrar o investimento).
- **Ult vira multiplicador de burn empilhado por impacto** (não "campo que cresce depois", ideia
  anterior descartada): como a barrage bate várias vezes, o multi-hit natural dela vira o motor de
  escalada. **Diferencia claramente do s2**: Reckoning consome **agora**; o ult **não consome**, só
  multiplica o que já está pingando por um tempo — dois jeitos distintos de lidar com burn acumulado.
- **s0 Ember Kiss** segue §2.1: garante ignição mesmo em alvo frio, não é spam.

### 4F.2 Pendências menores da Rin
- Números do multiplicador de burn (quanto por impacto, teto, duração) — knob de implementação.
- Ordem ótima de uso entre Reckoning (consome) e o multiplicador do ult (não consome) — provavelmente
  multiplicar primeiro e colher depois é a sequência forte; vale conferir que isso não vire um "jeito
  certo" único demais na prática.

---

## 4G. Caso-piloto — Velvet (ranged caster / necromante das almas)

**Identidade:** necromante que colhe as almas de quem mata. **Não reinventa o que já existe:** a
passiva (Decay + Death Orb) já era a peça certa — hits empilham Decay (DoT + baixa o threshold de
execução), e matar um alvo sob Decay já dropa um **Death Orb** que detona em área depois de um delay.
O único ajuste real do kit é dar ao **ult** agência sobre esse orbe (colher de uma vez em vez de só
esperar), sem inventar mecânica de entidade nova. Elemento: death. Referência de *feel* (só conversa):
necromante que colhe almas.

> **Correção de framework que veio dela (duas rodadas):** a 1ª tentativa (pivô "Syndra", orbe nascendo
> de **hit** direto) inundava o mapa. A 2ª tentativa ("Soul Harvest", orbe de **kill** com dano ambiente
> próprio + 3 fontes de plantio novas) resolvia o flood mas **complicava à toa** — o jogo já tem um
> sistema de orbe funcionando (`Death Orb`) e não precisava de um paralelo. A versão final **mantém o
> Death Orb como está**; a única peça nova é o ult.

| Slot | Nome (placeholder, EN) | Shape | Efeito | Papel no kit |
|---|---|---|---|---|
| **Passive** | **Decay** | — | *sem mudança:* hits empilham Decay (DoT + baixa threshold de execução); kill sob Decay dropa **Death Orb** (delay, detona em área) | *já era a peça certa — mantida como está* |
| **s0** | **Soul Rend** | single | dano single-target, **bônus vs HP baixo** (referência de conversa: Sudden Death do Tibia — finalizador) | cast modificado padrão (§2.1) |
| **s1** | **Cursed Ground** | `field` | zona de dano/slow | terreno — identidade de controle (§2.7); **sem** interação nova com orbe |
| **s2** | **Abyssal Shade** | `summon` | *sem mudança:* shade roaming que deixa rastro de corrosão | *já era a peça certa — mantida como está* |
| **s3** | **Nightmare** | beam | linha | universal, sempre no s3 (§2.7) |
| **Ult** | **Reign of Shadows** | barrage | grande e lento, poucos impactos; **força a detonação imediata de todos os Death Orbs pendentes** no momento do cast (em vez de esperarem o delay normal) | única peça nova: agência sobre o timing do que já existe |

### 4G.1 Racional das decisões-chave da Velvet
- **Não precisava de "Soul Orb".** O `Death Orb` (`GameConfig.cs` ~L1012-1021) já é kill-gated (bounded
  de graça) e já dá a fantasia de necromante. Inventar um sistema paralelo só pra chamar de outro nome
  era complexidade sem ganho — feedback direto do usuário depois de eu ter escrito a versão "Soul
  Harvest" completa.
- **s1 e s2 voltam a ser só o que já eram** (zona de terreno; shade roaming) — cortei o "também plantam
  orbe" que eu tinha adicionado nas duas. Menos fontes de plantio = menos coisa nova pra balancear.
- **O único acréscimo real é o ult ganhar uma função sobre o Death Orb existente:** puxar a detonação
  pro momento do cast em vez de deixar só o timer resolver. Ainda cumpre "grande + lento, setup→payoff"
  (§2.7) — a colheita acontece a run toda (passiva), o ult é o "cobrar tudo agora" — sem exigir cap novo,
  semântica de entidade nova, nem tick ambiente adicional.
- **"Monarca das sombras" não entrou no kit** — ficou só como referência de conversa/flavor, não como
  mecânica (o `summon` shape spawna 1 unidade por cast; uma legião de verdade seria escopo novo).

### 4G.2 Pendências menores da Velvet
- Se o ult "forçar detonação" precisa de algum bônus de dano por orbe estourado junto, ou se só
  antecipar o timer já é suficiente clímax — knob de implementação/playtesting.
- Cap atual (`VelvetDeathOrbMaxPerFloor=5`) segue valendo sem mudança — nenhuma fonte nova de plantio
  foi adicionada, então não há pressão pra revisar o teto.

---

## 5. Banco de ideias de expansão (não usar tudo — sementes)

Vocabulário de skill que falta e daria identidade real (poucos por personagem):
- **Auto-modifiers** (auto): empowered next-attack, every-3rd-hit, pierce temporário, split/multishot, atacar em movimento.
- **Recast / charge / hold:** apertar de novo pra detonar, segurar pra carregar.
- **Defesa ativa** (melee): parry/counter, guard, retaliação ao receber dano, bônus por nº de adjacentes.
- **Setup→payoff** (caster): marca que detona, field com delay, channel.
- **Stance / transformação temporária:** ult que **muda como você joga** por alguns segundos.
- Semente de Kaeli futura: o **sicário/assassin** (burst + execute + reset-on-kill) — brigava com o
  modelo melee=box-and-tank; encaixa melhor como **ranged auto burst** no futuro.

---

## 6. Próximos passos
1. ✅ **Seren** (melee auto), **Rynna** (melee caster), **Lunara** (ranged auto), **Gaia** (ranged
   auto), **Eloa** (ranged caster), **Rin** (ranged caster) e **Velvet** (ranged caster) desenhadas —
   **roster inteiro (7/7) com kit redesenhado.**
2. Definir os **nomes finais dos roles** (eixos pattern/range).
3. Decidir os **números da Berserk** global (§2.6: threshold único vs gradiente, valor de X).
4. ✅ **Roadmap de implementação criado:** [`docs/roadmap/not started/roadmap_kaelis_kit_reformulation.md`](../roadmap/not%20started/roadmap_kaelis_kit_reformulation.md)
   — KR-00 (seams: auto-modifier + ult-estado) + KR-01..KR-07 (um prompt por Kaeli) + KR-08 (balance).
   Tracks globais (remoção das reactions §2.4, Berserk §2.6, retune de barrage §2.3, rename de roles §3)
   ficam **fora** dessa trilha, cada um como roadmap próprio depois que os kits assentarem.

> Lembrete de invariantes (não negociáveis na implementação): backend autoritativo; determinismo do
> tick (só o `Rng` da run); **toda constante de simulação em `Domain/GameConfig.cs`**; IDs `waifu:*`/
> `skill:*` estáveis; skills data-driven por *shape* (parametrizar shape existente, não criar dispatch novo).
