# Skin "Verão" — Eloa (SK-14)

> Gerado pela skill `kaeli-asset-prompts` (Modo Skin). Referência de entrada:
> `frontend/public/assets/kaelis/eloa/base.png`. Cole cada prompt no **GPT Image 2.0**
> junto com a `base.png` como imagem de referência. São 8 prompts, um por arquivo de destino.
>
> **Bloco de identidade (congelado em todos os 8):** preserva rosto/cabelo/olhos/asas; troca só
> roupa + cenário pelo tema Verão. Asas de penas **presentes** (traço de raça mantido).
>
> **Palette anchor:** cabelo preto longo liso · olhos rosa brilhantes · pele clara · asas de penas
> preto-e-branco · acento branco + rosa + dourado de sol.
>
> **Cenário ancorado (linha Verão):** praia tropical / festival de verão, sol forte, água
> azul-turquesa, areia clara, palmeiras. Mood: vibrante, alegre, luz quente de verão.

---

## Bloco de identidade (NÃO reescrever entre prompts)

```
Using this character as reference, keep her face, hair, eyes and wings EXACTLY:
very long straight black hair, glowing pink eyes, fair skin, and her large black-and-white
feathered angel wings (white near the body fading to black at the tips).
NEW outfit for a Summer / beach skin: an elegant white-and-gold one-piece swimsuit with thin
golden trim and a small golden chain detail at the hip, a sheer white beach wrap/sarong at the
waist, delicate gold anklet, barefoot or light gold sandals. Feathered angel wings stay fully
present and uncovered.
Keep it the same person — only the outfit and setting change. Palette accent: white + soft pink
glow + warm gold sunlight.
```

---

## idle-1.png / idle-2.png / idle-3.png  (corpo inteiro, transparente)

```
[BLOCO DE IDENTIDADE]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even sunlight lighting. Same art style as the reference image. Wings fully visible.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing beach pose, arms relaxed at sides, wings softly half-open.
- Variant 2: one hand raised to adjust her sun hat / brush hair, subtle elegant gesture.
- Variant 3: one hand near face shading her eyes from the sun, introspective pose.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

## wallpaper.png  (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, feathered wings softly open catching the light.

Background: tropical summer beach — bright white sand, turquoise-blue water, gentle waves,
palm trees, clear sunny sky with soft clouds, a distant summer festival with paper lanterns
and flags along the shore.
Lighting: warm golden sunlight, soft pink rim light on her hair and wings; glittering highlights
on the water; floating warm light particles and a few drifting white feathers.

Style: high quality anime art, same as reference. Mood: vibrant, joyful, radiant summer.
Aspect ratio: 16:9 landscape.
```

---

## bg-landscape.png  (cenário vazio, 16:9 — parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Tropical summer beach — bright white sand, turquoise-blue water, gentle waves, palm trees,
clear sunny sky with soft clouds, a distant summer festival with paper lanterns and flags
along the shore. Warm golden sunlight, glittering highlights on the water. The center-bottom
area of the sand should be slightly illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

---

## bg-portrait.png  (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Tropical summer beach recomposed vertically: emphasize height — tall palm trees rising up the
frame, a strip of turquoise water and bright sky above, white sand in the foreground with a few
seashells, distant summer-festival lanterns strung overhead. Warm golden sunlight.
Center area slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

## banner.png  (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose (hair and sarong flowing in the sea breeze, one wing sweeping out). LEFT side
intentionally less busy — atmospheric beach background with subtle sun glare, soft bokeh of
water sparkles and a couple of drifting white feathers, leaving room for text/UI overlay.

Background: warm turquoise-to-golden gradient with glowing light particles.
Style: premium summer anime, like Genshin Impact / Blue Archive seasonal banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

---

## thumb.png  (busto, 1:1)

```
[BLOCO DE IDENTIDADE]

Square portrait (1:1), face and upper chest only (swimsuit straps and a hint of wing visible).
Expression: bright, warm, gentle summer smile. Background: simple, soft turquoise-to-white
radial gradient with a warm golden glow behind her — NO complex background elements (must read
clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento

Depois de gerar as imagens, salve em (convenção de subpasta de skin — confirmar no desktop):

```
frontend/public/assets/kaelis/eloa/skins/verao/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (16:9, cena completa)
  bg-landscape.png                    (16:9, cena vazia)
  bg-portrait.png                     (9:16, cena vazia)
  banner.png                          (2:1, personagem à direita)
  thumb.png                           (1:1, busto)
```

Tornar a skin **jogável** (registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do
`KaeliArtService`) é passo de desktop/backend — fora do escopo deste brief (ver `## Depois`
do `roadmap_skins.md`).
