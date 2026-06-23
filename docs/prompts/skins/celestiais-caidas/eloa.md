# Skin "Celestiais Caídas" — Eloa (SK-03)

> **8 prompts de asset** para a skin **Celestiais Caídas** da Eloa (anjo caído, asas de penas em
> destaque). Cole cada bloco no **GPT Image 2.0** usando
> `frontend/public/assets/kaelis/eloa/base.png` como **imagem de referência**. Isto são **prompts**,
> não imagens.
>
> **Modo Skin:** congela rosto/cabelo/olhos/raça (asas de penas) e **substitui** a segunda-pele
> neutra da base pelo manto branco-e-ouro rasgado do tema. O bloco de identidade abaixo é **idêntico**
> nos 8 prompts — é a âncora de consistência. Só muda enquadramento, fundo e pose.
>
> **Cenário ancorado (linha Celestiais Caídas):** reino celestial em colapso — catedral nas nuvens se
> partindo, halos quebrados flutuando, luz dourada vazando por fendas no céu. **Setor da Eloa:** a
> catedral celestial desabando — colunas e vitrais se partindo, penas caindo na luz dourada, halo
> dela escurecendo. Acento: monocromático (preto-e-branco) + brilho rosa + ouro celestial. Mood:
> épico, dramático, queda divina no auge.

---

## Bloco de identidade (idêntico nos 8)

```
Using this character as reference, keep her face, hair and eyes EXACTLY, and keep her angel race
feature: very long straight black hair, glowing pink eyes, fair skin, and large black-and-white
feathered angel wings. Palette anchor (do NOT drift): black hair, glowing pink eyes, fair skin;
black-and-white feathers; accent monochrome + pink glow + celestial gold.
NEW outfit for a "Fallen Celestial" skin: a torn flowing white-and-gold mantle/robe with gold
filigree, draped to leave the feathered wings fully exposed and on display; a darkening, dimming
halo above her head. Keep it the same person — only the outfit and setting change.
```

---

## idle-1.png · idle-2.png · idle-3.png (corpo inteiro, transparente)

```
[BLOCO DE IDENTIDADE]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same anime art style as the reference image. Feathered wings fully open and on
display, a few loose feathers drifting near her.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides, wings spread wide.
- Variant 2: one hand raised to chest/collar, subtle elegant gesture, wings half-furled.
- Variant 3: one hand raised near face or hair, introspective pose, head tilted down slightly.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

---

## wallpaper.png (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible, feathered wings fully open and dramatic.

Background: a collapsing celestial cathedral — towering columns and stained glass breaking apart,
broken halos floating, golden light leaking through cracks in the sky, feathers falling through the
light. Lighting: dramatic, backlit, golden rim light with a soft pink glow on her face; her own halo
darkening; floating feathers and gold dust drifting down.

Style: high quality anime art, same as reference. Mood: epic, dramatic, fallen angel at the peak of
the fall. Aspect ratio: 16:9 landscape.
```

---

## bg-landscape.png (cenário vazio, 16:9 — parallax)

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

A collapsing celestial cathedral — towering columns and stained glass breaking apart, broken halos
floating, golden light leaking through cracks in the sky, feathers falling through the light, gold
dust drifting. The center-bottom area where a character would stand should be slightly illuminated,
ready for a character to be composited in.

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

A collapsing celestial cathedral, recomposed VERTICAL: emphasize height — towering broken cathedral
columns and tall stained-glass windows rising, cracks of golden sky-light above, broken halos
floating up the frame, feathers and gold dust drifting down through the foreground. Center area
slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

---

## banner.png (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight dynamic
pose (mantle cloth and black hair flowing, one feathered wing sweeping out). LEFT side intentionally
less busy — atmospheric background with subtle falling feathers, broken halos and gold filigree
motifs, leaving room for text/UI overlay.

Background: deep black-to-gold gradient with glowing feather and gold-dust particles and faint
golden cracks. Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive banners. Same
as reference. Aspect ratio: 2:1 landscape.
```

---

## thumb.png (busto, 1:1)

```
[BLOCO DE IDENTIDADE]

Square portrait (1:1), face and upper chest only — the darkening halo and the start of the feathered
wings visible at the edges. Expression: serene yet sorrowful, divine fall. Background: simple, dark
black-to-gold radial gradient with a subtle pink glow behind her — NO complex background elements
(must read clearly at small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

---

## Onde salvar

Pós-processo (upscale/removebg/crop) e então salve em:

```
frontend/public/assets/kaelis/eloa/skins/celestiais-caidas/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, 16:9)
  bg-landscape.png                    (cena vazia, 16:9)
  bg-portrait.png                     (cena vazia, 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, 1:1)
```

Tornar a skin **jogável** (registrar como `SkinDef` em `Domain/Waifus.cs` + manifest do
`KaeliArtService`) é um passo de **desktop/backend**, fora deste brief (ver `## Depois` no roadmap).
