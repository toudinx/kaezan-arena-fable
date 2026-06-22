---
name: kaeli-social-prompts
description: >-
  Gera um set de posts de social/Instagram para uma Kaeli do Kaezan Arena Fable: para cada post,
  um prompt de geração de imagem (1:1 feed e/ou 4:5 retrato) que mantém a identidade visual da
  personagem, mais caption (PT e EN) e hashtags. Usa a mesma técnica de "bloco de identidade" da
  skill kaeli-asset-prompts para a personagem sair consistente entre os posts. Use SEMPRE que o
  usuário pedir para criar posts, campanha de instagram/social, divulgação, "kaeli do dia", teaser
  de banner, lore drop ou conteúdo de redes das Kaelis. É a skill do roadmap_web_social.md no Claude
  Code Web. Não é para os 8 assets do jogo (isso é kaeli-asset-prompts) nem para sprites do Tibia.
---

# Kaeli Social Prompts

Produz o material de um post/campanha de social a partir da identidade de uma Kaeli: prompts de
imagem prontos para o GPT Image 2.0 / DALL·E + caption + hashtags. Quem gera as imagens é o usuário.

## Quando rodar (no Claude Code Web)
Disparada pelos prompts `SO-*` do `docs_web/roadmap_web_social.md`. Respeita `docs_web/CLAUDE_WEB.md`:
**lê** só `README.md` + `docs_web/roster_digest.md` (+ doc nomeado), **escreve** só em
`docs_web/social/<slug>-<campanha>.md`. Não toca em código nem gera imagem.

## Fluxo

### Passo 1 — Bloco de identidade (consistência)
Pegue a identidade da Kaeli no `roster_digest.md`. Se ela tiver bloco congelado (ex. Velvet), use-o
**sem alterar**. Senão, monte um bloco curto a partir da identidade visual do digest (cabelo, olhos,
roupa, acessórios, paleta) — mesma técnica da skill `kaeli-asset-prompts`. Esse bloco entra no topo
de **todo** prompt de imagem do post, para a personagem não "mudar" entre posts.

### Passo 2 — Definir a campanha
Confirme com o usuário só o que faltar: **Kaeli(s)**, **tema da campanha** e **quantos posts**
(default 3). Tipos comuns: lançamento de banner (teaser → reveal → disponível), kaeli do dia,
evento sazonal, lore drop, bastidores.

### Passo 3 — Emitir os posts
Para cada post, entregue um bloco com:
- **Prompt de imagem** (bloco de código) = bloco de identidade + cena/enquadramento do post.
  Formato: **1:1** (feed) por padrão; ofereça **4:5** quando ocupar mais tela ajudar; **9:16** p/ story.
- **Caption (PT)** — gancho na 1ª linha, corpo curto, CTA. Tom = voz da Kaeli (personalidade do digest).
- **Caption (EN)** — tradução natural, não literal.
- **Hashtags** — mix de nicho (gacha/anime art) + marca do jogo; sem spam (8–15).

### Passo 4 — Fechar
Lembre onde salvar as imagens e que captions/hashtags são rascunho editável. Numere os posts na
ordem de publicação sugerida.

## Template de prompt de imagem (por post)

```
[BLOCO DE IDENTIDADE DA KAELI]

Social media post illustration, [proporção: square 1:1 / portrait 4:5]. [enquadramento: ex.
upper-body hero shot / full scene / close portrait]. Background: [cenário ancorado da Kaeli, do
digest — ou variação do tema da campanha]. Mood: [mood da Kaeli]. Leave [espaço] for caption text
overlay if needed.

Style: premium dark-fantasy anime, same identity as described. High quality, [acento] accent lighting.
```

## Exemplo de bloco de saída (1 post)

```markdown
### Post 1 — Teaser do banner (1:1)

**Imagem:**
[prompt de imagem aqui]

**Caption (PT):**
A noite está prestes a ganhar uma arautidade. 🩸 Em breve no Kaezan Arena Fable.

**Caption (EN):**
The night is about to gain a herald. 🩸 Coming soon to Kaezan Arena Fable.

**Hashtags:**
#gacha #animeart #darkfantasy #kaezanarenafable #gachagame #waifu #characterdesign
```

## Notas
- **Consistência > variedade:** todos os posts da mesma Kaeli compartilham o bloco de identidade.
- **Só nossas Kaelis** nas imagens — nunca IP de terceiros.
- Uma campanha por arquivo. Gera **prompts + texto**, não imagens.
