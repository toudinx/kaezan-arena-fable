# Resumo dos Kits das Kaelis

> Snapshot do estado atual do código (`Domain/Classes.cs`, `Domain/Waifus.cs`, `Domain/GameConfig.cs`).
> Documento de referência/revisão. Os números são de balanceamento e mudam por playtest.
> Segregado por **role**: Mages · Archers · Knights.

---

## Como ler este doc

**Identidade = `elemento` + `role`.** O role é o eixo mecânico primário (auto vs skill, cadência,
alcance, AoE). A *fantasia* e o *caos* vêm do kit + da passiva (trait). A arma (`wand`/`bow`/`melee`)
hoje é só cosmética (sprite/missile).

### Tuning por role (`GameConfig.Roles`)

| Role | Auto dmg | Skill dmg | Cadência auto | Alcance | AoE |
|---|---|---|---|---|---|
| **Mage** | 0.75 | **1.15** | 2000 ms (lenta) | 4 | **1.00** (maior) |
| **Archer** | **1.15** | 0.95 | **1400 ms (rápida)** | **5** (maior) | 0.65 (menor) |
| **Knight** | 1.05 | 0.80 | 1700 ms | 1 (corpo a corpo) | 0.80 |

- **Mage:** auto fraco, skill forte, AoE grande, cadência lenta → identidade de **caster de área/caos**.
- **Archer:** auto forte, cadência rápida, maior alcance → identidade de **auto-attack/kite**.
- **Knight:** melhor auto sustentado em melee, AoE média, range 1 → identidade de **duelista corpo a corpo**.

### Geometria das shapes (data-driven, sem dispatch novo no engine)

| Shape     | Geometria                                                                                                                                                          |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `single`  | Projétil/golpe em alvo único.                                                                                                                                      |
| `area`    | Explosão em torno do ponto-alvo (diamante de raio `Radius`).                                                                                                       |
| `cone`    | Leque à frente do caster (largura cresce com `Radius`).                                                                                                            |
| `beam`    | Linha reta que **perfura** tudo à frente (comprimento = `Range`).                                                                                                  |
| `nova`    | Estouro radial centrado no caster (raio `Radius`).                                                                                                                 |
| `chain`   | Salta entre alvos próximos (`ChainJumps`/`ChainRange`), dano cai `ChainFalloff` por salto.                                                                         |
| `field`   | Terreno pintado no chão que pulsa dano (`SummonPower`/`SummonPulseMs`); pode **se espalhar** tile a tile (`FieldSpreadChance`/`Generations`, teto 80 tiles/andar). |
| `barrage` | Vários impactos no ponto (`Strikes` espaçados `StrikeIntervalMs`); pode deixar field em cada impacto (`StrikeLeavesField`).                                        |
| `summon`  | Construto que pulsa dano; pode **vagar** rumo ao inimigo (`SummonRoams`) e deixar rastro de field (`SummonLeavesField`).                                           |
| `buff`    | Buff próprio (atk/atkspeed/haste).                                                                                                                                 |

### Dash por role (Espaço — `GameConfig`)

Cooldown 2500 ms, i-frames 300 ms para todos; o **movimento e o payoff** diferem por role:

- **Mage — Trail:** desliza 3 tiles, **para antes** da 1ª parede/mob e semeia um rastro de scorch fraco
  (DoT 0.18, 600 ms/tick, vida 1600 ms) **do elemento da Kaeli** (Rin=fogo, Velvet=morte, Eloa=holy).
  O rastro **não** se espalha (contágio é identidade de cast, não de dash).
- **Archer — Sprint:** desliza 3 tiles **atravessando mobs** (só para em parede), pousa no tile mais
  distante e ganha **haste** (×1.5 por 1800 ms). Identidade de pura mobilidade/kite.
