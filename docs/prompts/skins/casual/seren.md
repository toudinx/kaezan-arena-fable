# SK-13 — Skin "Casual" · Seren

Set de 8 prompts da skin **Casual** da Seren (minimalista chique off-duty).
Gerados via `kaeli-asset-prompts` (Modo Skin) a partir de
`frontend/public/assets/kaelis/seren/base.png`.

- **Identidade preservada:** cabelo branco-prateado longo em rabo de cavalo alto, olhos azuis,
  pele clara, humana (sem traços não-humanos).
- **Tema (substitui roupa + cenário):** gola alta + casaco longo bege/creme; livraria silenciosa /
  rua de inverno ao entardecer.
- **Acento:** prata / branco / azul-gelo, com toques de bege quente do casaco.

> O **bloco de identidade** abaixo é colado, sem alterações, no topo dos 8 prompts. É a âncora de
> consistência — só muda enquadramento, pose e cenário entre os assets.

**Bloco de identidade (Modo Skin):**

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long silver-white hair tied in a high ponytail, clear blue eyes, fair skin. Human, no
non-human features.
NEW outfit for a "Casual" skin: a cozy cream ribbed turtleneck sweater under a long camel/beige
wool coat (open, soft drape), slim dark trousers, a thin ice-blue scarf, and clean white sneakers
or low ankle boots. Minimal jewelry — small silver stud earrings. Effortless chic, off-duty look.
Keep it the same person — only the outfit and setting change.
```

---

## `idle-1.png` / `idle-2.png` / `idle-3.png` (3 variantes, transparente)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long silver-white hair tied in a high ponytail, clear blue eyes, fair skin. Human, no
non-human features.
NEW outfit for a "Casual" skin: a cozy cream ribbed turtleneck sweater under a long camel/beige
wool coat (open, soft drape), slim dark trousers, a thin ice-blue scarf, and clean white sneakers
or low ankle boots. Minimal jewelry — small silver stud earrings. Effortless chic, off-duty look.
Keep it the same person — only the outfit and setting change.

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, coat draping naturally.
- Variant 2: one hand tucked in the coat pocket, weight on one leg, relaxed casual stance.
- Variant 3: one hand adjusting the scarf near her collar, soft introspective expression.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

## `wallpaper.png` (cena completa, 16:9)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long silver-white hair tied in a high ponytail, clear blue eyes, fair skin. Human, no
non-human features.
NEW outfit for a "Casual" skin: a cozy cream ribbed turtleneck sweater under a long camel/beige
wool coat (open, soft drape), slim dark trousers, a thin ice-blue scarf, and clean white sneakers
or low ankle boots. Minimal jewelry — small silver stud earrings. Effortless chic, off-duty look.
Keep it the same person — only the outfit and setting change.

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible.

Background: cozy quiet bookstore interior on a winter evening — tall warm-lit wooden bookshelves,
a reading nook with a soft lamp, a large window showing a snowy street outside with falling snow
and ice-blue dusk light. Warm interior glow meeting cool blue light from the window.
Lighting: soft, intimate; warm amber key light on her face, cool ice-blue rim light from the
window; faint floating dust catching the lamplight; gentle snow visible through the glass.

Style: high quality anime art, same as reference. Mood: calm, serene, cozy off-duty winter.
Aspect ratio: 16:9 landscape.
```

---

## `bg-landscape.png` (cenário vazio, 16:9 — parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Cozy quiet bookstore interior on a winter evening — tall warm-lit wooden bookshelves, a reading
nook with a soft lamp, a large window showing a snowy street outside with falling snow and
ice-blue dusk light. Warm interior glow meeting cool blue light from the window; faint floating
dust catching the lamplight. The center-bottom area where a character would stand should be
slightly illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use o `wallpaper.png` já pronto e peça:
> "Remove the character completely and fill the empty space naturally with the background.
> Keep all lighting and atmosphere identical. Return only the background."

---

## `bg-portrait.png` (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Cozy quiet bookstore on a winter evening, recomposed vertically: emphasize height — tall wooden
bookshelves rising up the frame, a tall window on the side showing a snowy street and ice-blue
dusk light, a soft reading lamp, snow falling outside. Foreground floor with a warm rug.
Center area slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

## `banner.png` (personagem à direita, 2:1)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long silver-white hair tied in a high ponytail, clear blue eyes, fair skin. Human, no
non-human features.
NEW outfit for a "Casual" skin: a cozy cream ribbed turtleneck sweater under a long camel/beige
wool coat (open, soft drape), slim dark trousers, a thin ice-blue scarf, and clean white sneakers
or low ankle boots. Minimal jewelry — small silver stud earrings. Effortless chic, off-duty look.
Keep it the same person — only the outfit and setting change.

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight dynamic
pose (coat and ponytail catching a soft breeze, hand near scarf). LEFT side intentionally less
busy — atmospheric winter background with subtle falling snow and soft bokeh of bookstore lights,
leaving room for text/UI overlay.

Background: deep cool blue to near-black gradient with glowing ice-blue and warm-amber particles.
Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

---

## `thumb.png` (busto, 1:1)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long silver-white hair tied in a high ponytail, clear blue eyes, fair skin. Human, no
non-human features.
NEW outfit for a "Casual" skin: a cozy cream ribbed turtleneck sweater under a long camel/beige
wool coat (open, soft drape), a thin ice-blue scarf. Minimal jewelry — small silver stud earrings.
Effortless chic, off-duty look. Keep it the same person — only the outfit and setting change.

Square portrait (1:1), face and upper chest only. Expression: warm, calm, a soft gentle smile.
Background: simple, dark ice-blue to black radial gradient, subtle cool glow behind her — NO
complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento

Gerar cada imagem (GPT Image 2.0 + `seren/base.png`) → pós-processo ComfyUI, e salvar em:

```
frontend/public/assets/kaelis/seren/skins/casual/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

Tornar jogável é passo de desktop: registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do
`KaeliArtService` (fora do escopo deste brief).
