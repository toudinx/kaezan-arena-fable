---
name: kaeli-asset-prompts
description: >-
  Gera o conjunto completo de prompts de geração de imagem (para ChatGPT/DALL·E ou
  qualquer gerador image-to-image) que produzem os 8 assets visuais de uma Kaeli do
  Kaezan Arena Fable a partir de UMA imagem-base de personagem: 3 idles transparentes,
  wallpaper, bg-landscape, bg-portrait, banner e thumb. Use SEMPRE que o usuário pedir
  para criar/gerar os assets, arte, imagens, idle, wallpaper, banner ou thumbnail de
  uma Kaeli nova (Mirai, Aurora, Ember, Neva, Sage, Mira, Kaela, Wren, etc.), ou disser
  algo como "quero fazer o mesmo que a Velvet para a Kaeli X", "preciso dos prompts de
  imagem da personagem", ou "gera as artes da [nome]". Também use ao montar o character
  sheet visual de uma Kaeli. Não é para sprites do Tibia (esses vêm do AssetExtractor).
---

# Kaeli Asset Prompts

Gera os prompts de geração de imagem que produzem o set visual completo de uma Kaeli,
a partir de uma imagem-base. É o mesmo fluxo usado para a Velvet, generalizado para
qualquer personagem do roster.

## Por que esta skill existe

O frontend premium (ver `docs/FRONTEND_REMAP.md`) consome **8 assets por Kaeli**. Gerá-los
à mão, um prompt de cada vez, é repetitivo e — pior — frágil: se cada prompt descreve a
personagem com palavras um pouco diferentes, o gerador produz uma "personagem diferente" em
cada imagem. A consistência entre os 8 assets é o que separa um gacha premium de um amador.

A técnica central desta skill resolve isso: **construir um "bloco de identidade" único** —
uma descrição congelada dos traços imutáveis da Kaeli (cabelo, olhos, roupa, acessórios) — e
**injetá-lo literalmente em todos os prompts**. O gerador sempre vê a mesma personagem; só
muda enquadramento, fundo e pose.

## Fluxo

### Passo 1 — Montar o bloco de identidade

Você precisa de uma **imagem-base** da Kaeli (de preferência corpo inteiro, fundo limpo) e dos
metadados dela. Os metadados já existem no backend em `Domain/Waifus.cs` — consulte lá `name`,
`title`, `element`, `rarity` e `description`/`personality` da Kaeli em questão. Se a imagem-base
revelar detalhes visuais que o texto não captura (cor de cabelo/olhos, estilo da roupa), descreva
o que você vê.

> **Modo web (Claude Code Web).** Quando rodando no web (sob a doutrina `docs_web/CLAUDE_WEB.md`),
> **não leia `Domain/Waifus.cs`** (é código). Pegue os metadados e a identidade visual em
> `docs_web/roster_digest.md`, e escreva a saída em `docs_web/skins/<slug>-<tema>.md` (skin) ou
> no destino que o prompt do roadmap indicar.

Pergunte ao usuário **só o que faltar**. O mínimo necessário:

- **`waifu:id`** e **nome** (ex: `waifu:velvet`, "Velvet")
- **Elemento** e **raridade** (definem a paleta de acento e o clima do cenário)
- **Identidade visual**: cor/comprimento de cabelo, cor dos olhos, roupa, acessórios marcantes,
  paleta dominante, e o "mood" (ex: gótico melancólico, radiante sagrado, selvagem bárbaro)

Com isso, escreva o **bloco de identidade** — 2-4 frases, sempre no mesmo formato:

```
Using this character as reference, keep her exact design:
[cor + comprimento de cabelo], [cor dos olhos], [roupa detalhada com cores],
[acessórios marcantes], [calçado], [marcas registradas].
```

> Esse bloco é colado **sem alterações** no topo de cada um dos prompts dos Passos 2. É a âncora
> de consistência. Nunca reescreva a personagem com outras palavras entre um prompt e outro.

### Passo 2 — Emitir os 8 prompts

