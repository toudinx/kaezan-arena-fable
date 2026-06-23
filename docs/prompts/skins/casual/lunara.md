# Skin "Casual" — Lunara (SK-12)

> 8 prompts de geração de imagem para o **GPT Image 2.0**, usando
> `frontend/public/assets/kaelis/lunara/base.png` como **imagem de referência**.
> Modo Skin: congela rosto/cabelo/olhos/orelhas de coelho; troca roupa + cenário pelo tema **Casual**.
> Tema: cozy off-duty, **orelhas de coelho presentes**. Cenário ancorado: cafeteria aconchegante.
>
> **Bloco de identidade** (idêntico nos 8, exceto bg-landscape/bg-portrait que são só cenário):
>
> ```
> Using this character as reference, keep her face, hair and eyes EXACTLY:
> very long wavy silver-lavender hair, blue eyes, fair skin, and her large white rabbit ears
> (moon-hare girl — the rabbit ears stay). Accent palette: lavender / silver / soft pastel.
> NEW outfit for a cozy CASUAL / off-duty skin: oversized soft pastel-lavender hoodie/sweatshirt
> (slightly slouchy, sleeves a bit long), short cozy shorts, and white-and-lavender sneakers.
> Keep it the same person — only the outfit and setting change. The rabbit ears remain visible.
> ```

---

## idle-1.png · idle-2.png · idle-3.png  (3 variantes, fundo transparente)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long wavy silver-lavender hair, blue eyes, fair skin, and her large white rabbit ears
(moon-hare girl — the rabbit ears stay). Accent palette: lavender / silver / soft pastel.
NEW outfit for a cozy CASUAL / off-duty skin: oversized soft pastel-lavender hoodie/sweatshirt
(slightly slouchy, sleeves a bit long), short cozy shorts, and white-and-lavender sneakers.
Keep it the same person — only the outfit and setting change. The rabbit ears remain visible.

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design and identical outfit across all 3:
- Variant 1: neutral standing pose, arms relaxed, hands tucked partly into the long hoodie sleeves.
- Variant 2: one hand raised holding a warm to-go coffee cup near her chest, soft cozy gesture.
- Variant 3: one hand raised near her face/hair, relaxed introspective pose, slight smile.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three. Rabbit ears fully visible.
```

## wallpaper.png  (cena completa, 16:9 landscape)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long wavy silver-lavender hair, blue eyes, fair skin, and her large white rabbit ears
(moon-hare girl — the rabbit ears stay). Accent palette: lavender / silver / soft pastel.
NEW outfit for a cozy CASUAL / off-duty skin: oversized soft pastel-lavender hoodie/sweatshirt
(slightly slouchy, sleeves a bit long), short cozy shorts, and white-and-lavender sneakers.
Keep it the same person — only the outfit and setting change. The rabbit ears remain visible.

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, relaxed off-duty pose holding a warm drink.

Background: cozy modern café interior — warm wood counter, hanging pendant lights, shelves with
mugs and pastries, a big window with soft afternoon light, small potted plants, a chalkboard menu,
gentle steam rising from coffee. Lighting: warm, soft, inviting; gentle lavender rim light on her
hair; floating soft pastel dust motes and faint steam in the light.

Style: high quality anime art, same as reference. Mood: relaxed, cozy, slice-of-life.
Aspect ratio: 16:9 landscape.
```

## bg-landscape.png  (cenário vazio, 16:9 — para parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Cozy modern café interior — warm wood counter, hanging pendant lights, shelves with mugs and
pastries, a big window with soft afternoon light, small potted plants, a chalkboard menu, gentle
steam in the air. The center-bottom area where a character would stand should be slightly
illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use o **wallpaper** já pronto e peça:
> "Remove the character completely and fill the empty space naturally with the café background.
> Keep all lighting and atmosphere identical. Return only the background."

## bg-portrait.png  (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Cozy modern café interior, recomposed VERTICALLY — emphasize height: tall window streaming soft
afternoon light from above, hanging pendant lights descending, a tall shelf of mugs and plants,
warm wood counter in the foreground, gentle steam rising. Center area slightly illuminated for a
character later.

Style: same anime painterly style as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

## banner.png  (personagem à direita, 2:1 landscape)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long wavy silver-lavender hair, blue eyes, fair skin, and her large white rabbit ears
(moon-hare girl — the rabbit ears stay). Accent palette: lavender / silver / soft pastel.
NEW outfit for a cozy CASUAL / off-duty skin: oversized soft pastel-lavender hoodie/sweatshirt
(slightly slouchy, sleeves a bit long), short cozy shorts, and white-and-lavender sneakers.
Keep it the same person — only the outfit and setting change. The rabbit ears remain visible.

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, relaxed cozy
pose (hands in hoodie pocket or holding a coffee cup, hair and ears softly catching the light).
LEFT side intentionally less busy — atmospheric warm café background with subtle bokeh lights and
floating pastel dust, leaving room for text/UI overlay.

Background: warm cream-to-soft-lavender gradient with glowing bokeh and gentle steam.
Style: premium cozy slice-of-life anime, like Blue Archive character banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

## thumb.png  (busto, 1:1 quadrado)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long wavy silver-lavender hair, blue eyes, fair skin, and her large white rabbit ears
(moon-hare girl — the rabbit ears stay). Accent palette: lavender / silver / soft pastel.
NEW outfit for a cozy CASUAL / off-duty skin: oversized soft pastel-lavender hoodie/sweatshirt
collar visible at the shoulders. Keep it the same person — only the outfit changes.

Square portrait (1:1), face and upper chest only, oversized hoodie collar visible. Rabbit ears
visible at the top. Expression: soft, warm, gently smiling, cozy and relaxed. Background: simple
dark lavender-to-black radial gradient, subtle pastel glow behind her — NO complex background
elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Como salvar (passo de desktop, fora deste brief)

Cole cada prompt no **GPT Image 2.0** com a `base.png` da Lunara como referência → pós-processo
ComfyUI (upscale / removebg nos idles / crop) pela trilha do `roadmap_producao_visual.md`. Salve em:

```
frontend/public/assets/kaelis/lunara/skins/casual/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (16:9)
  bg-landscape.png                    (16:9, sem personagem)
  bg-portrait.png                     (9:16, sem personagem)
  banner.png                          (2:1)
  thumb.png                           (1:1)
```

Tornar jogável = registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do `KaeliArtService`
(passo de backend, fora desta etapa de brief).
