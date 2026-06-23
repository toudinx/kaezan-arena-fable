# Skin "Celestiais Caídas" — Rin (SK-01)

> **8 prompts de asset** para a skin **Celestiais Caídas** da Rin (demônio ascendido, asas de
> morcego em destaque). Cole cada bloco no **GPT Image 2.0** usando
> `frontend/public/assets/kaelis/rin/base.png` como **imagem de referência**. Isto são **prompts**,
> não imagens.
>
> **Modo Skin:** congela rosto/cabelo/olhos/raça (súcubo) e **substitui** a segunda-pele neutra da
> base pela regalia carmim-e-preto do tema. O bloco de identidade abaixo é **idêntico** nos 8 prompts —
> é a âncora de consistência. Só muda enquadramento, fundo e pose.
>
> **Cenário ancorado (linha Celestiais Caídas):** reino celestial em colapso — catedral nas nuvens se
> partindo, halos quebrados flutuando, luz dourada vazando por fendas no céu. **Setor da Rin:** ala
> infernal do reino caído — fendas com brasa, brasas subindo, calor avermelhado misturado à luz dourada.
> Acento: carmim + preto + ouro. Mood: épico, dramático, queda divina/demoníaca no auge.

---

## Bloco de identidade (idêntico nos 8)

```
Using this character as reference, keep her face, hair and eyes EXACTLY, and keep all her succubus
race features: very long wavy crimson-red hair, glowing magenta-pink eyes, fair skin, pointed ears,
curved black demon horns ringed in gold, large dark bat wings, and a long demon tail with a
heart-shaped tip. Palette anchor (do NOT drift): crimson-red hair, magenta-pink eyes, fair skin;
accent crimson + black + gold.
NEW outfit for a "Fallen Celestial" skin: minimal ornate crimson-and-black regalia with gold
filigree, bare shoulders, flowing cloth that leaves the bat wings fully exposed and on display.
Keep it the same person — only the outfit and setting change.
```

---

## idle-1.png · idle-2.png · idle-3.png (corpo inteiro, transparente)

```
[BLOCO DE IDENTIDADE]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same anime art style as the reference image. Bat wings fully open and on display.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, wings spread wide.
- Variant 2: one hand raised to chest/collar, subtle elegant gesture, wings half-furled.
- Variant 3: one hand raised near face or hair, introspective pose, tail curling forward.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

## wallpaper.png (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, bat wings fully open and dramatic.

Background: infernal sector of a collapsing celestial realm — cathedral in the clouds breaking
apart, broken halos floating, molten fissures glowing with embers, golden light leaking through
cracks in the sky, reddish heat haze. Lighting: dramatic, backlit, crimson-gold rim light; soft
ambient glow on her face; floating ember sparks and gold dust rising.

Style: high quality anime art, same as reference. Mood: epic, dramatic, ascended demon at the peak
of the fall. Aspect ratio: 16:9 landscape.
```

---

## bg-landscape.png (cenário vazio, 16:9 — parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Infernal sector of a collapsing celestial realm — cathedral in the clouds breaking apart, broken
halos floating, molten fissures glowing with embers, golden light leaking through cracks in the sky,
reddish heat haze, rising ember sparks and gold dust. The center-bottom area where a character would
stand should be slightly illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use o **wallpaper** já pronto e peça: "Remove the
> character completely and fill the empty space naturally with the background. Keep all lighting and
> atmosphere identical. Return only the background."

---

## bg-portrait.png (cenário vazio, 9:16 — fundo da página)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

Infernal sector of a collapsing celestial realm, recomposed VERTICAL: emphasize height — towering
broken cathedral arches rising, tall cracks of golden sky-light above, molten fissures and rising
embers in the foreground floor, broken halos floating up the frame. Center area slightly illuminated
for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

## banner.png (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight dynamic
pose (regalia cloth and crimson hair flowing, one bat wing sweeping out). LEFT side intentionally
less busy — atmospheric background with subtle floating embers, broken halos and gold filigree
motifs, leaving room for text/UI overlay.

Background: deep crimson-to-black gradient with glowing ember particles and faint golden cracks.
Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

---

## thumb.png (busto, 1:1)

```
[BLOCO DE IDENTIDADE]

Square portrait (1:1), face and upper chest only — horns and the start of the bat wings visible at
the edges. Expression: confident, alluring, a touch wicked. Background: simple, dark crimson-to-black
radial gradient with a subtle ember glow behind her — NO complex background elements (must read
clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Onde salvar

Pós-processo (upscale/removebg/crop) e então salve em:

```
frontend/public/assets/kaelis/rin/skins/celestiais-caidas/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

Tornar a skin **jogável** (registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do
`KaeliArtService`) é um passo de **desktop/backend**, fora deste brief (ver `## Depois` no roadmap).
