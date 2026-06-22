# Roadmap — Pipeline de Imagem (ComfyUI local)

> **Como usar este arquivo.** Cada `IMG-NN` / `VID-NN` é uma unidade auto-contida. Dispare com
> **"implemente o prompt IMG-NN do `docs/roadmap_image_pipeline.md`"**. Cada prompt declara
> **Modelo · Effort · Depende de · Aceite · Verificação**.
>
> **Não confundir com:** `docs/roadmap_custom_sprites.md` (sprites in-game autorais — outra fera,
> toca o renderer) e a trilha web (`docs_web/`). Este toca **`tools/` (Python + workflows ComfyUI)**
> e os PNGs de asset em `frontend/public/assets/` / `output/`. **Nenhuma regra de jogo.**

## Tese

O fluxo de arte é: **GPT Image 2.0 gera** (Kaelis, items, monstros, backgrounds, logos) →
**ComfyUI local pós-processa** (upscale + remoção de fundo + crop) → entra no jogo. A geração fica
no GPT (escolha do projeto e já funciona bem); esta frente conserta e **generaliza o pós-processo
local grátis** para **todos os tipos de asset** (hoje só trata Kaeli, e só o upscale roda em lote),
e abre o ComfyUI para **agentes** via MCP. Nada aqui depende de API paga.

## O que já existe
- `tools/kaezan_tools_ui.py` — UI web (porta 7879) que dispara jobs de batch.
- `tools/comfyui_batch.py` — orquestrador dos workflows no ComfyUI.
- `tools/upscale_anime.py` (✅ funciona em lote) · `tools/remove_bg.py` (⚠️ **batch quebrado**).
- `tools/workflows/upscale_2x_anime.json` · `removebg_isnet_anime.json`.
- `output/upscaled/<kaeli>/…` e `output/upscaled/_originais/<kaeli>/*-vN.png`.

## Matriz de pós-processo por tipo de asset
| Tipo | upscale | removebg | proporção | observação |
|---|---|---|---|---|
| Kaeli — idle-1/2/3 | sim | **sim** | ~2:3 | transparente |
| Kaeli — wallpaper/bg-landscape | sim | não | 16:9 | |
| Kaeli — bg-portrait | sim | não | 9:16 | |
| Kaeli — banner | sim | não | 2:1 | |
| Kaeli — thumb | sim | não | 1:1 | |
| **Item** (ícone) | sim | **sim** | 1:1 | transparente |
| **Monstro art** (common/elite/boss) | sim | **sim** | conforme uso | card/bestiary (≠ sprite in-game) |
| **Background de página** | sim | não | 16:9 / 9:16 | |
| **Logo** | sim | **sim** | variável | transparente |

> Geração desses assets continua no GPT. O Comfy só faz o **pós**. Destinos de item/monstro/logo
> que ainda não existem: deixar TODO "confirmar no desktop".

## Invariantes
- **Nenhuma API paga.** Só ComfyUI local + Python. Sem serviço externo.
- **Não tocar no jogo** (`backend/`, `frontend/src/`). Só `tools/`, `output/` e PNGs de asset.
- **Slug estável** (`<slug>` = `waifu:*` sem prefixo); `manifest.json` sempre em sincronia.

---

## IMG-01 — Documentar o pipeline atual  ⭐
- **Modelo:** Codex · **Effort:** low · **Depende de:** — · (Onda 1)
- **Objetivo:** `tools/README.md` explicando o fluxo GPT→ComfyUI, como subir a UI (7879),
  pré-requisitos (ComfyUI + modelos), e a convenção de `output/`.
- **Aceite:** alguém novo processa 1 asset lendo só o doc.

## IMG-02 — Consertar o removebg em lote  ⭐
- **Modelo:** Codex · **Effort:** medium · **Depende de:** IMG-01 · (Onda 1)
- **Objetivo:** o removebg (ISNet anime) tem que rodar em **lote** igual ao upscale. Diagnosticar
  por que só o upscale funciona em batch e corrigir no `comfyui_batch.py` + `remove_bg.py` + UI.
- **Aceite:** apontar uma pasta com N imagens e receber N PNGs com alpha real, sem rodar 1 a 1.
- **Verificação:** lote nos 3 idles de uma Kaeli → 3 transparentes corretos.

## IMG-03 — Batch genérico por tipo de asset  ⭐
- **Modelo:** Codex · **Effort:** medium · **Depende de:** IMG-02 · (Onda 2)
- **Objetivo:** generalizar o batch além de Kaeli: um **config por tipo** (upscale on/off, removebg
  on/off, proporção-alvo, destino) cobrindo Kaeli/item/monstro/background/logo (ver matriz acima).
