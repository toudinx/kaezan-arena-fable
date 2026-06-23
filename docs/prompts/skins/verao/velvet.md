# SK-15 — Skin "Verão" · Velvet (praia gótica)

> 8 prompts de geração de imagem (GPT Image 2.0). Use `frontend/public/assets/kaelis/velvet/base.png`
> como **imagem de referência** em todos. Modo Skin: congela rosto/cabelo/olhos/raça (humana),
> substitui roupa + cenário pelo tema **Verão**.
>
> **Palette anchor (texto):** cabelo roxo-escuro longo, olhos vermelhos brilhantes, pele clara, humana.
> Acento: roxo + preto. **Cenário ancorado:** praia tropical, sol forte, água azul-turquesa, palmeiras.
>
> **Bloco de identidade (idêntico nos 8 prompts):**
>
> ```
> Using this character as reference, keep her face, hair and eyes EXACTLY:
> very long dark purple hair, glowing red eyes, fair skin. Human — no wings, no horns, no tail,
> normal human ears. Palette: dark purple + black accents.
> NEW outfit for a summer / beach skin: a black-and-purple bikini with dark lace trim, a sheer
> dark-purple lace pareo wrapped at the hips, holding a black lace parasol. Keep it the same
> person — only the outfit and setting change.
> ```

---

## `idle-1.png` · `idle-2.png` · `idle-3.png` (corpo inteiro, transparente)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin. Human — no wings, no horns, no tail,
normal human ears. Palette: dark purple + black accents.
NEW outfit for a summer / beach skin: a black-and-purple bikini with dark lace trim, a sheer
dark-purple lace pareo wrapped at the hips, holding a black lace parasol. Keep it the same
person — only the outfit and setting change.

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, parasol resting closed against one shoulder.
- Variant 2: one hand adjusting the pareo at her hip, subtle elegant gesture, parasol open behind her.
- Variant 3: one hand raised near her hair, introspective pose, gentle smile.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

## `wallpaper.png` (cena completa, 16:9)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin. Human — no wings, no horns, no tail,
normal human ears. Palette: dark purple + black accents.
NEW outfit for a summer / beach skin: a black-and-purple bikini with dark lace trim, a sheer
dark-purple lace pareo wrapped at the hips, holding a black lace parasol. Keep it the same
person — only the outfit and setting change.

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, holding the black lace parasol over one shoulder against the bright sun.

Background: tropical beach paradise, strong midday sun, turquoise-blue water, white sand,
swaying palm trees, soft sea breeze, distant horizon haze. A subtle gothic touch — dark purple
beach umbrella and a black lace beach blanket nearby.
Lighting: bright warm sunlight, gentle purple rim light on her hair; soft glow on her face;
floating sea-spray sparkle and dust.

Style: high quality anime art, same as reference. Mood: playful summer with a dark-elegant twist.
Aspect ratio: 16:9 landscape.
```

## `bg-landscape.png` (cenário vazio, 16:9 — parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Tropical beach paradise, strong midday sun, turquoise-blue water, white sand, swaying palm trees,
distant horizon haze, with a subtle gothic touch — a dark purple beach umbrella and a black lace
beach blanket on the sand. The center-bottom area where a character would stand should be slightly
illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use a `wallpaper.png` pronta e peça:
> "Remove the character completely and fill the empty space naturally with the background.
> Keep all lighting and atmosphere identical. Return only the background."

## `bg-portrait.png` (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Tropical beach paradise recomposed vertically: emphasize height — tall palm trees rising up the
frame, bright sun high above, turquoise water in the mid-ground, white sand in the foreground with
a dark purple beach umbrella and black lace blanket. Center area slightly illuminated for a
character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

## `banner.png` (personagem à direita, 2:1)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin. Human — no wings, no horns, no tail,
normal human ears. Palette: dark purple + black accents.
NEW outfit for a summer / beach skin: a black-and-purple bikini with dark lace trim, a sheer
dark-purple lace pareo wrapped at the hips, holding a black lace parasol. Keep it the same
person — only the outfit and setting change.

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight dynamic
pose (pareo and hair flowing in the sea breeze, parasol open behind her). LEFT side intentionally
less busy — atmospheric beach background with subtle palm fronds and sea sparkle, leaving room for
text/UI overlay.

Background: deep purple to black tropical-dusk gradient with glowing sun-sparkle particles.
Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive summer banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

## `thumb.png` (busto, 1:1)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin. Human — no wings, no horns, no tail,
normal human ears. Palette: dark purple + black accents.
NEW outfit for a summer / beach skin: a black-and-purple bikini with dark lace trim, a sheer
dark-purple lace pareo wrapped at the hips, holding a black lace parasol. Keep it the same
person — only the outfit and setting change.

Square portrait (1:1), face and upper chest only. Expression: playful, confident, a hint of sultry
melancholy. Background: simple, dark purple to black radial gradient with a warm sun glow behind
her — NO complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento

Depois de gerar as imagens (GPT Image 2.0 → pós-processo ComfyUI), salve em:

```
frontend/public/assets/kaelis/velvet/skins/verao/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (16:9)
  bg-landscape.png                    (16:9, sem personagem)
  bg-portrait.png                     (9:16, sem personagem)
  banner.png                          (2:1)
  thumb.png                           (1:1)
```

Tornar a skin **jogável** (registrar `SkinDef` em `Domain/Waifus.cs` + manifest do `KaeliArtService`)
é passo de desktop/backend — fora do escopo deste brief (ver `## Depois` no roadmap).