- **Knight — Cleave (Blink):** **pisca** 2 tiles (pode passar por cima de 1 mob à frente) e detona um
  **nova** no pouso (raio 1, 0.70 atk, elemento `physical` reaction-inert). Burst concentrado ao chegar.

---

# 🔮 MAGES

> Auto fraco / skill forte / AoE grande / cadência lenta. Três motores de caos distintos.
> **Beam em todas** (preparando uma futura carta de "largura de beam").
> Base: Atk 22 · HP 150 cada.

## Eloa — *Seraph of Judgment* (holy)

**Classe:** Seraph · **Trait:** *Seal of Judgment* (`judgment`) — cada hit marca **Sin** no alvo; a 3
stacks ele é **Julgado**: o próximo hit consome a marca num **burst sagrado em área** e **cura** a
Seraph. Espalhar marcas sustenta; focar um alvo detona rápido. *(Sin encadeia para o alvo seguinte —
seed capado, sem cascata recursiva.)*

| Slot | Skill | Shape | Pwr | CD | Alcance/Raio | Detalhes |
|---|---|---|---|---|---|---|
| 1 | **Light Lance** | single | 1.15 | 2.0 s | r5 | Lança precisa de luz. |
| 2 | **Judgment** | barrage | 1.20 | 9 s | r6 / raio1 | 3 lanças em sequência; **cada impacto consagra o chão** (field 0.30, deixa rastro). |
| 3 | **Sacred Ray** | beam | 1.75 | 6 s | linha 5 | Beam sagrado que perfura. |
| 4 | **Consecrated Halo** | field | — | 7 s | raio2 | Consagra o chão à volta da Seraph (luz que pulsa 0.55 por 4 s). |
| **ULT** | **Absolution** | barrage | 2.20 | — | r6 / raio2 | 5 pilares de luz em sequência, **cada um consagra o chão** onde cai. |

## Velvet — *Herald of the Nightmare* (death)

**Classe:** Necromancer · **Trait:** *Accumulated Curse* (`decay`) — cada habilidade empilha **Decay**
(DoT) e **sobe o limiar de execução** (executa <15%, +2%/stack até <25%). Mais curse = alvo estoura
mais cedo. **Necromante:** mobs que morrem com Decay invocam **Death Orbs** (orbe que explode em área,
dano escala com stacks; cap 5/andar, **uma geração só** — sem cascata).

| Slot | Skill | Shape | Pwr | CD | Alcance/Raio | Detalhes |
|---|---|---|---|---|---|---|
| 1 | **Mortal Strike** | single | 1.30 | 2.0 s | r4 | Energia mortal precisa. |
| 2 | **Curse** | field | — | 6 s | r5 / raio1 | Apodrece o chão e **se espalha** tile a tile (30% / 2 ger.), devora quem fica. |
| 3 | **Nightmare** | beam | 1.80 | 8 s | linha 7 | Beam de pesadelo longo. |
| 4 | **Abyssal Shade** | summon | — | 12 s | raio1 | Sombra que **vaga rumo aos vivos** (0.70 por 6 s) e deixa **rastro de corrosão** que se espalha. |
| **ULT** | **Eternal Plague** | barrage | 1.30 | — | r6 / raio2 | 5 impactos de praga, cada um aplica **DoT (4×0.45)** e **semeia podridão que se espalha** (35% / 2 ger.). |

## Rin — *Pact Succubus* (fire)

**Classe:** Pact Succubus · **Trait:** *Contagion* (`contagion`) — hits de fogo **incendeiam** e a
queima **salta** para o inimigo não-queimando mais próximo (na morte do alvo ou a cada 2 s). Cada tick
de burn **cura Rin** um pouco (o pacto). Posiciona para encadear o fogo.

