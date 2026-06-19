# Velvet — Prompts de geração de imagem

> **Kaeli:** Velvet · **ID:** `waifu:velvet` · **Raridade:** 5★ · **Elemento:** Death  
> **Pasta de destino:** `frontend/public/assets/kaelis/velvet/`  
> **Status:** assets gerados e em uso.

---

## Bloco de identidade

Cole este bloco **sem alterações** no topo de cada prompt abaixo.

```
Using this character as reference, keep her exact design:
very long dark purple hair, glowing red eyes, black-and-purple gothic lolita dress with
lace and ruffles, purple flower hair ornament and cat-ear tiara crown, black thigh-high
stockings, black lace heels.
```

**Cenário ancorado:** catedral gótica noturna — lustres de cristal roxo, correntes, runas roxas
brilhando no chão, vitrais com luz violeta, névoa rasa. Acento: roxo. Mood: dark, ethereal, melancholic.

---

## idle-1.png / idle-2.png / idle-3.png

```
Using this character as reference, keep her exact design:
very long dark purple hair, glowing red eyes, black-and-purple gothic lolita dress with
lace and ruffles, purple flower hair ornament and cat-ear tiara crown, black thigh-high
stockings, black lace heels.

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides.
- Variant 2: one hand raised to chest/collar, subtle elegant gesture.
- Variant 3: one hand raised near face or hair, introspective pose.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

## wallpaper.png

```
Using this character as reference, keep her exact design:
very long dark purple hair, glowing red eyes, black-and-purple gothic lolita dress with
lace and ruffles, purple flower hair ornament and cat-ear tiara crown, black thigh-high
stockings, black lace heels.

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible.

Background: gothic castle interior at night — purple crystal chandeliers, heavy iron chains,
glowing purple runes on the stone floor, stained glass windows casting violet light, shallow
ground mist drifting across the floor.
Lighting: dramatic, backlit, deep violet rim light; soft ambient glow on her face;
floating purple particle dust.

Style: high quality anime art, same as reference. Mood: dark, ethereal, melancholic.
Aspect ratio: 16:9 landscape.
```

---

## bg-landscape.png

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Gothic castle interior at night — purple crystal chandeliers hanging from vaulted stone ceiling,
heavy iron chains, glowing purple runes on the stone floor, tall stained glass windows casting
violet and deep blue light, shallow ground mist drifting across the floor. The center-bottom
area where a character would stand should be slightly illuminated by the rune glow, ready for
a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use o wallpaper já pronto e peça:
> "Remove the character completely and fill the empty space naturally with the background.
> Keep all lighting and atmosphere identical. Return only the background."

---

## bg-portrait.png

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Gothic cathedral interior at night — very tall stone archways rising high, narrow stained glass
windows with violet light climbing the full height of the frame, stone floor with glowing purple
runes in wide circular patterns, iron chains descending from the vaulted ceiling, candle clusters
at floor level casting warm orange against the cool violet ambiance. Center area slightly
illuminated for a character to be placed later.

Style: same anime painterly style as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

## banner.png

```
Using this character as reference, keep her exact design:
very long dark purple hair, glowing red eyes, black-and-purple gothic lolita dress with
lace and ruffles, purple flower hair ornament and cat-ear tiara crown, black thigh-high
stockings, black lace heels.

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose (dress and hair flowing). LEFT side intentionally less busy — atmospheric gothic
stone with subtle purple crystal and chain motifs, leaving room for text/UI overlay.

Background: deep purple to black gradient with glowing violet particles.
Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

---

## thumb.png

```
Using this character as reference, keep her exact design:
very long dark purple hair, glowing red eyes, black-and-purple gothic lolita dress with
lace and ruffles, purple flower hair ornament and cat-ear tiara crown, black thigh-high
stockings, black lace heels.

Square portrait (1:1), face and upper chest only. Expression: cold, composed, hint of
melancholy. Background: simple, dark purple to black radial gradient, subtle violet glow
behind her — NO complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## manifest.json

```json
"waifu:velvet": ["idle-1","idle-2","idle-3","wallpaper","bg-landscape","bg-portrait","banner","thumb"]
```