Para cada asset, combine o bloco de identidade + o template correspondente da seção **Templates**,
preenchendo os trechos `[entre colchetes]` com o cenário/clima derivados do elemento e mood da
Kaeli. Mantenha o cenário **coerente entre os assets** (a mesma "terra natal" visual da Kaeli):
a catedral gótica roxa da Velvet, por exemplo, vira a fonte de todos os fundos dela.

Entregue os prompts em blocos de código separados, prontos para copiar, **nomeados pelo arquivo
de destino**, para o usuário saber qual prompt vira qual arquivo.

### Passo 3 — Instruções de salvamento

Feche sempre lembrando onde os arquivos vão e como registrá-los, seguindo a convenção do
`docs/FRONTEND_REMAP.md`:

```
frontend/public/assets/kaelis/<slug>/
  idle-1.png idle-2.png idle-3.png   (transparente, corpo inteiro)
  wallpaper.png                       (cena completa, landscape 16:9)
  bg-landscape.png                    (cena vazia, landscape 16:9)
  bg-portrait.png                     (cena vazia, portrait 9:16)
  banner.png                          (personagem à direita, 2:1)
  thumb.png                           (busto, quadrado 1:1)
```

Onde `<slug>` é o id sem o prefixo (`waifu:velvet` → `velvet`). Lembre o usuário de adicionar o
id ao `frontend/public/assets/kaelis/manifest.json` para o `KaeliArtService` reconhecer a arte.

## Especificação dos assets

| Arquivo | Conteúdo | Proporção | Fundo |
|---|---|---|---|
| `idle-1/2/3.png` | corpo inteiro, 3 poses | retrato (~2:3) | **transparente** |
| `wallpaper.png` | cena completa, personagem dentro | 16:9 landscape | cenário cheio |
| `bg-landscape.png` | cenário **sem** personagem | 16:9 landscape | cenário cheio |
| `bg-portrait.png` | cenário **sem** personagem | 9:16 portrait | cenário cheio |
| `banner.png` | personagem à direita, espaço à esquerda p/ UI | 2:1 landscape | cenário cheio |
| `thumb.png` | rosto + busto, legível em miniatura | 1:1 quadrado | gradiente simples |

## Templates

Em todos: comece com o **bloco de identidade**, depois o corpo abaixo. Substitua `[...]`.

### idle-1/2/3 (3 variantes, transparente)

```
[BLOCO DE IDENTIDADE]

Full body character art, transparent background (PNG with alpha), no background scene.
Soft even lighting. Same art style as the reference image.

Generate 3 variants as SEPARATE images, identical character design across all 3:
- Variant 1: neutral standing pose, arms relaxed at sides.
- Variant 2: one hand raised to chest/collar, subtle elegant gesture.
- Variant 3: one hand raised near face or hair, introspective pose.

Aspect ratio: portrait (roughly 2:3). Transparent background on all three.
```

### wallpaper (cena completa, 16:9)

```
[BLOCO DE IDENTIDADE]

Create a full cinematic scene wallpaper, 16:9 widescreen. She stands center-slightly-left,
full body visible.

Background: [cenário do elemento — ex: "gothic castle interior at night, purple crystal
chandeliers, chains, glowing runes on the floor, stained glass with violet light, ground mist"].
Lighting: dramatic, backlit, [cor de acento] rim light; soft ambient glow on her face;
floating [cor] particle dust.

Style: high quality anime art, same as reference. Mood: [mood da Kaeli].
Aspect ratio: 16:9 landscape.
```

### bg-landscape (cenário vazio, 16:9) — para parallax

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

[mesmo cenário do wallpaper, descrito sem a personagem]. The center-bottom area where a
character would stand should be slightly illuminated, ready for a character to be composited in.

Style: same anime painterly style as reference. No characters, no silhouettes, no people.
Aspect ratio: 16:9 landscape.
```

> Se o gerador insistir em colocar a personagem, use a imagem do **wallpaper** já pronto e peça:
> "Remove the character completely and fill the empty space naturally with the background.
> Keep all lighting and atmosphere identical. Return only the background."

### bg-portrait (cenário vazio, 9:16) — fundo da página Kaelis

```
Using this image as STYLE reference, create ONLY the background scene, NO character present.

[mesmo cenário, recomposto em vertical: enfatize altura — arcos altos, janelas que sobem,
chão em primeiro plano com runas]. Center area slightly illuminated for a character later.

