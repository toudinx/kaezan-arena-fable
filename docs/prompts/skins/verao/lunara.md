# Skin "Verão" — Lunara (SK-19)

> 8 prompts de geração de imagem para a skin **Verão** da Lunara, prontos para colar no
> **GPT Image 2.0** usando `frontend/public/assets/kaelis/lunara/base.png` como referência.
> Isto gera **prompts**, não imagens.
>
> **Modo Skin:** o bloco de identidade congela rosto/cabelo/olhos/orelhas de coelho e
> **substitui** roupa + cenário pelo tema praia. Cole o bloco **sem alterações** no topo dos
> prompts que têm a personagem (idles, wallpaper, banner, thumb). Os de cenário vazio
> (bg-landscape, bg-portrait) usam a base só como referência de **estilo**.

---

## Bloco de identidade (cole idêntico em todos os prompts com personagem)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
moon-hare girl with very long wavy silver-lavender hair, bright blue eyes, fair skin, and her
large fluffy white rabbit ears (keep the ears, they are part of her). Palette anchor: silver-lavender
hair, blue eyes, fair skin, soft pastel lavender accents.
NEW outfit for a Summer beach skin: a cute pastel bikini (soft lavender and white, with small frills
or a bow), barefoot, optional sheer pastel beach wrap around the hips. She carries / floats inside a
cute white rabbit-shaped swim ring (inflatable float with rabbit ears and face).
Keep it the same person — only the outfit and setting change.
```

**Cenário ancorado (tema Verão):** praia tropical / festival de verão — sol forte, areia
clara, água azul-turquesa, palmeiras, céu azul limpo. Mood: vibrante, alegre, fofo.

---

## `idle-1.png` / `idle-2.png` / `idle-3.png` (transparente, corpo inteiro)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
moon-hare girl with very long wavy silver-lavender hair, bright blue eyes, fair skin, and her
large fluffy white rabbit ears (keep the ears, they are part of her). Palette anchor: silver-lavender
hair, blue eyes, fair skin, soft pastel lavender accents.
NEW outfit for a Summer beach skin: a cute pastel bikini (soft lavender and white, with small frills
or a bow), barefoot, optional sheer pastel beach wrap around the hips. She carries / floats inside a
cute white rabbit-shaped swim ring (inflatable float with rabbit ears and face).
Keep it the same person — only the outfit and setting change.

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, holding the rabbit swim ring at one side.
- Variant 2: playful pose, holding the rabbit float in front of her with both hands, cheerful.
- Variant 3: one hand raised near face/ear in a cute introspective pose, float resting at her hip.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

## `wallpaper.png` (cena completa, 16:9 landscape)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
moon-hare girl with very long wavy silver-lavender hair, bright blue eyes, fair skin, and her
large fluffy white rabbit ears (keep the ears, they are part of her). Palette anchor: silver-lavender
hair, blue eyes, fair skin, soft pastel lavender accents.
NEW outfit for a Summer beach skin: a cute pastel bikini (soft lavender and white, with small frills
or a bow), barefoot, optional sheer pastel beach wrap around the hips. She carries / floats inside a
cute white rabbit-shaped swim ring (inflatable float with rabbit ears and face).
Keep it the same person — only the outfit and setting change.

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, playful summer pose with the rabbit swim ring.

Background: tropical beach / summer festival — bright sun, pale sand, turquoise-blue water,
palm trees, clear blue sky, gentle waves, a few pastel beach parasols in the distance.
Lighting: warm bright daylight, soft pastel-lavender rim light on her hair; sparkling highlights
on the water; floating soft light particles.

Style: high quality anime art, same as reference. Mood: vibrant, cheerful, cute.
Aspect ratio: 16:9 landscape.
```

---

## `bg-landscape.png` (cenário vazio, 16:9 — parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Tropical beach / summer festival — bright sun, pale sand, turquoise-blue water, palm trees,
clear blue sky, gentle waves, pastel beach parasols in the distance. The center-bottom area where a
character would stand should be slightly illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use o **wallpaper** já pronto e peça:
> "Remove the character completely and fill the empty space naturally with the background.
> Keep all lighting and atmosphere identical. Return only the background."

---

## `bg-portrait.png` (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Tropical beach / summer festival recomposed vertically: emphasize height — tall palm trees
rising up, a vertical strip of turquoise water meeting pale sand in the foreground, clear blue sky
filling the top, pastel parasols. Center area slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

## `banner.png` (personagem à direita, 2:1 landscape)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
moon-hare girl with very long wavy silver-lavender hair, bright blue eyes, fair skin, and her
large fluffy white rabbit ears (keep the ears, they are part of her). Palette anchor: silver-lavender
hair, blue eyes, fair skin, soft pastel lavender accents.
NEW outfit for a Summer beach skin: a cute pastel bikini (soft lavender and white, with small frills
or a bow), barefoot, optional sheer pastel beach wrap around the hips. She carries / floats inside a
cute white rabbit-shaped swim ring (inflatable float with rabbit ears and face).
Keep it the same person — only the outfit and setting change.

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose (hair flowing, holding the rabbit float). LEFT side intentionally less busy —
atmospheric summer beach background with subtle palm leaves and soft light, leaving room for
text/UI overlay.

Background: warm pastel sky and turquoise water gradient with glowing summer particles and gentle bokeh.
Style: premium bright-summer anime, like Genshin Impact / Blue Archive seasonal banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

---

## `thumb.png` (busto, 1:1 quadrado)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
moon-hare girl with very long wavy silver-lavender hair, bright blue eyes, fair skin, and her
large fluffy white rabbit ears (keep the ears, they are part of her). Palette anchor: silver-lavender
hair, blue eyes, fair skin, soft pastel lavender accents.
NEW outfit for a Summer beach skin: a cute pastel bikini (soft lavender and white, with small frills
or a bow).
Keep it the same person — only the outfit and setting change.

Square portrait (1:1), face and upper chest only. Expression: cheerful, warm, sweet summer smile.
Background: simple, soft pastel lavender-to-turquoise radial gradient with a subtle sunny glow behind
her — NO complex background elements (must read clearly at small UI sizes). Keep the rabbit ears visible.

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Onde salvar

Depois de gerar no GPT Image 2.0 e pós-processar (ComfyUI: upscale/removebg/crop), salve em:

```
frontend/public/assets/kaelis/lunara/skins/verao/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (16:9)
  bg-landscape.png                    (16:9, sem personagem)
  bg-portrait.png                     (9:16, sem personagem)
  banner.png                          (2:1)
  thumb.png                           (1:1)
```

Tornar a skin **jogável** (registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do
`KaeliArtService`) é um passo de **desktop/backend**, fora deste brief (ver `## Depois` no roadmap).