| Slot | Skill | Shape | Pwr | CD | Alcance/Raio | Detalhes |
|---|---|---|---|---|---|---|
| 1 | **Ember Kiss** | single | 1.30 | 1.7 s | r5 | Beijo de brasa rápido, em sucessão frenética. |
| 2 | **Burning Contract** | chain | 1.25 | 7 s | r6 | Pacto salta de inimigo a inimigo (**5 saltos**, falloff 0.15) + **DoT (4×0.30)**. |
| 3 | **Hall of Flames** | field | — | 9 s | r6 / raio1 | **Pira densa concentrada** (0.55 por 3.5 s); lambe pouco nas bordas (18% / 1 ger.). *Reformulado: era um incêndio que enchia o mapa.* |
| 4 | **Ashen Breath** | beam | 1.55 | 6 s | linha 5 | Linha de fogo que perfura tudo à frente. *Reformulado de cone p/ beam.* |
| **ULT** | **Infernal Ball** | barrage | 1.50 | — | r7 / raio2 | 4 meteoros em sequência; **stun 400 ms** + acende o chão que **se espalha** (38% / 2 ger.) até virar fornalha. |

---

# 🏹 ARCHERS

> Auto forte / cadência rápida (1400 ms) / maior alcance (5) / AoE menor. Foco = auto-attack + kite.
> Dash dá **haste** (sprint atravessa mobs).

## Lunara — *Lunar Hare* (ice)

**Classe:** Ice Archer · Atk 20 · HP 205 · **Trait:** *Shatter* (`shatter`) — o gelo dela **lenta**;
bater num alvo já lento dá **dano bônus + haste breve**; o 3º hit no alvo lento o **estilhaça** num
burst e consome a slow. Hit-and-run premia mobilidade.

| Slot | Skill | Shape | Pwr | CD | Alcance/Raio | Detalhes |
|---|---|---|---|---|---|---|
| 1 | **Lunar Cut** | single | 1.30 | 1.8 s | r5 | Estilhaço de luar; **slow 0.7 / 1.5 s**. |
| 2 | **Frost Leap** | chain | 1.30 | 7 s | r5 | Salta entre inimigos (3 saltos), deixa gelo; **slow 0.7 / 1.2 s**. |
| 3 | **Frozen Garden** | field | — | 10 s | r5 / raio1 | Jardim de gelo p/ kitar (0.35 por 5 s); **slow 0.5 / 1.5 s** (não se espalha). |
| 4 | **Crescent** | area | 1.45 | 7 s | r5 / raio1 | Crescente sobre o alvo; corta e lenta à volta (**slow 0.7 / 1.2 s**). |
| **ULT** | **New Moon** | nova | 2.50 | — | raio3 | Onda de frio absoluto à volta; **slow 0.6 / 2.0 s**. |

## Gaia — *Monolith Archer* (earth)

**Classe:** Monolith Archer · Atk 21 · HP 170 · **Trait:** *Prey* (`prey`) — marca um alvo como
**Presa**; dano contra a Presa **cresce com o tempo de caçada** (+5%/s até +30%). Quando a Presa morre,
a marca **salta** para o próximo e Gaia ganha **atk speed** por alguns segundos.

| Slot | Skill | Shape | Pwr | CD | Alcance/Raio | Detalhes |
|---|---|---|---|---|---|---|
| 1 | **Mineral Arrow** | single | 1.30 | 1.8 s | r5 | Flecha de pedra, certeira de longe. |
| 2 | **Monolith Fall** | area | 1.45 | 4 s | r7 / raio2 | Monólito sobre a área; **stun 300 ms** em quem está embaixo. |
| 3 | **Binding Roots** | field | — | 9 s | r6 / raio1 | Raízes de pedra prendem e ferem (0.40 por 5 s); **slow 0.4 / 2.0 s**. |
| 4 | **Stone Shards** | cone | 1.55 | 6 s | leque raio3 | Estilhaça a rocha à frente num leque largo. |
| **ULT** | **Tectonic Rain** | barrage | 1.45 | — | r7 / raio2 | 3 pedras tectônicas em sequência; **stun 300 ms** nos sobreviventes. |

---

# ⚔️ KNIGHTS