Style: same as reference. No characters. Aspect ratio: 9:16 portrait (tall).
```

### banner (personagem à direita, 2:1)

```
[BLOCO DE IDENTIDADE]

Gacha game character banner, 2:1 landscape. Character on the RIGHT side, 3/4 body, slight
dynamic pose (dress/hair flowing). LEFT side intentionally less busy — atmospheric background
with subtle [motivos do elemento], leaving room for text/UI overlay.

Background: [cor escura] to black gradient with glowing particles.
Style: premium dark-fantasy anime, like Genshin Impact / Blue Archive banners. Same as reference.
Aspect ratio: 2:1 landscape.
```

### thumb (busto, 1:1)

```
[BLOCO DE IDENTIDADE]

Square portrait (1:1), face and upper chest only. Expression: [traço de personalidade —
ex: "cold, composed, hint of melancholy"]. Background: simple, dark [cor do elemento] to black
radial gradient, subtle glow behind her — NO complex background elements (must read clearly at
small UI sizes).

Style: high quality anime art, same as reference. Aspect ratio: 1:1 square.
```

## Exemplo (Velvet, 5★ Death/Holy — gótica)

Bloco de identidade gerado:

```
Using this character as reference, keep her exact design:
very long dark purple hair, glowing red eyes, black-and-purple gothic lolita dress with
lace and ruffles, purple flower hair ornament and cat-ear tiara crown, black thigh-high
stockings, black lace heels.
```

Cenário ancorado: catedral gótica noturna, lustres de cristal roxo, correntes, runas roxas no
chão, vitrais violeta, névoa rasa. Acento: roxo. Mood: dark, ethereal, melancholic.
→ Esse bloco + cenário entram nos 6 templates acima para gerar os 8 arquivos da pasta `velvet/`.

## Modo Skin (roupa/tema alternativo de uma Kaeli existente)

Uma **skin** é o mesmo set de 8 assets, mas com a Kaeli vestindo outra roupa e em outro cenário
(ex. "Eloa de verão", "Velvet noiva"). O objetivo é que ela continue **reconhecível** como a mesma
personagem. Disparada pelos prompts `SK-*` do `docs_web/roadmap_web_skins.md`.

A única diferença em relação ao fluxo normal está no **bloco de identidade**: em vez de congelar
roupa+acessórios, você congela só os traços **imutáveis** da personagem e **substitui** o resto
pelo tema.

1. **Preserve (do digest):** rosto/feições, cor e comprimento de cabelo, cor dos olhos, raça/silhueta
   (orelhas, asas, chifres, cauda — o que for da personagem, não da roupa).
2. **Substitua pelo tema:** roupa, acessórios, calçado, e **cenário ancorado** (o tema manda agora —
   praia ensolarada para "verão", salão natalino para "natal", etc.).

Formato do bloco de identidade no modo skin:

```
Using this character as reference, keep her face, hair and eyes EXACTLY:
[cor + comprimento de cabelo], [cor dos olhos], [traços de raça imutáveis se houver].
NEW outfit for a [tema] skin: [roupa nova detalhada com cores], [acessórios do tema], [calçado].
Keep it the same person — only the outfit and setting change.
```

Depois siga os **mesmos 6 templates** dos Passos 2 (idles, wallpaper, bg-landscape, bg-portrait,
banner, thumb), trocando o cenário ancorado pelo cenário do tema. Salve em
`docs_web/skins/<slug>-<tema>.md` (no web) e lembre que tornar a skin **jogável** (entrar como
`SkinDef` em `Waifus.cs`) é um passo de **desktop**.

## Notas

- **Uma Kaeli por vez.** Os 8 assets de uma personagem compartilham a mesma identidade e cenário.
- A imagem-base é a fonte de verdade visual; o texto só reforça. Se o usuário trocar a base,
  reconstrua o bloco de identidade.
- Isto gera **prompts**, não imagens — quem gera é o ChatGPT/gerador do usuário. Não tente
  invocar APIs de imagem.
- Para animar os idles (breathing/physics) depois, ver `docs/FRONTEND_REMAP.md` (CSS crossfade)
  e a discussão de Remotion/Kling — fora do escopo desta skill.
