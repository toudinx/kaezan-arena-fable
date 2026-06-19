# Roster — Prompts de imagem-base das Kaelis

> **O que é este arquivo.** A *imagem-base* é o primeiro passo do pipeline de arte de cada Kaeli.
> Cada prompt agora gera **3 poses de corpo inteiro, fundo transparente**, no mesmo estilo da
> Velvet — essas 3 poses **já são os idles `idle-1/2/3` usados no jogo**. Gerar as três no mesmo
> prompt é proposital: trava roupa, cor de cabelo, acessórios e proporções entre as poses (a
> consistência some se você gerar uma pose por vez). Depois que escolher a base de uma Kaeli, peça
> ao Claude pra gerar o doc de assets dela (wallpaper, bgs, banner, thumb) como foi feito em
> [velvet.md](velvet.md) e [astra.md](astra.md) via skill `kaeli-asset-prompts`.
>
> **Como usar cada prompt:** anexe **a imagem da Velvet** (corpo inteiro, fundo transparente) como
> referência de **estilo/qualidade** e cole o prompt. O prompt instrui o gerador a manter o
> *estilo de arte* da referência, mas criar uma personagem **completamente nova** — nunca copiar
> a Velvet. Gere algumas variações do trio, escolha o melhor conjunto, salve as 3 como base.
>
> **Filosofia do elenco:** gacha premium estilo **Wuthering Waves / Genshin / Nikke** — o que manda
> é ficar **lindo e variado**. Cada Kaeli tem cor/penteado/paleta/silhueta/**raça** distintas das
> outras. Buscamos diversidade de raças: anjos, súcubus, demônios/oni, dragões e humanas com
> orelha/cauda de animal (raposa/kitsune, gato/neko, lobo). Lore e raridade do backend são só
> inspiração solta aqui; ajustamos depois — quanto mais base visual boa, mais rico o jogo.

---

## Visão geral do elenco

| Kaeli | id | Raça | Elem. sug. | Conceito visual | Cabelo | Paleta | Traço marcante |
|---|---|---|---|---|---|---|---|
| **Velvet** ✅ | `waifu:velvet` | humana | death | dama gótica do abismo | roxo escuro longo | roxo/preto | olhos vermelhos, lolita gótica |
| **Astra** ✅ | `waifu:astra` | humana | — | guardiã real | prata em rabo de cavalo | azul/ouro | armadura de estrelas |
| **Mira** | `waifu:mira` | humana | physical | brigona de rua ensolarada | cobre curto bagunçado | ferrugem/couro | bronzeada, atlética, sorriso |
| **Wren** | `waifu:wren` | humana | physical | arqueira silenciosa da mata | castanho-cinza trança lateral | verde-floresta | **pele escura**, penas no cabelo |
| **Sage** | `waifu:sage` | humana | earth | sacerdotisa gentil da natureza | loiro-mel ondulado longo | sálvia/creme/flor | coroa de flores, sardas |
| **Mirai** | `waifu:mirai` | **kemonomimi (lobo)** | physical | garota-lobo feral | prata-cinza selvagem | prata/pele/osso | **orelhas e cauda de lobo** |
| **Sylwen** | `waifu:sylwen` | **elfa** | ice | maga elfa do gelo | platinado liso longuíssimo | branco/azul-gelo | orelhas pontudas |
| **Ember** | `waifu:ember` | humana | fire | gênia piromante explosiva | ruivo-laranja maria-chiquinha | carmesim/ouro | fagulhas, fuligem, óculos |
| **Aurora** | `waifu:aurora` | humana | holy | sacerdotisa radiante do alvorecer | rosa-dourado degradê longo | ouro/rosa/pêssego | aura de luz, halo |
| **Seraphel** 🆕 | `waifu:seraphel` | **anjo** | holy | valquíria celestial guerreira | loiro-pálido coroa+rabo | branco/marfim/ouro | **asas de penas**, halo, espadão |
| **Lilith** 🆕 | `waifu:lilith` | **súcubus** | energy | tentadora do prazer | vinho→magenta ondulado | vinho/magenta/preto | **chifres, asas de morcego, cauda** |
| **Kasai** 🆕 | `waifu:kasai` | **oni/demônio** | fire | berserker infernal | flama vermelha rabo alto | carmesim/brasa/ferro | **chifres curvos**, kanabō, musculosa |
| **Tamamo** 🆕 | `waifu:tamamo` | **kitsune (raposa)** | energy | miko de nove caudas | branco-platinado dourado | branco/ouro/vermelhão | **9 caudas + orelhas**, foxfire azul |
| **Suzu** 🆕 | `waifu:suzu` | **neko (gato)** | physical | ladra ágil noturna | preto pontas cinza | grafite/teal/rosa | **orelhas e cauda de gato**, adagas |
| **Mei** 🆕 | `waifu:mei` | **dragão (draconid)** | energy | soberana da tempestade | azul→violeta ventado | azul-oceano/violeta/ouro | **chifres, asas e cauda de dragão**, escamas |

> Sylwen tem `display name` "Neva" no backend hoje — você pediu a elfa loira chamada **Sylwen**.
> Quando ajustarmos o backend, alinhamos o nome. Aqui chamo de Sylwen.
>
> IDs `waifu:*` das novas (`seraphel`, `lilith`, `kasai`, `tamamo`, `suzu`, `mei`) são sugestões
> minhas — só viram estáveis quando entrarem no backend. As novas focam raças que faltavam e
> cobrem o elemento `energy`, hoje sem nenhuma Kaeli.

---

## Template do prompt de imagem-base

Todos os prompts abaixo seguem este formato (já preenchido por Kaeli). O bloco de **3 poses** no
fim é o que garante consistência de roupa/personagem entre os idles:

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

[descrição visual detalhada da Kaeli]

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, outfit,
accessories and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: [idle neutro que combina com a vibe dela]
- Pose 2: [gesto de assinatura]
- Pose 3: [batida de personalidade]

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Mira — brigona de rua ensolarada

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A confident young female street brawler with warm sun-kissed tan skin and an athletic, toned
build. Short choppy copper-auburn hair with a messy fringe and one small braid tucked behind
the ear. Bright amber eyes, a small scar over one eyebrow, a cocky half-grin. She wears a
sleeveless cropped burnt-orange top over chest wraps (toned midriff showing), dark fitted
combat trousers with a wide worn leather belt, hand bandages wrapping up both forearms, and
fingerless leather gloves. A couple of weathered leather bracelets. Worn leather boots laced
with bandages. Palette: rust orange, copper, cream, leather brown. Energetic, ready-to-fight vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, outfit,
accessories and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: relaxed boxing stance, fists loosely down, weight on one leg, cocky half-grin.
- Pose 2: one fist punching into her open palm, leaning forward, grinning challenge.
- Pose 3: arms crossed, leaning back slightly, smug confident smirk.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Wren — arqueira silenciosa da mata

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A lithe female forest ranger with rich deep-brown skin and a calm, watchful expression. Long
deep ash-brown hair with sun-bleached tips, woven into one thick side braid decorated with small
feathers and carved bone charms. Keen pale jade-green eyes, half-lidded and focused, a single
stripe of subtle face paint under one eye. She wears layered ranger gear: a fitted dark-green
leather jerkin over a charcoal undershirt, a hooded half-cloak in mottled forest greens, leather
bracers on the forearms, fingerless archer gloves, and a quiver strap crossing the chest. Feather
earrings and a small carved wooden pendant. Tall laced soft-soled leather boots. She carries a
slim recurve longbow. Palette: forest green, moss, charcoal, leather brown against warm dark skin.
Quiet, predatory, grounded vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, outfit,
accessories and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: standing calm, bow held loosely at her side, one hand resting on the quiver strap.
- Pose 2: drawing an arrow halfway, bow partly raised, eyes narrowed and focused.
- Pose 3: half-turned, glancing back over one shoulder, alert and watchful.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Sage — sacerdotisa gentil da natureza

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A gentle, warm female nature priestess with a soft graceful build and a serene kind smile. Very
long wavy honey-blonde hair with thin strands of mossy green woven through, worn loose with small
face-framing braids and a crown of living flowers and vines. Warm leaf-green eyes, fair sun-touched
skin with light freckles across the nose. She wears a flowing druidic robe-dress in cream and
sage-green: bare shoulders draped with a leafy mantle, a petal-layered skirt, leaf-and-vine gold
embroidery, and a woven-flower sash at the waist. Blooming flowers tucked in the hair, a softly
glowing seed pendant, delicate gold leaf earrings. Bare feet or soft sandals with vine wraps.
Palette: sage green, cream, honey gold, soft pink blossoms. Maternal, peaceful, radiant vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, outfit,
accessories and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: hands gently clasped in front of her, serene standing, soft smile.
- Pose 2: one hand cupped open holding a small glowing blooming flower.
- Pose 3: one hand brushing a vine or flower in her hair, head tilted, warm smile.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Mirai — garota-lobo feral (orelhas e cauda)

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A wild feral wolf-girl warrior (kemonomimi) with large fluffy silver-grey WOLF EARS on top of
her head and a thick bushy WOLF TAIL. Medium-length wild shaggy silver-grey hair with white
streaks in a rough low side-tail and a messy fringe. Sharp golden-amber eyes with feral
slit-like pupils, fair cool-toned skin with a few faint claw-scar marks, a fanged grin. Lean,
athletic, agile build. She wears tribal hunter garb: a fur-trimmed asymmetric leather top, a
wolf-pelt mantle over one shoulder, a layered hide skirt with fur trim (toned midriff exposed),
leather wraps on arms and thighs, bone-and-claw accessories. A claw necklace and a carved bone
hairpin. Fur-lined leather boots. Palette: silver-grey, white, fur brown, bone cream, with cold
blue accents. Alert, wild, crouch-ready predatory energy.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, ears,
tail, outfit, accessories and colors across all three (these become her 3 in-game idle poses).
Vary ONLY the pose and expression:
- Pose 1: low alert half-crouch, hands loose and clawed, ears perked, tail raised.
- Pose 2: one hand near her mouth showing fangs in a wide grin, ears pricked forward.
- Pose 3: standing upright, head tilted, tail curling, curious wild expression.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Sylwen — maga elfa do gelo (elfa, orelhas pontudas)

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

An elegant tall high-ELF cryomancer with long pointed elf ears, a slender regal build, and a
composed, distant expression. Very long straight platinum-to-icy-blonde hair falling past the
waist, styled with a delicate braided crown and a few crystalline strands. Pale ice-blue, almost
silver eyes; very pale porcelain skin with a faint cool glow. She wears an elegant cryomancer gown
in white and glacial blue with silver filigree: a high collar, a fitted snowflake-embroidered
bodice, sheer translucent sleeves, and a long flowing skirt with a thigh slit — the hems trimmed
with sharp angular crystal-edged geometry like ice shards. A thin silver circlet set with a blue
gem, crystalline snowflake earrings, delicate frost-glass gloves, faint frost forming at the hem.
White heeled boots with crystal facets. Palette: white, ice blue, silver, pale cyan. Cold,
precise, serene vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, outfit,
accessories and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: composed and still, hands folded in front, a slight regal tilt of the head.
- Pose 2: one hand raised palm-up, a crystalline snowflake forming above it.
- Pose 3: one hand to her chin, distant analytic gaze, the other arm across her waist.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Ember — gênia piromante explosiva

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A petite, energetic young female fire mage caught mid-motion with a mischievous grin. Vivid
sunset hair — fiery red-orange fading to golden tips — gathered into high messy twin tails with
loose flyaway strands and a swept asymmetric fringe. Big lively molten orange-gold eyes, fair
warm-flushed skin with a small soot smudge on one cheek. She wears an academy-rebel mage outfit:
a short off-shoulder black-and-crimson jacket with rolled sleeves and faint scorch marks over a
flame-orange cropped top, high-waisted dark shorts with a utility belt of glass vials, mismatched
thigh-high socks (one slipping down), and arm warmers. A pair of goggles pushed up on her head,
a flame-shaped hairpin, fingers wrapped in bandages, a small inked phrase tattooed on one forearm.
Chunky boots, one lace untied. Floating sparks and glowing embers drifting around her. Palette:
crimson, orange, gold, charcoal black. Explosive, playful, genius-klutz vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, outfit,
accessories and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: bouncy energetic stance, one hand thrown up, wide grin.
- Pose 2: cupping a small dancing flame in both hands, mischievous focused look.
- Pose 3: pulling the goggles down over one eye with a wink, free hand flicking a spark.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Aurora — sacerdotisa radiante do alvorecer

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A radiant, serene female dawn priestess, tall and graceful, with a benevolent soft smile. Very
long flowing wavy hair in a luminous gradient — soft rose-gold at the roots fading to pale
glowing gold-white at the tips — ornately styled with a half-up crown braid and delicate golden
hair ornaments. Warm amber-gold gentle eyes, fair radiant skin with a soft inner glow. She wears
a flowing priestess gown in white and warm gold with rose and peach accents: layered translucent
draping like sunrise clouds, gold solar-halo embroidery, an elegant gold collar piece at a deep
neckline, long bell sleeves, an ornate gold waist piece, and a light-catching cape-veil. A thin
golden halo-circlet with a sun motif, sun-disc earrings, soft motes of light drifting around her.
Elegant gold-trimmed white heeled sandals. Palette: warm gold, rose, peach, white, soft amber.
Luminous, holy, peaceful dawn vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, outfit,
accessories and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: serene standing, hands gently clasped at the waist, soft smile.
- Pose 2: one hand raised palm-up cradling a glowing mote of dawn light.
- Pose 3: both arms gently opening outward in a gesture of blessing, eyes softly closed.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

# Novas Kaelis 🆕 (diversidade de raças)

---

## Seraphel — anjo valquíria celestial (asas + espadão)

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A tall, statuesque female warrior-ANGEL (valkyrie) with a serene but resolute expression and a
faint divine glow. Large pristine white-and-gold FEATHERED WINGS spread behind her, and a thin
floating golden HALO ring above her head. Long flowing pale-gold hair styled into an ornate
braided crown with a high ponytail cascading down, a few strands lifted as if by holy wind.
Luminous pale-blue eyes with golden irises, fair radiant skin, faint glowing gold filigree
markings along her cheekbones. She wears elegant ceremonial plate armor in white, ivory and gold:
a sculpted breastplate engraved with a sunburst, ornate pauldrons shaped like folded feathers, a
fitted white tabard with gold trim falling between armored thigh plates, gauntlets, and greaves
over a slim underskirt. A long flowing white-and-gold cape. She holds a radiant greatsword that
glows with soft golden light. Feather and halo motifs throughout. Palette: white, ivory, gold,
soft sky-blue. Holy, regal, protective, untouchable vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, wings,
halo, armor and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: standing tall, the greatsword's point resting on the ground, both hands on the pommel,
  wings half-folded, calm and resolute.
- Pose 2: one hand raised to her chest in a solemn vow, sword held vertical at her side, wings
  beginning to spread.
- Pose 3: sword lowered, one hand extended forward palm-up offering a blessing, soft smile.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Lilith — súcubus tentadora (chifres, asas de morcego, cauda)

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

An alluring, confident female SUCCUBUS with a sultry half-lidded gaze and a teasing smirk. A pair
of curved black-and-crimson DEMON HORNS sweep back from her forehead, large membranous BAT-LIKE
WINGS with deep-magenta inner membrane fold behind her, and a long slender DEMON TAIL ending in a
spade/heart-shaped tip curls around one leg. Long voluminous wavy hair in deep wine-red fading to
magenta tips, swept dramatically to one side. Glowing pink-violet eyes with slit pupils, flawless
pale skin with a warm rosy flush, a small visible fang. She wears a daring dark-glamour outfit: a
black-and-magenta corset bodice with gold trim and lace, sheer detached sleeves and long opera
gloves, a high-slit flowing hip-drape skirt with garter straps, sheer thigh-high stockings, and
tall heeled boots. Gold jewelry — layered chokers, fine chains, a heart-gem pendant. A heart motif
echoed in the tail tip and accessories. Faint floating pink heart-shaped magic sparks drift around
her. Palette: wine red, magenta, black, gold. Seductive, playful, dangerous-charm vibe (tasteful,
elegant — premium gacha glamour, not explicit).

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, horns,
wings, tail, outfit and colors across all three (these become her 3 in-game idle poses). Vary
ONLY the pose and expression:
- Pose 1: one hand on her hip, weight on one leg, confident sultry stance, tail curling beside her.
- Pose 2: a single finger raised to her lips in a teasing shush-wink, wings spread slightly.
- Pose 3: blowing a kiss with one hand, a glowing pink heart spark floating from her fingertips.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Kasai — oni/demônio berserker infernal (chifres, kanabō)

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A fierce, powerful female ONI-DEMON warrior with an intense fanged grin and a battle-hungry glare.
Two thick curved HORNS of dark polished crimson rise from her head, pointed demon ears, and faint
glowing-ember crack patterns trace along her skin. Tall, athletic, muscular-yet-feminine build
with warm reddish-tan skin. Long wild flame-red hair with darker roots and ember-orange tips,
gathered in a high messy tail with escaping strands blowing as if from rising heat. Burning
gold-and-red eyes with slit pupils. She wears an asymmetric oni-warrior outfit: a fur-and-leather
pauldron over one shoulder, chest wrappings under an open dark-red haori jacket with black flame
patterns, an exposed toned midriff, a wide knotted obi belt, hakama-style battle trousers tucked
into shin guards, and forearm wraps with iron bracers. Demonic paper talismans, a prayer-bead
bracelet, and a heavy spiked iron KANABŌ war-club resting on her shoulder, glowing with inner
fire. Glowing embers and faint smoke drift around her. Palette: crimson, ember orange, charcoal
black, iron grey, gold. Ferocious, wild, infernal-warrior vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, horns,
outfit, weapon and colors across all three (these become her 3 in-game idle poses). Vary ONLY the
pose and expression:
- Pose 1: kanabō resting on one shoulder, free hand on her hip, cocky wide stance, fanged grin.
- Pose 2: cracking her knuckles with fire flaring around her fist, leaning forward eagerly.
- Pose 3: kanabō swung low behind her in a ready-to-charge crouch, mid-roar.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Tamamo — kitsune de nove caudas (miko, foxfire)

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

An elegant, mischievous NINE-TAILED FOX-GIRL (kitsune) with refined grace and a knowing smile.
Large fluffy FOX EARS (white with golden-amber tips) on her head and NINE large luxurious FOX
TAILS (white tipped with gold) fanning out behind her, with wisps of pale-blue spirit FOXFIRE
dancing among them. Very long straight silken hair, pale platinum-white with soft golden streaks,
partly tied up with an ornate kanzashi hairpin and small bell ornaments, the rest cascading. Warm
amber-gold eyes with slit pupils, fair porcelain skin with subtle red shrine-maiden markings on
the cheeks. She wears an ornate shrine-maiden outfit reimagined as premium fantasy: a white-and-
gold haori-kimono top with wide bell sleeves and a red sash, a short pleated red-and-white hakama
skirt with gold trim, sheer white thigh-highs with gold cord ties, and elegantly reworked
traditional sandals. Magatama beads, a golden bell choker, paper talisman charms. Soft floating
blue foxfire flames and golden petals drift around her. Palette: white, gold, vermilion red, pale
spirit-blue. Graceful, playful-trickster, mystical vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, ears, the
nine tails, outfit and colors across all three (these become her 3 in-game idle poses). Vary ONLY
the pose and expression:
- Pose 1: hands tucked into opposite sleeves, serene standing, the nine tails fanned wide.
- Pose 2: one wide sleeve raised to cover a sly smile, head tilted playfully.
- Pose 3: one palm extended forward conjuring a small blue foxfire flame, tails curling around her.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Suzu — neko ladra ágil noturna (orelhas e cauda de gato)

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A nimble, playful CAT-GIRL (nekomimi) thief with a sly grin and bright curious eyes. Sleek
triangular CAT EARS (black with pink inner fur) atop her head and a long slender black CAT TAIL
with a curled tip. Short-to-medium tousled hair in black with ash-grey tips and one dyed teal
streak, swept asymmetrically with a small braid. Big bright golden-green cat eyes with slit
pupils, fair skin, a small fang, a tiny bandage across the bridge of her nose. Lithe, agile,
lightly athletic build. She wears a sleek urban-rogue outfit: a fitted dark-charcoal sleeveless
crop top with an attached hood, a cropped asymmetric jacket tied at the waist, fingerless gloves,
fitted dark leggings with thigh pouches and buckled straps, light flexible boots, and a wrap belt
holding a pair of slim daggers. A small bell collar/choker, slim knee and elbow guards. Palette:
charcoal black, ash grey, teal accents, a hint of pink. Quick, sneaky, mischievous thief energy.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, ears,
tail, outfit, daggers and colors across all three (these become her 3 in-game idle poses). Vary
ONLY the pose and expression:
- Pose 1: relaxed catlike lean, one hand resting on a dagger hilt, tail up and curious.
- Pose 2: a finger to her lips in a playful "shh" with a wink, the other hand behind her back.
- Pose 3: light on the balls of her feet, both daggers half-drawn, ready-to-pounce crouch.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Mei — soberana dragão da tempestade (chifres, asas e cauda)

```
Use the attached image ONLY as an art-style and quality reference (same anime rendering,
brush quality, soft lighting, full-body framing, transparent background). Do NOT copy its
character or outfit — create a COMPLETELY NEW, original character described below.

A proud, spirited DRAGON-GIRL (draconid) with a regal bearing and a fierce confident grin. A pair
of elegant ridged DRAGON HORNS curve back from her head, pointed draconic ears, iridescent SCALE
patches along her cheekbones, forearms and the outer sides of her legs, and a long powerful scaled
DRAGON TAIL. A pair of membranous DRAGON WINGS (azure with violet membrane) arch behind her. Long
flowing hair in deep ocean-blue fading to electric-violet tips, wind-swept, with a couple of swept
horn-like locks. Glowing cyan-violet eyes with slit pupils, sun-kissed skin. She wears a sleek
dragoon outfit blending armor and royalty: a fitted scaled-pattern bodysuit in deep blue, layered
armored plates with gold trim on the shoulders, hips and shins shaped like dragon scales, a flowing
tattered half-cape, clawed gauntlets, and thigh-high armored boots. Faint storm-energy crackles
around her hands and tail tip. Palette: deep ocean blue, electric violet, gold, slate grey. Proud,
electric, sky-sovereign vibe.

Generate the SAME character as 3 SEPARATE full-body images — identical face, hairstyle, horns,
wings, tail, outfit and colors across all three (these become her 3 in-game idle poses). Vary ONLY
the pose and expression:
- Pose 1: arms crossed, wings spread wide, chin slightly raised, proud and commanding.
- Pose 2: one clawed hand raised with crackling storm energy gathering in the palm.
- Pose 3: wings sweeping, leaning forward mid-taunt with a confident grin, tail lashing.

Each of the 3 images: full body, facing forward, standing, transparent background (PNG with
alpha), soft even studio lighting, no background scene. High quality premium gacha character art
(Wuthering Waves / Genshin Impact tier). Aspect ratio: portrait (roughly 2:3).
```

---

## Próximo passo

1. Gere o trio de poses de cada Kaeli (anexando a Velvet como referência de estilo). As 3 poses
   já são os idles do jogo — confira se roupa/cabelo/cor batem entre elas antes de salvar.
2. Escolha a melhor variação do trio e salve provisoriamente como `idle-1/2/3`.
3. Para cada Kaeli pronta, peça: *"gera os assets da [nome]"* — a skill `kaeli-asset-prompts`
   monta o bloco de identidade a partir da base e produz os demais assets (wallpaper, bg-landscape,
   bg-portrait, banner, thumb), salvando o doc em `docs/prompts/<nome>.md`.

> **Ideias para a próxima leva de raças** (quando quiser expandir mais): dark-elf (drow) de sombras,
> vampira aristocrata, naga/sereia, golem/autômato de porcelana, anjo caído (ponte entre Seraphel e
> Lilith), e mais kemonomimi (coelha, dragãozinho-lagarto, esquilo). Cada uma reforça uma silhueta/
> elemento ainda pouco usados.
