# Astra — Prompts de geração de imagem

> **Kaeli:** Astra (ex-Kaela) · **ID:** `waifu:astra` · **Raridade:** 5★ · **Elemento:** Physical  
> **Pasta de destino:** `frontend/public/assets/kaelis/astra/`  
> **Status:** prompts prontos; assets pendentes de geração.

---

## Bloco de identidade

Cole este bloco **sem alterações** no topo de cada prompt abaixo.

```
Using this character as reference, keep her exact design:
very long silver-white hair in a high ponytail with loose flowing side strands, cool blue eyes,
white-and-blue armored outfit with gold star motifs — blue flowing cape trimmed in gold with
pointed star accents at the hem, layered white skirt beneath blue chest armor with gold
engravings, black gauntlet gloves, blue thigh-high armored stockings with gold details,
blue heeled boots with gold star clasps, red tassel ornament at the chest brooch.
```

**Cenário ancorado:** fortaleza real de Thais ao amanhecer — muralhas de pedra cinza com
archotes acesos, céu azul cobalto com raios dourados rasgando as nuvens, bandeiras azul-e-ouro
tremulando, escadas de pedra largas com runas de proteção entalhadas. Acento: azul cobalto +
ouro. Mood: regal, austero, inabalável.

---

## idle-1.png / idle-2.png / idle-3.png

```
Using this character as reference, keep her exact design:
very long silver-white hair in a high ponytail with loose flowing side strands, cool blue eyes,
white-and-blue armored outfit with gold star motifs — blue flowing cape trimmed in gold with
pointed star accents at the hem, layered white skirt beneath blue chest armor with gold
engravings, black gauntlet gloves, blue thigh-high armored stockings with gold details,
blue heeled boots with gold star clasps, red tassel ornament at the chest brooch.

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed, cape settled, composed expression — the stance of a veteran guard at rest.
- Variant 2: one gauntleted hand raised to chest/brooch, slight tilt of the head, calm authority in her gaze.
- Variant 3: one hand extended slightly forward — an open palm, protective gesture, as if holding back a tide.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

## wallpaper.png

```
Using this character as reference, keep her exact design:
very long silver-white hair in a high ponytail with loose flowing side strands, cool blue eyes,
white-and-blue armored outfit with gold star motifs — blue flowing cape trimmed in gold with
pointed star accents at the hem, layered white skirt beneath blue chest armor with gold
engravings, black gauntlet gloves, blue thigh-high armored stockings with gold details,
blue heeled boots with gold star clasps, red tassel ornament at the chest brooch.

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, facing forward with calm authority.

Background: grand royal fortress ramparts at dawn — wide stone battlements with lit torches,
cobalt blue sky with golden light breaking through clouds, blue-and-gold banners billowing,
broad stone steps with protection runes carved into them, distant silhouette of Thais city.
Lighting: dramatic, warm golden rim light from the rising sun behind her; cool blue ambient
from the fortress stone; floating gold particle dust like embers from the torches.

Style: high quality anime art, same as reference. Mood: regal, duty-bound, immovable.
Aspect ratio: 16:9 landscape.
```

---

## bg-landscape.png

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Grand royal fortress ramparts at dawn — wide stone battlements with lit torches casting warm
orange glows, cobalt blue sky with golden light breaking through heavy clouds, blue-and-gold
banners billowing in wind, broad stone steps with protection runes engraved, distant silhouette
of Thais city below. The center-bottom area where a character would stand should be slightly
illuminated by a convergence of torch light and dawn rays, ready for a character to be
composited in.

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

Grand royal fortress interior at dawn — very tall stone archways rising upward, high windows
letting in cobalt blue and gold light from the breaking dawn, stone floor with protection runes
carved in wide patterns, lit torches on tall iron brackets along the walls, blue-and-gold
tapestries hanging from the vaulted ceiling, a wide stone staircase descending in the
foreground. Center area slightly illuminated from above for a character to be placed later.

Style: same anime painterly style as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

## banner.png

```
Using this character as reference, keep her exact design:
very long silver-white hair in a high ponytail with loose flowing side strands, cool blue eyes,
white-and-blue armored outfit with gold star motifs — blue flowing cape trimmed in gold with
pointed star accents at the hem, layered white skirt beneath blue chest armor with gold
engravings, black gauntlet gloves, blue thigh-high armored stockings with gold details,
blue heeled boots with gold star clasps, red tassel ornament at the chest brooch.

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose — cape and hair flowing from the right as if she just turned to face a threat.
LEFT side intentionally less busy — atmospheric stone fortress wall with subtle gold star
motifs and dawn light, leaving room for text/UI overlay.

Background: deep cobalt blue to black gradient with floating gold particles and faint rune
glyphs dissolving into light. A faint silhouette of fortress ramparts in the far distance.
Style: premium fantasy anime, like Genshin Impact / Blue Archive banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

---

## thumb.png

```
Using this character as reference, keep her exact design:
very long silver-white hair in a high ponytail with loose flowing side strands, cool blue eyes,
white-and-blue armored outfit with gold star motifs — blue flowing cape trimmed in gold with
pointed star accents at the hem, layered white skirt beneath blue chest armor with gold
engravings, black gauntlet gloves, blue thigh-high armored stockings with gold details,
blue heeled boots with gold star clasps, red tassel ornament at the chest brooch.

Square portrait (1:1), face and upper chest only. Expression: composed, serious, faintly
warm — the face of someone who has never broken a promise. Background: simple, dark cobalt
blue to black radial gradient with subtle golden glow behind her and faint star particle
effects — NO complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## manifest.json

```json
"waifu:astra": ["idle-1","idle-2","idle-3","wallpaper","bg-landscape","bg-portrait","banner","thumb"]
```

---

## Pendências

- [ ] Renomear `waifu:kaela` → `waifu:astra` em `Domain/Waifus.cs` (nome, title, trait id, skin ids)
- [ ] Gerar os 8 assets com os prompts acima e salvar em `frontend/public/assets/kaelis/astra/`
- [ ] Adicionar entrada no `manifest.json`
