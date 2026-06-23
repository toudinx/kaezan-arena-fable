# SK-18 — Skin "Verão" · Rin

> 8 prompts de geração de imagem (GPT Image 2.0) para a skin **Verão** da Rin.
> Referência de entrada: `frontend/public/assets/kaelis/rin/base.png`.
> Modo Skin: congela rosto/cabelo/olhos/raça (súcubo), troca roupa + cenário pelo tema praia.
> Cada prompt embute o **mesmo** bloco de identidade. Cole a `base.png` como referência em todos.

**Tema:** praia tropical / festival de verão — sol forte, água azul-turquesa, palmeiras, areia clara.
**Roupa:** biquíni vermelho, asas e cauda de súcubo à mostra. **Mood:** travesso, vibrante, alegre.
**Acento:** carmim + preto + ouro.

---

## Bloco de identidade (idêntico nos 8 prompts)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long wavy crimson-red hair, glowing pink-magenta eyes, fair skin, pointed ears,
curved black spiky demon horns ringed in gold, large bat wings, and a long demon tail
with a heart-shaped tip. Palette anchor: crimson + black + gold.
NEW outfit for a Summer beach skin: a red bikini (crimson top and bottoms with thin gold
trim), barefoot or simple sandals. Keep the bat wings and heart-tipped tail fully visible.
Keep it the same person — a mischievous succubus — only the outfit and setting change.
```

---

## idle-1.png · idle-2.png · idle-3.png (corpo inteiro, transparente)

```
[BLOCO DE IDENTIDADE]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even sunlight lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing beach pose, arms relaxed, playful confident smile.
- Variant 2: one hand on hip, weight on one leg, teasing mischievous expression.
- Variant 3: one hand brushing hair near face, tail curling playfully, introspective.

Bat wings and heart-tipped demon tail fully visible on all three.
Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

## wallpaper.png (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, bat wings half-open behind her, heart-tipped tail curling.

Background: bright tropical beach / summer festival — white sand, turquoise-blue ocean,
palm trees, clear sunny sky, distant beach umbrellas and festival flags.
Lighting: strong warm sunlight, golden rim light, sparkling reflections on the water;
floating warm light particles and sea spray.

Style: high quality anime art, same as reference. Mood: vibrant, playful, mischievous.
Aspect ratio: 16:9 landscape.
```

## bg-landscape.png (cenário vazio, 16:9 — parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Bright tropical beach / summer festival — white sand foreground, turquoise-blue ocean,
palm trees, clear sunny sky, beach umbrellas and festival flags in the distance. The
center-bottom area where a character would stand should be slightly illuminated, ready for
a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, parta do `wallpaper.png` pronto e peça:
> "Remove the character completely and fill the empty space naturally with the background.
> Keep all lighting and atmosphere identical. Return only the background."

## bg-portrait.png (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Bright tropical beach / summer festival recomposed vertically: tall palm trees rising,
turquoise ocean meeting a clear sunny sky high up, white sand and a beach towel in the
foreground. Center area slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

## banner.png (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose (hair and tail flowing, wings half-open). LEFT side intentionally less busy —
atmospheric tropical beach background with subtle sun glow and sea sparkle, leaving room
for text/UI overlay.

Background: warm sunset-beach gradient (turquoise to warm gold) with glowing sea-spray
particles.
Style: premium summer-gacha anime, like Genshin Impact / Blue Archive summer banners.
Same as reference. Aspect ratio: 2:1 landscape.
```

## thumb.png (busto, 1:1)

```
[BLOCO DE IDENTIDADE]

Square portrait (1:1), face and upper chest only (red bikini top visible). Expression:
playful, teasing, mischievous smile. One horn and pointed ear visible. Background: simple,
bright turquoise-to-warm-gold radial gradient, subtle sun glow behind her — NO complex
background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento

Gere cada imagem colando o prompt no GPT Image 2.0 com a `base.png` da Rin como referência, depois
pós-processe (upscale/removebg/crop) pela trilha do `roadmap_producao_visual.md`. Salve em:

```
frontend/public/assets/kaelis/rin/skins/verao/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

Tornar a skin **jogável** (registrar `SkinDef` em `Domain/Waifus.cs` + manifest do `KaeliArtService`)
é passo de desktop/backend — fora do escopo deste brief (ver `## Depois` no roadmap).