- **Tarefas:** tabela de tipos; seleção de tipo na UI; idempotência; copiar pro destino certo.
- **Aceite:** processar um lote de itens e um de backgrounds, cada um com o tratamento correto.

## IMG-04 — Crop + validação por tipo
- **Modelo:** Codex · **Effort:** low · **Depende de:** IMG-03 · (Onda 3)
- **Objetivo:** crop centralizado p/ a proporção-alvo + validador que falha se faltar alpha onde
  devia, se a proporção estiver fora, ou se faltar arquivo no set (ex. os 8 de uma Kaeli).
- **Aceite:** validação numa pasta reporta OK/erros por asset.

## IMG-05 — MCP do ComfyUI local (agentes disparam workflows)  ⭐
- **Modelo:** Codex · **Effort:** medium · **Depende de:** IMG-03 · (Onda 3, paraleliza c/ IMG-04)
- **Objetivo:** expor o pipeline a **agentes** via MCP, apontando pro ComfyUI local — rodar
  upscale/removebg/batch por chamada de ferramenta, sem a UI. Avaliar reusar um MCP de ComfyUI
  open-source (ex. `shawnrushefsky/comfyui-mcp`) vs. um wrapper fino sobre o `comfyui_batch.py`.
  **Grátis/local, sem API paga.**
- **Aceite:** um agente consegue disparar o pós-processo de uma pasta e receber os finais.

## IMG-06 — Consistência de rosto (face detailer/restore)
- **Modelo:** Codex · **Effort:** medium · **Depende de:** IMG-03 · (Onda 3)
- **Objetivo:** workflow opcional de *face detailer*/restore no upscale p/ rosto nítido e consistente
  entre idle/banner/thumb; toggle no batch.

## IMG-07 — (Experimental) Variante de skin via img2img local
- **Modelo:** Opus · **Effort:** high · **Depende de:** IMG-03 · (Onda 4)
- **Objetivo:** explorar img2img + ControlNet/IPAdapter pegando o `idle-1` como base e trocando
  roupa/cenário mantendo pose e rosto — alternativa **grátis** ao GPT para skins. Só vale se a
  consistência ficar boa; senão, skins seguem no GPT.

---

## Vídeo (image-to-video) — marketing-first  🎬
ComfyUI roda modelos de vídeo locais (WAN/LTX/SVD/AnimateDiff), grátis. **Começamos pelo ROI seguro:
reels e teasers pro Instagram** (cruza com `docs_web/roadmap_web_social.md`). Animação in-engine
(skills/summon) fica como experimental no fim.

## VID-01 — Spike de modelo de vídeo local
- **Modelo:** Opus · **Effort:** medium · **Depende de:** IMG-01 · (Onda 3)
- **Objetivo:** escolher modelo/workflow de vídeo no ComfyUI e gerar **1 reel de teste** a partir de
  uma arte existente (ex. wallpaper de uma Kaeli). Avaliar qualidade × custo de GPU.
- **Aceite:** 1 clipe curto (loop) renderizado localmente, sem API paga.

## VID-02 — Pipeline de reels/teaser pra social
- **Modelo:** Codex · **Effort:** medium · **Depende de:** VID-01 · (Onda 4)
- **Objetivo:** automatizar clipes curtos 9:16/1:1 (parallax/ken-burns + motion) a partir de
  wallpaper/banner de uma Kaeli, prontos pro Instagram.
- **Aceite:** gerar um reel de banner reaproveitando assets já prontos.

## VID-03 — (Experimental) cinematic de reveal/summon
- **Modelo:** Opus · **Effort:** high · **Depende de:** VID-01 · (Onda 5)
- **Objetivo:** explorar uma cena cinematográfica de reveal de banner/summon. Marketing primeiro;
  uso in-engine só se valer a pena.

---

## Execução paralela
- **Onda 1:** IMG-01 → IMG-02 (conserto do removebg, prioridade).
- **Onda 2:** IMG-03 (batch genérico).
- **Onda 3:** IMG-04 + IMG-05 + IMG-06 + VID-01 (todos dependem do batch; tocam partes distintas).
- **Onda 4:** IMG-07 + VID-02.
- **Onda 5:** VID-03.

## Depois
- Animar idles in-game (breathing/physics) — ver `docs/FRONTEND_REMAP.md`.
- Geração local própria (Flux/SDXL) se um dia quiser sair do GPT — fora de escopo enquanto o GPT entregar.
- Sprites in-game autorais (Kaelis + bosses) — vivem em `docs/roadmap_custom_sprites.md`.
