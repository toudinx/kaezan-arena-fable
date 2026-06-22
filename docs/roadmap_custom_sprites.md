# Roadmap — Sprites In-Game Autorais (Kaelis + Bosses)

> **Como usar este arquivo.** Cada `SPR-NN` é uma unidade auto-contida. Dispare com
> **"implemente o prompt SPR-NN do `docs/roadmap_custom_sprites.md`"**. Campos por prompt:
> **Modelo · Effort · Depende de · Aceite · Verificação**.
>
> **Não confundir com:** `docs/roadmap_image_pipeline.md` (pós-processo de arte 2D — upscale/
> removebg/vídeo). Este aqui é **R&D de sprite jogável no grid** e toca o **renderer/AssetsService**.

## Tese

Hoje as entidades no grid usam **sprites do Tibia** (extraídos via AssetExtractor, recoloridos em
runtime). Isso é prático, mas tira identidade visual — as Kaelis premium aparecem no combate como
outfits emprestados. **O maior ganho de identidade do projeto** é dar sprite autoral às
entidades-herói. Mas é o item **mais difícil**: sprite de jogo ≠ arte 16:9.

**Decisão (fechada): abordagem híbrida.** Sprite autoral só para **as 7 Kaelis + os bosses**
(1 por tier). Mobs comuns/elites continuam no Tibia. Assim ganhamos identidade onde mais importa
sem reconstruir a biblioteca inteira.

## A dificuldade real (ler antes de estimar)
O renderer (`frontend/src/app/core/renderer.ts` + `assets.service.ts`, métodos
`drawOutfit`/`drawObject`) espera sprites com:
- **Direções** (o outfit do Tibia tem N direções por pattern);
- **Frames de animação** (idle/andar);
- **Máscara de recolor** opcional (paleta HSI de 133 cores, aplicada em runtime).

Gerar com IA um **sheet direcional e animado consistente** (mesma personagem em todas as direções/
frames) é o problema central — não é "rodar um batch". Por isso **SPR-01 é um spike**: a técnica
vencedora define todo o resto.

## Invariantes
- **Híbrido:** entidade sem sprite autoral **cai no Tibia** (fallback). Nada quebra.
- **Determinismo e render intactos:** não mudar a lógica de tick/colisão; só a camada visual.
- **Sprites só via `AssetsService`** (convenção do frontend) — nenhum componente mapeia caminho direto.
- **IDs estáveis** (`waifu:*`, `monster:*`). Slug = id sem prefixo.
- Geração de imagem **grátis/local quando der** (ComfyUI); sem API paga nova.

---

## SPR-01 — Spike de técnica de sprite  ⭐
- **Modelo:** Opus · **Effort:** high · **Depende de:** — · (Onda 1)
- **Objetivo:** avaliar 2–3 abordagens para um sheet direcional consistente de **1 Kaeli de teste**
  e escolher a vencedora por **consistência entre direções/frames × custo por personagem**.
  Candidatas: (a) gerar pose-base e derivar direções por img2img/ControlNet; (b) modelo de
  pixel/sprite dedicado; (c) render 3D→2D; (d) IA + acabamento manual.
- **Aceite:** 1 Kaeli renderizada no grid de teste com ≥4 direções reconhecíveis como a mesma personagem.
- **Verificação:** carregar no jogo via um caminho de teste e andar nas 4 direções.

## SPR-02 — Contrato de sprite autoral no frontend
- **Modelo:** Opus · **Effort:** medium · **Depende de:** SPR-01 · (Onda 2)
- **Objetivo:** definir como o `AssetsService` carrega um sprite **não-Tibia**: formato de sheet,
  manifest (direções/frames), e se mantém recolor ou fixa cor. Implementar o caminho de carga +
  o **fallback** para Tibia quando não houver sprite autoral.
- **Contexto:** `frontend/src/app/core/assets.service.ts`, `renderer.ts`, `manifest`/`types.ts`.
- **Aceite:** uma entidade com sprite autoral renderiza por ele; sem sprite, usa Tibia, sem regressão.

## SPR-03 — Produzir as 7 Kaelis como sprite in-game
- **Modelo:** Codex (produção) + Opus (revisão) · **Effort:** high · **Depende de:** SPR-02 · (Onda 3)
- **Objetivo:** gerar e integrar os sheets das 7 Kaelis pela técnica vencedora, usando o
  `roster_digest`/assets existentes como referência de identidade.
- **Aceite:** as 7 Kaelis aparecem no grid com sprite autoral; build verde; fallback intacto.

## SPR-04 — Produzir os bosses
- **Modelo:** Codex + Opus · **Effort:** high · **Depende de:** SPR-02 · (Onda 3, paraleliza c/ SPR-03)
- **Objetivo:** sheets autorais dos bosses (1 por tier), mantendo `monster:*` estável.
- **Aceite:** cada boss renderiza com sprite autoral; mobs comuns/elites seguem no Tibia.

## SPR-05 — Polimento + escala visual
- **Modelo:** Opus · **Effort:** medium · **Depende de:** SPR-03, SPR-04 · (Onda 4)
- **Objetivo:** afinar escala/ancoragem/animação no grid, sombras e leitura em combate; documentar
  o processo de "nova entidade autoral" no `tools/README.md`.

---

## Execução
- **Onda 1:** SPR-01 (spike — gate de tudo).
- **Onda 2:** SPR-02 (contrato no frontend).
- **Onda 3:** SPR-03 + SPR-04 (produção, partes distintas → paralelizam).
- **Onda 4:** SPR-05.

## Depois
- Outfits autorais para mais monstros (além de boss), se o spike provar que escala barato.
- Addons/skins jogáveis em sprite autoral (hoje skins são `SkinDef` com lookType do Tibia).
