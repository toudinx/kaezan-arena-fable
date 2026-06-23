# SK-10 — Skin "Casual" · Velvet (gótica off-duty)

> 8 prompts de geração de imagem para o **GPT Image 2.0**, usando
> `frontend/public/assets/kaelis/velvet/base.png` como **imagem de referência** em todos.
> Modo Skin: congela rosto/cabelo/olhos/raça; troca roupa + cenário pelo tema Casual.
> Salvar em `frontend/public/assets/kaelis/velvet/skins/casual/` (ver §Salvamento).

**Bloco de identidade (idêntico nos 8 prompts):**

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin, human (no horns, no wings, no tail,
normal human ears). Palette anchor: dark purple hair, red eyes, fair skin; accent violet + black.
NEW outfit for a CASUAL off-duty skin: oversized black hoodie with violet/purple accents
(drawstrings, inner lining, small print), short pleated skirt, black thigh-high socks, chunky
sneakers; relaxed cozy goth streetwear. Keep it the same person — only the outfit and setting change.
```

**Cenário ancorado (Casual · Velvet):** quarto gótico aconchegante / loja de discos — prateleiras
de vinil, pôsteres de banda, luzes fairy roxas, pelúcias escuras, almofadas, abajur quente. Mood:
relaxado, off-duty, intimista. Acento: roxo/violeta.

---

## idle-1.png · idle-2.png · idle-3.png

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin, human (no horns, no wings, no tail,
normal human ears). Palette anchor: dark purple hair, red eyes, fair skin; accent violet + black.
NEW outfit for a CASUAL off-duty skin: oversized black hoodie with violet/purple accents
(drawstrings, inner lining, small print), short pleated skirt, black thigh-high socks, chunky
sneakers; relaxed cozy goth streetwear. Keep it the same person — only the outfit and setting change.

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, hands in hoodie pocket, relaxed.
- Variant 2: one hand adjusting the hood/collar, weight on one hip, casual gesture.
- Variant 3: hand near face/hair, soft introspective off-duty pose.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

## wallpaper.png

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin, human (no horns, no wings, no tail,
normal human ears). Palette anchor: dark purple hair, red eyes, fair skin; accent violet + black.
NEW outfit for a CASUAL off-duty skin: oversized black hoodie with violet/purple accents
(drawstrings, inner lining, small print), short pleated skirt, black thigh-high socks, chunky
sneakers; relaxed cozy goth streetwear. Keep it the same person — only the outfit and setting change.

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, relaxed off-duty pose.

Background: cozy goth bedroom / record store — shelves of vinyl records, dark band posters,
purple fairy string lights, dark plushies, cushions, a warm lamp glow. Lighting: soft warm
interior light, gentle violet rim light, intimate ambient glow on her face; subtle floating dust.

Style: high quality anime art, same as reference. Mood: relaxed, cozy, off-duty.
Aspect ratio: 16:9 landscape.
```

## bg-landscape.png

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Cozy goth bedroom / record store interior — shelves of vinyl records, dark band posters, purple
fairy string lights, dark plushies, cushions, a warm lamp glow. The center-bottom area where a
character would stand should be slightly illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use o `wallpaper.png` pronto e peça: "Remove the
> character completely and fill the empty space naturally with the background. Keep all lighting
> and atmosphere identical. Return only the background."

## bg-portrait.png

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Cozy goth bedroom / record store, recomposed vertically: emphasize height — tall vinyl shelves
rising up, hanging fairy lights and posters above, cushions and rug in the foreground. Purple
fairy-light glow, warm lamp. Center area slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

## banner.png

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin, human (no horns, no wings, no tail,
normal human ears). Palette anchor: dark purple hair, red eyes, fair skin; accent violet + black.
NEW outfit for a CASUAL off-duty skin: oversized black hoodie with violet/purple accents
(drawstrings, inner lining, small print), short pleated skirt, black thigh-high socks, chunky
sneakers; relaxed cozy goth streetwear. Keep it the same person — only the outfit and setting change.

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight dynamic
pose (hoodie/hair flowing, casual stance). LEFT side intentionally less busy — atmospheric cozy
interior with subtle vinyl/poster/fairy-light motifs, leaving room for text/UI overlay.

Background: dark violet to black gradient with glowing purple particles and soft bokeh from
string lights. Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive banners.
Same as reference. Aspect ratio: 2:1 landscape.
```

## thumb.png

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long dark purple hair, glowing red eyes, fair skin, human (no horns, no wings, no tail,
normal human ears). Palette anchor: dark purple hair, red eyes, fair skin; accent violet + black.
NEW outfit for a CASUAL off-duty skin: oversized black hoodie with violet/purple accents
(drawstrings, inner lining), relaxed cozy goth streetwear. Keep it the same person.

Square portrait (1:1), face and upper chest only, hood loosely down. Expression: relaxed, soft,
a hint of a smile (off-duty, at ease). Background: simple dark violet to black radial gradient,
subtle purple glow behind her — NO complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento

```
frontend/public/assets/kaelis/velvet/skins/casual/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro, ~2:3)
  wallpaper.png                       (16:9, cena completa)
  bg-landscape.png                    (16:9, cena vazia)
  bg-portrait.png                     (9:16, cena vazia)
  banner.png                          (2:1, personagem à direita)
  thumb.png                           (1:1, busto)
```

Depois (passo de **desktop/backend**, fora deste brief): registrar a skin como `SkinDef` em
`Domain/Waifus.cs` e no manifest do `KaeliArtService` para a arte ser reconhecida.
