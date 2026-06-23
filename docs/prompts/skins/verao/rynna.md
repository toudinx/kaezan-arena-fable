# SK-17 — Skin "Verão" · Rynna

> **Modo Skin** da skill `kaeli-asset-prompts`. Congela rosto/cabelo/olhos/raça da Rynna
> (dragão), substitui roupa + cenário pelo tema **Verão** (praia tropical).
> **Referência de entrada:** `frontend/public/assets/kaelis/rynna/base.png` (cole como imagem de
> referência em TODOS os 8 prompts).
>
> **Gerador:** GPT Image 2.0 (image-to-image, com a `base.png` anexada).
> **Saída final dos assets:** `frontend/public/assets/kaelis/rynna/skins/verao/`.

## Bloco de identidade (idêntico nos 8 prompts)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
dark-skinned dragon-girl with very long electric-blue hair, glowing violet eyes, pointed ears,
ridged electric-blue dragon horns ringed in gold, electric-blue scale patches on her skin, large
purple membranous dragon wings, and a long scaled electric-blue dragon tail. Palette anchor:
dark skin, electric-blue hair, violet eyes, electric-blue + violet + gold accents.
Keep ALL her dragon features visible and uncovered (horns, ears, scale patches, wings, tail).
NEW outfit for a Summer beach skin: an electric-blue two-piece bikini with violet/gold trim,
surf/ocean vibe — gold anklet and a thin gold body chain as accents, barefoot.
Keep it the same person — only the outfit and setting change.
```

---

## idle-1.png · idle-2.png · idle-3.png (3 variantes, transparente)

```
[BLOCO DE IDENTIDADE]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image. Wings half-open, tail visible.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing beach pose, arms relaxed at sides, weight on one hip.
- Variant 2: one hand raised adjusting hair, playful summer gesture, slight smile.
- Variant 3: one hand on hip, looking back over the shoulder, confident surf-girl pose.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

## wallpaper.png (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, dragon wings spread, tail curling along the sand.

Background: tropical beach / summer festival — bright sun, turquoise-blue ocean, gentle waves,
white sand, palm trees, distant festival stalls and paper lanterns; clear blue sky.
Lighting: bright warm sunlight, electric-blue rim light on her wings and hair; sparkling
sea-spray and floating violet light particles.

Style: high quality anime art, same as reference. Mood: vibrant, joyful, surf/ocean energy.
Aspect ratio: 16:9 landscape.
```

## bg-landscape.png (cenário vazio, 16:9) — para parallax

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Tropical beach / summer festival — bright sun, turquoise-blue ocean, gentle waves, white sand,
palm trees, distant festival stalls and paper lanterns, clear blue sky. The center-bottom area
where a character would stand (on the sand near the waterline) should be slightly illuminated,
ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use a `wallpaper.png` pronta e peça:
> "Remove the character completely and fill the empty space naturally with the beach background.
> Keep all lighting and atmosphere identical. Return only the background."

## bg-portrait.png (cenário vazio, 9:16) — fundo da página Kaelis

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Tropical beach / summer festival recomposed vertically: emphasize height — tall palm trees rising,
a string of paper lanterns climbing, turquoise ocean meeting a high bright sky at the top, white
sand in the foreground. Center area slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

## banner.png (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose (hair and dragon wings flowing in the sea breeze, tail swaying). LEFT side
intentionally less busy — atmospheric beach background with subtle turquoise waves and palm
silhouettes, leaving room for text/UI overlay.

Background: deep ocean-blue to black gradient with glowing electric-blue and violet particles,
warm sun flare on the right.
Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive summer banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

## thumb.png (busto, 1:1)

```
[BLOCO DE IDENTIDADE]

Square portrait (1:1), face and upper chest only (horns, pointed ears and electric-blue hair
clearly visible). Expression: playful, confident, bright summer smile. Background: simple, dark
electric-blue to black radial gradient with a subtle turquoise glow behind her — NO complex
background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento

Gere cada prompt no GPT Image 2.0 (anexando a `base.png` da Rynna), pós-processe pela trilha do
`roadmap_producao_visual.md` (upscale/removebg/crop) e salve em:

```
frontend/public/assets/kaelis/rynna/skins/verao/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

Tornar a skin **jogável** (registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do
`KaeliArtService`) é passo de desktop/backend — fora do escopo deste brief.
