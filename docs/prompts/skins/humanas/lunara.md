# SK-07 — Skin "Humanas" · Lunara

> 8 prompts de geração de imagem (GPT Image 2.0) para a skin **Humanas** da Lunara.
> Referência de entrada em **todos**: `frontend/public/assets/kaelis/lunara/base.png`.
> Tema "Humanas": Lunara como **humana** — orelhas de coelho **removidas**, orelhas humanas normais;
> reconhecível só pela cor de cabelo lavanda-prata e olhos azuis. Look casual-chique fofo,
> cenário slice-of-life (parque/cafeteria).
>
> **Como usar:** cole a `base.png` como imagem de referência + o prompt inteiro (bloco de
> identidade + corpo). O bloco de identidade é **idêntico nos 8** — é a âncora de consistência.

## Bloco de identidade (idêntico nos 8 prompts)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long silver-lavender hair (soft waves), bright blue eyes, fair skin.

IMPORTANT — "Humanas" skin: she is now FULLY HUMAN. Remove ALL non-human features:
NO rabbit ears, no animal ears of any kind, no fluff, no tail — give her plain normal human ears.
The ONLY way she stays recognizable is her silver-lavender hair color and blue eyes.

NEW outfit for a "Humanas" everyday skin: cute casual-chic look — a soft cream/lavender oversized
knit sweater or cardigan over a light pastel dress, a small crossbody bag, white sneakers or low
ankle boots, delicate everyday jewelry. Cozy, stylish, slice-of-life.
Keep it the same person — only the rabbit ears are removed and the outfit/setting change.
```

---

## idle-1.png / idle-2.png / idle-3.png (3 variantes, fundo transparente)

```
[BLOCO DE IDENTIDADE]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides.
- Variant 2: one hand raised to chest/collar adjusting the cardigan, subtle gentle gesture.
- Variant 3: one hand near her face brushing hair behind a (human) ear, soft shy smile.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
Reminder: normal human ears only, NO rabbit ears.
```

## wallpaper.png (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, casual relaxed posture.

Background: a calm modern city park beside a cozy corner café — wooden benches, soft greenery,
cherry/blossom-tinted trees, string lights, a glass café front with warm light spilling out.
Lighting: gentle warm late-afternoon sun, soft golden hour glow on her face; faint lavender
bloom in the air; light bokeh from the café lights.

Style: high quality anime art, same as reference. Mood: soft, everyday, surprising-in-its-normalcy.
Aspect ratio: 16:9 landscape. Reminder: human ears only, NO rabbit ears.
```

## bg-landscape.png (cenário vazio, 16:9 — para parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

A calm modern city park beside a cozy corner café — wooden benches, soft greenery, cherry/blossom
trees, string lights, a warm-lit glass café front. Gentle warm late-afternoon golden-hour light,
soft bokeh. The center-bottom area where a character would stand should be slightly illuminated,
ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

## bg-portrait.png (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

The same cozy park-and-café scene recomposed vertically: emphasize height — tall blossom trees
arching overhead, string lights running up, the café's tall glass front, foreground path/benches
at the bottom. Warm golden-hour light, soft bokeh. Center area slightly illuminated for a
character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

## banner.png (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight dynamic
pose (sweater/hair softly flowing, casual cheerful posture). LEFT side intentionally less busy —
atmospheric background with subtle blossom petals and warm café-light bokeh, leaving room for
text/UI overlay.

Background: warm cream-to-soft-lavender gradient with glowing particles and faint petals.
Style: premium slice-of-life anime, like Blue Archive / Genshin everyday banners. Same as reference.
Aspect ratio: 2:1 landscape. Reminder: human ears only, NO rabbit ears.
```

## thumb.png (busto, 1:1)

```
[BLOCO DE IDENTIDADE]

Square portrait (1:1), face and upper chest only. Expression: soft warm everyday smile, gentle and
a little shy. Background: simple, soft lavender-cream to white radial gradient, subtle glow behind
her — NO complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
Reminder: normal human ears, NO rabbit ears — recognizable only by silver-lavender hair and blue eyes.
```

---

## Salvamento (passo de "Depois")

Gere cada prompt no GPT Image 2.0 com a `base.png` da Lunara como referência, pós-processe pela
trilha do `roadmap_producao_visual.md` (upscale/removebg/crop) e salve em:

```
frontend/public/assets/kaelis/lunara/skins/humanas/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

Tornar a skin **jogável** (registrar `SkinDef` em `Domain/Waifus.cs` + manifest do `KaeliArtService`)
é passo de desktop/backend — fora do escopo deste brief.
