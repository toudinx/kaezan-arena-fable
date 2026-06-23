# SK-06 — Humanas · Eloa (8 prompts de asset)

> **Skin:** Humanas (linha "E se fossem humanas?") · **Kaeli:** Eloa (`waifu:eloa`) · **Tema:** humana
> **Modo:** Skin (congela rosto/cabelo/olhos; **remove** as asas de anjo) · **Referência:** `frontend/public/assets/kaelis/eloa/base.png`
>
> **Bloco de identidade** (colado idêntico no topo dos 7 prompts com personagem) congela só os traços
> imutáveis da Eloa **menos os traços não-humanos** — a transformação central desta skin. Cole no
> GPT Image 2.0 com a `base.png` como imagem de referência.
>
> **Palette anchor:** cabelo preto longo liso, olhos rosa brilhantes, pele clara. **Removido:** asas de penas (*no wings*).
> **Cenário ancorado (tema Humanas):** cidade moderna comum, slice-of-life. **Variação Eloa:** look de
> estudante elegante; biblioteca/campus, luz suave de tarde.

---

## Bloco de identidade (skin) — usado nos 7 prompts com personagem

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long straight black hair, glowing pink eyes, fair skin.
This is a HUMAN-version skin of an angel character: NO wings, no halo, no feathers,
no non-human features at all — normal human girl. Keep it the same person — only the
outfit and setting change.
NEW outfit for a "modern human student" skin: elegant smart-casual student look — a
cream knit sweater over a white collared shirt, a pleated dark-grey skirt, sheer black
tights, brown leather loafers; a slim shoulder bag and round thin-framed glasses as accents.
Palette tell to preserve: black hair, glowing pink eyes, fair skin.
```

---

## idle-1.png · idle-2.png · idle-3.png  (corpo inteiro, transparente)

```
[BLOCO DE IDENTIDADE acima]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, holding a closed book against one hip.
- Variant 2: one hand raised to adjust her glasses, subtle elegant gesture, gentle smile.
- Variant 3: one hand tucking a strand of hair behind her ear, introspective pose, looking aside.

Absolutely NO wings, no halo, no feathers — normal human back and shoulders.
Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

## wallpaper.png  (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE acima]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, in her student outfit, holding a book.

Background: quiet university library / campus reading hall — tall wooden bookshelves, a large
arched window letting in warm late-afternoon sunlight, dust motes floating in the light beams,
a long study table with a few open books and a desk lamp, soft bokeh of distant shelves.
Lighting: warm, soft, golden-hour sun through the window; gentle ambient glow on her face;
her glowing pink eyes are the only "magical" tell. Floating dust particles in the sunbeams.

Style: high quality anime art, same as reference. Mood: calm, cozy, slice-of-life, surprising
in its ordinariness. NO wings, no halo. Aspect ratio: 16:9 landscape.
```

## bg-landscape.png  (cenário vazio, 16:9 — para parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Quiet university library / campus reading hall — tall wooden bookshelves, a large arched window
with warm late-afternoon sunlight, dust motes floating in the light beams, a long study table
with a few open books and a desk lamp, soft bokeh of distant shelves. The center-bottom area
where a character would stand should be slightly illuminated, ready for a character to be
composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use o **wallpaper** já pronto e peça:
> "Remove the character completely and fill the empty space naturally with the background.
> Keep all lighting and atmosphere identical. Return only the background."

## bg-portrait.png  (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Quiet university library / campus reading hall, recomposed vertically: emphasize height — very
tall bookshelves rising up, a tall arched window with warm late-afternoon sunlight streaming down,
dust motes in the light beams, foreground with a study table corner and stacked books. Center area
slightly illuminated for a character later.

Style: same anime painterly style as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

## banner.png  (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE acima]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, in her
student outfit, slight dynamic pose (hair and skirt softly flowing, holding a book to her chest).
LEFT side intentionally less busy — atmospheric background with subtle blurred bookshelves and
soft sunbeams, leaving room for text/UI overlay.

Background: warm cream-and-amber library tones fading to a soft dark edge with glowing dust motes.
NO wings, no halo. Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive banners.
Same as reference. Aspect ratio: 2:1 landscape.
```

## thumb.png  (busto, 1:1)

```
[BLOCO DE IDENTIDADE acima]

Square portrait (1:1), face and upper chest only, in her student sweater and glasses.
Expression: gentle, warm, quietly intelligent, a soft hint of a smile. Background: simple,
warm cream-to-soft-dark radial gradient, subtle glow behind her — NO complex background elements,
NO wings, NO halo (must read clearly at small UI sizes). Her glowing pink eyes stand out.

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento (passo de geração — depois)

Cole cada prompt no GPT Image 2.0 com `frontend/public/assets/kaelis/eloa/base.png` como referência.
Após pós-processo (ComfyUI: upscale/removebg/crop), salve a skin em:

```
frontend/public/assets/kaelis/eloa/skins/humanas/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (16:9)
  bg-landscape.png                    (16:9, cena vazia)
  bg-portrait.png                     (9:16, cena vazia)
  banner.png                          (2:1)
  thumb.png                           (1:1)
```

Tornar **jogável** (registrar `SkinDef` em `Domain/Waifus.cs` + manifest do `KaeliArtService`) é
passo de desktop/backend — fora do escopo deste brief (ver `## Depois` no roadmap).
