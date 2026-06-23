# SK-09 — Skin "Miko" · Eloa

> 8 prompts de geração de imagem para a skin **Miko** da Eloa (sacerdotisa celestial, asas de penas
> presentes). Cole cada bloco no **GPT Image 2.0** usando `frontend/public/assets/kaelis/eloa/base.png`
> como imagem de referência. Gera **prompts**, não imagens.
>
> **Modo Skin:** congela rosto/cabelo/olhos/raça (asas) da Eloa; troca roupa + cenário pelo tema Miko.
> **Palette anchor:** cabelo preto longo liso, olhos rosa brilhantes, pele clara, asas de penas
> preto-e-branco (brancas junto ao corpo → pretas nas pontas). Acento: monocromático + brilho rosa /
> ouro celestial.
> **Cenário ancorado (tema Miko · Eloa):** santuário de montanha ao amanhecer — portão torii vermelho,
> degraus de pedra, lanternas de papel, névoa matinal, luz branca dourada. Mood: sagrado e sereno com
> a presença angelical.

---

## Bloco de identidade (idêntico nos 8 — não reescrever)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long straight black hair with light fringe, glowing pink eyes, fair skin, and her large
black-and-white feathered angel wings (white near the body fading to black at the wingtips).
NEW outfit for a celestial Miko (Japanese shrine maiden) skin: a white haori / kimono top with
wide sleeves, a pale-gold hakama with subtle feather motifs, ofuda paper talismans and gold tassels,
white tabi socks and zōri sandals; a thin gold halo motif. Keep the angel wings fully visible and
unchanged. Keep it the same person — only the outfit and setting change.
```

---

## idle-1.png / idle-2.png / idle-3.png  (corpo inteiro, fundo transparente)

```
[BLOCO DE IDENTIDADE acima]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image. Wings fully visible.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, wings calmly spread.
- Variant 2: one hand raised to chest/collar holding an ofuda talisman, serene gesture.
- Variant 3: one hand raised near face or hair, introspective pose, sleeve flowing.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

## wallpaper.png  (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE acima]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, angel wings spread.

Background: Japanese mountain shrine at dawn — large red torii gate, stone steps, hanging paper
lanterns, distant peaks, morning mist drifting low, sacred rope (shimenawa) with paper streamers.
Lighting: soft white-gold sunrise, warm rim light on her wings and hair; gentle ambient glow on her
face; floating golden dust and a few falling white feathers.

Style: high quality anime art, same as reference. Mood: sacred, serene, celestial.
Aspect ratio: 16:9 landscape.
```

## bg-landscape.png  (cenário vazio, 16:9 — parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Japanese mountain shrine at dawn — large red torii gate, stone steps, hanging paper lanterns,
distant peaks, low morning mist, shimenawa rope with paper streamers, soft white-gold sunrise light.
The center-bottom area where a character would stand should be slightly illuminated, ready for a
character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use o **wallpaper** já pronto e peça: "Remove the
> character completely and fill the empty space naturally with the background. Keep all lighting and
> atmosphere identical. Return only the background."

## bg-portrait.png  (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Japanese mountain shrine at dawn, recomposed vertically — emphasize height: a tall red torii gate
rising, long stone stairway climbing upward, hanging lanterns, mountain peaks above, foreground
stone steps with morning mist; soft white-gold sunrise. Center area slightly illuminated for a
character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

## banner.png  (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE acima]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight dynamic
pose (haori sleeves and hair flowing, one wing arcing behind). LEFT side intentionally less busy —
atmospheric background with subtle torii silhouette, paper lanterns and falling feathers, leaving
room for text/UI overlay.

Background: warm dawn gold to deep dark gradient with glowing golden particles.
Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

## thumb.png  (busto, 1:1)

```
[BLOCO DE IDENTIDADE acima]

Square portrait (1:1), face and upper chest only (white haori collar visible, a hint of wing behind).
Expression: calm, gentle, quietly radiant. Background: simple, dark radial gradient from soft gold to
black, subtle glow behind her — NO complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento

Gerar cada imagem no GPT Image 2.0 com a `base.png` da Eloa, pós-processar (upscale/removebg/crop)
pela trilha do `roadmap_producao_visual.md`, e salvar em:

```
frontend/public/assets/kaelis/eloa/skins/miko/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

Tornar a skin **jogável** (registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do
`KaeliArtService`) é um passo de desktop/backend — fora do escopo deste brief.
