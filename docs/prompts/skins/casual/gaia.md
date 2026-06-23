# Skin "Casual" — Gaia (boho/earthy off-duty)

> **SK-11** · Modo Skin · Referência: `frontend/public/assets/kaelis/gaia/base.png`
> Cole cada prompt no **GPT Image 2.0** usando a `base.png` da Gaia como imagem de referência.
> Gera **prompts**, não imagens. Salvar em `frontend/public/assets/kaelis/gaia/skins/casual/`.

**Bloco de identidade (idêntico nos 8 — não reescrever):**

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
dark-skinned girl with very long wavy black hair, glowing green eyes, and a single green
face-paint stripe under one eye. Warm earth-toned palette (terracotta, olive, sand).
NEW outfit for a casual / boho-earthy skin: a cropped knit top, high-waisted denim shorts,
layered earth-toned bead and shell necklaces, woven leather bracelets, flat strappy sandals.
Keep it the same person — only the outfit and setting change.
```

**Cenário ancorado (tema Casual · Gaia):** feira/mercado ao ar livre ensolarado — barracas de
toldo de lona, vasos de plantas e ervas, guirlandas de luzes, frutas e tecidos coloridos, luz
quente de fim de tarde, poeira dourada no ar. Mood: relaxado, off-duty, terroso.

---

### `idle-1.png` · `idle-2.png` · `idle-3.png` (3 variantes, fundo transparente)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
dark-skinned girl with very long wavy black hair, glowing green eyes, and a single green
face-paint stripe under one eye. Warm earth-toned palette (terracotta, olive, sand).
NEW outfit for a casual / boho-earthy skin: a cropped knit top, high-waisted denim shorts,
layered earth-toned bead and shell necklaces, woven leather bracelets, flat strappy sandals.
Keep it the same person — only the outfit and setting change.

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, easy relaxed weight.
- Variant 2: one hand adjusting a necklace / resting at collarbone, soft smile.
- Variant 3: one hand tucking hair behind her ear, hip cocked, off-duty casual gesture.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

### `wallpaper.png` (cena completa, 16:9 landscape)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
dark-skinned girl with very long wavy black hair, glowing green eyes, and a single green
face-paint stripe under one eye. Warm earth-toned palette (terracotta, olive, sand).
NEW outfit for a casual / boho-earthy skin: a cropped knit top, high-waisted denim shorts,
layered earth-toned bead and shell necklaces, woven leather bracelets, flat strappy sandals.
Keep it the same person — only the outfit and setting change.

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, relaxed off-duty posture.

Background: sunny open-air market / fair — canvas awning stalls, potted plants and herbs,
strings of festoon lights, colorful fabrics and fruit baskets, warm late-afternoon sunlight,
golden dust floating in the air.
Lighting: warm and natural, soft terracotta/olive rim light; gentle glow on her face;
floating golden dust particles.

Style: high quality anime art, same as reference. Mood: relaxed, off-duty, earthy.
Aspect ratio: 16:9 landscape.
```

---

### `bg-landscape.png` (cenário vazio, 16:9 — para parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Sunny open-air market / fair — canvas awning stalls, potted plants and herbs, strings of
festoon lights, colorful fabrics and fruit baskets, warm late-afternoon sunlight, golden dust
floating in the air. The center-bottom area where a character would stand should be slightly
illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference, warm earth-toned palette. No characters, no
silhouettes, no people. Aspect ratio: 16:9 landscape.
```

---

### `bg-portrait.png` (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Sunny open-air market / fair recomposed vertically: emphasize height — tall awning poles and
hanging festoon lights overhead, stacked stalls and hanging fabrics rising up, potted plants
and a cobblestone foreground at the bottom. Warm late-afternoon sunlight, golden dust in the
air. Center area slightly illuminated for a character later.

Style: same as reference, warm earth-toned palette. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

### `banner.png` (personagem à direita, 2:1 landscape)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
dark-skinned girl with very long wavy black hair, glowing green eyes, and a single green
face-paint stripe under one eye. Warm earth-toned palette (terracotta, olive, sand).
NEW outfit for a casual / boho-earthy skin: a cropped knit top, high-waisted denim shorts,
layered earth-toned bead and shell necklaces, woven leather bracelets, flat strappy sandals.
Keep it the same person — only the outfit and setting change.

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose (hair and necklaces gently swaying). LEFT side intentionally less busy —
atmospheric earthy market background blurred out (warm bokeh of festoon lights and awnings),
leaving room for text/UI overlay.

Background: warm terracotta-to-deep-brown gradient with glowing golden particles.
Style: premium anime, like Genshin Impact / Blue Archive banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

---

### `thumb.png` (busto, 1:1 quadrado)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
dark-skinned girl with very long wavy black hair, glowing green eyes, and a single green
face-paint stripe under one eye. Warm earth-toned palette (terracotta, olive, sand).
NEW outfit for a casual / boho-earthy skin: a cropped knit top, layered earth-toned bead and
shell necklaces, woven leather bracelets.
Keep it the same person — only the outfit and setting change.

Square portrait (1:1), face and upper chest only. Expression: warm, easy-going, friendly smile
with a confident off-duty vibe. Background: simple, dark earthy green-to-black radial gradient,
subtle warm glow behind her — NO complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Onde salvar / registrar

```
frontend/public/assets/kaelis/gaia/skins/casual/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

> Tornar a skin **jogável** (registrar `SkinDef` em `Domain/Waifus.cs` + manifest do
> `KaeliArtService`) é um passo de desktop/backend — fora desta etapa de brief (ver `## Depois`
> no roadmap).