> Melhor auto sustentado em melee / range 1 / AoE média. **Taunt** (estilo *exeta res*) puxa ranged
> para o corpo a corpo. Dash = blink + cleave nova no pouso.

## Seren — *Astral Knight* (physical)

**Classe:** Astral Knight · Atk 21 · HP 240 · **Trait:** *Discipline* (`discipline`) — hits seguidos
no **mesmo alvo** escalam o dano (+8%/hit até +40%); trocar de alvo/parar **reseta**. Todo **3º hit é um
Perfect Cut** (crit garantido). Comprometer-se com o duelo, ou perder o momentum limpando adds.

| Slot    | Skill              | Shape  | Pwr  | CD    | Alcance/Raio | Detalhes                                                                                |
| ------- | ------------------ | ------ | ---- | ----- | ------------ | --------------------------------------------------------------------------------------- |
| 1       | **Precise Cut**    | single | 1.35 | 1.8 s | r1           | Corte limpo no alvo adjacente.                                                          |
| 2       | **Astral Advance** | chain  | 1.35 | 7 s   | r2           | Avança e o golpe **ricocheteia** (3 saltos, falloff 0.25).                              |
| 3       | **Sword Arc**      | cone   | 1.55 | 6 s   | leque raio2  | Arco de lâmina à frente + **TAUNT** (2.5 s): ranged largam o recuo e marcham pro melee. |
| 4       | **Zenith Stance**  | buff   | —    | 14 s  | self         | Postura de duelo: **+atk e +atk speed por 10 s** (`aegis`).                             |
| **ULT** | **Zenith**         | nova   | 2.60 | —     | raio3        | Golpe zênite à volta; **stun 500 ms**.                                                  |

## Rynna — *Thunder Dragoness* (energy)

**Classe:** Thunder Dragoness · Atk 21 · HP 220 · **Trait:** *Static Charge* (`static_charge`) — hits
enchem a barra de **Charge**; o golpe que a completa vira **Discharge** (corrente curta que **paralisa**
em volta). Cada paralisia acelera a ult; a barra já enche **30% mais rápido**. Curto alcance — dragoa de
impacto, não maga.

| Slot | Skill | Shape | Pwr | CD | Alcance/Raio | Detalhes |
|---|---|---|---|---|---|---|
| 1 | **Electric Claw** | single | 1.30 | 1.8 s | r1 | Garra carregada no adjacente; **paralisa 300 ms**. |
| 2 | **Thundering Tail** | cone | 1.55 | 6 s | leque raio2 | Cauda trovejante + **TAUNT** (2.5 s): puxa ranged pro melee. |
| 3 | **Short Discharge** | chain | 1.30 | 7 s | r2 | Descarga que ricocheteia entre inimigos próximos (3 saltos, falloff 0.25). |
| 4 | **Conductive Scale** | buff | — | 11 s | self | Carrega a escama condutora: **+cadência de golpes por 5 s** (`atkspeed`). |
| **ULT** | **Storm Heart** | nova | 2.70 | — | raio3 | Traz o céu abaixo: descarrega a tempestade em volta. |

---

## Notas de balanceamento (em aberto)

- **Rin OP:** Hall concentrado + Ashen Breath beam reduziram a saturação de fogo. Próximas alavancas se
  ainda estiver forte: trim no ult (Infernal Ball 38% spread), no Burning Contract (5 saltos) ou no
  trait Contagion.
- **Velvet:** Death Orbs cortados (0.22 base, 0.04/stack, cap 5, geração única) após o bug de cascata.
- **Beam Width card (planejada):** carta universal que aumenta a largura das beams (tiles perpendiculares)
  — ainda não implementada; segurada para não somar poder enquanto as mages calibram.
- **Próximas waves:** Archer (pierce/multishot/esteroide de atk speed) e Knight (threat/thorns/berserker)
  foram adiadas — foco atual nas mages.
