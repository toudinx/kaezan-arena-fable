# SK-16 — Skin "Verão" · Seren (resort)

> **Como usar.** Cole cada bloco abaixo no **GPT Image 2.0** com
> `frontend/public/assets/kaelis/seren/base.png` anexada como **imagem de referência**. Cada bloco
> está nomeado pelo **arquivo de destino**. O bloco de identidade é **idêntico nos 8** — é a âncora de
> consistência; não reescreva a personagem com outras palavras entre um prompt e outro.
>
> **Tema:** Verão / resort. **Roupa:** maiô prata/azul-gelo, look de resort.
> **Cenário ancorado:** praia tropical / festival de verão — sol forte, água azul-turquesa, palmeiras,
> areia clara. **Acento:** prata / branco / azul-gelo. **Mood:** vibrante, alegre, luz de meio-dia.
>
> **Palette anchor (preservar do `base.png`):** cabelo branco-prateado longo em rabo de cavalo alto,
> olhos azuis, pele clara, humana (sem traços não-humanos). Só a roupa e o cenário mudam.

---

## Bloco de identidade (idêntico nos 8 prompts)

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
very long silver-white hair worn in a high ponytail with loose front strands, bright blue eyes,
fair skin. Human — no horns, no wings, no tail, no animal ears (normal human ears).
NEW outfit for a Summer / resort skin: an elegant silver and ice-blue swimsuit — a sleek metallic-silver
one-piece (or stylish resort bikini) with pale ice-blue trim, paired with a sheer white sarong/wrap and
delicate silver jewelry; barefoot or light strappy sandals.
Keep it the same person — only the outfit and setting change. Palette accent: silver / white / ice-blue.
```

---

## `idle-1.png` · `idle-2.png` · `idle-3.png` (corpo inteiro, transparente)

```
[BLOCO DE IDENTIDADE]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, calm resort vibe.
- Variant 2: one hand resting on hip, sarong catching a light breeze, confident elegant gesture.
- Variant 3: one hand brushing the high ponytail near her face, relaxed introspective pose.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

## `wallpaper.png` (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible.

Background: bright tropical beach resort at midday — turquoise-blue water, pale clear sand,
swaying palm trees, distant white parasols and a summer festival mood, clear sunny sky.
Lighting: strong warm midday sun, soft ice-blue rim light on her silver hair; gentle ocean glow;
floating sparkles of sea spray and light particles.

Style: high quality anime art, same as reference. Mood: vibrant, joyful, summery.
Aspect ratio: 16:9 landscape.
```

---

## `bg-landscape.png` (cenário vazio, 16:9 — para parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Bright tropical beach resort at midday — turquoise-blue water, pale clear sand, swaying palm trees,
distant white parasols and summer festival mood, clear sunny sky, gentle sea spray. The center-bottom
area where a character would stand should be slightly illuminated, ready for a character to be
composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use a imagem do **wallpaper** já pronto e peça:
> "Remove the character completely and fill the empty space naturally with the background.
> Keep all lighting and atmosphere identical. Return only the background."

---

## `bg-portrait.png` (cenário vazio, 9:16 — fundo da página Kaelis)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Bright tropical beach resort recomposed vertically — emphasize height: tall palm trees rising,
a high sunny sky fading from pale to deep blue, turquoise water meeting pale sand in the lower third,
distant parasols. Center area slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

## `banner.png` (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose (sarong and ponytail flowing in the sea breeze). LEFT side intentionally less busy —
atmospheric background with subtle summer motifs (soft sun glare, palm silhouettes, sparkling water),
leaving room for text/UI overlay.

Background: warm ice-blue to deep-turquoise gradient with glowing sea-spray particles and sun bloom.
Style: premium summer anime, like Genshin Impact / Blue Archive seasonal banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

---

## `thumb.png` (busto, 1:1)

```
[BLOCO DE IDENTIDADE]

Square portrait (1:1), face and upper chest only. Expression: serene and cool, a soft confident smile,
calm summer poise. Background: simple, ice-blue to white radial gradient, subtle warm glow behind her —
NO complex background elements (must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Salvamento

Depois de gerar as imagens no GPT Image 2.0 + pós-processo (ComfyUI: upscale/removebg/crop pela trilha
do `roadmap_producao_visual.md`), salvar em:

```
frontend/public/assets/kaelis/seren/skins/verao/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

Tornar a skin **jogável** (registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do
`KaeliArtService`) é um passo de **desktop/backend**, fora deste brief (ver `## Depois` do roadmap).
