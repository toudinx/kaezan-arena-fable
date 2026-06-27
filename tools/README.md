# tools/ — Pipeline de arte 2D (GPT → ComfyUI)

> Fluxo completo PC ↔ celular: [`docs/WORKFLOW_imagem_e_cutscenes.md`](../docs/WORKFLOW_imagem_e_cutscenes.md)

## Visão geral

```
ChatGPT (celular)
  → output/inbox/<tipo>/<slug>/imagem.png   ← largar o arquivo aqui
  → UI :7879 ou comfyui_batch.py CLI        ← pós-processo local grátis
  → output/upscaled/<tipo>/<slug>/          ← resultado final
```

`<tipo>` = `kaeli | item | mob | background | logo | motion`
`<slug>` = id sem prefixo (ex: `velvet`, `sword-01`)

A geração de imagem fica no GPT (celular, via skill `kaeli-asset-prompts`).
Este pipeline só faz o **pós**: upscale + remoção de fundo.

---

## Rig StabilityMatrix — caminhos e convenções

ComfyUI roda via **StabilityMatrix** instalado em `C:\Kaezan\StabilityMatrix\`.
Os modelos ficam numa pasta compartilhada (usada por todos os pacotes SM):

```
C:\Kaezan\StabilityMatrix\Data\
  Models\
    StableDiffusion\   ← checkpoints SDXL (NetaYume, WAI, Animagine …)
    IpAdapter\         ← modelos IPAdapter (ip-adapter-plus*, ip-adapter-plus-face*)
    ClipVision\        ← CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors
    ControlNet\        ← controlnet-openpose-sdxl-xinsir.safetensors + diffusion_pytorch_model
    Lora\              ← LoRAs SDXL (gameb, character_sheet, ROSprites …)
    DiffusionModels\   ← Wan2.1 I2V fp8
    VAE\               ← wan_2.1_vae.safetensors
    TextEncoders\      ← umt5-xxl-enc-bf16.safetensors
    ESRGAN\            ← RealESRGAN_x4plus_anime_6B.pth, 4xNomos2_hq_dat2 …
    Ultralytics\bbox\  ← face_yolov8m.pt
    Sams\              ← SAM models
  Packages\
    ComfyUI\           ← instalação do ComfyUI
      .venv\Scripts\python.exe   ← Python do ComfyUI (tem torch, safetensors, etc.)
      extra_model_paths.yaml     ← mapeamento automático das pastas acima
```

O `extra_model_paths.yaml` é **gerenciado pelo StabilityMatrix** e já mapeia
`ipadapter`, `clip_vision`, `controlnet`, etc. para as pastas acima.
**Não edite manualmente** — o SM regenera o arquivo ao atualizar pacotes.

> Quando um script precisa de `torch` ou `safetensors` (ex: `gen00_convert_ipadapter.py`),
> use o Python do ComfyUI, não o do sistema:
> ```
> "C:\Kaezan\StabilityMatrix\Data\Packages\ComfyUI\venv\Scripts\python.exe" tools/<script.py>
> ```

---

## Pré-requisitos

### Python 3.10+

Nenhum `pip install` necessário para a UI ou `comfyui_batch.py` — usa só
a biblioteca padrão do Python. Os scripts legados têm requisitos próprios
(ver seção abaixo).

### ComfyUI

Necessário para a UI e `comfyui_batch.py`. Deve responder em `http://localhost:8188`.

```bash
# na pasta do ComfyUI:
python main.py
```

### Modelos de upscale

Colocar em `<ComfyUI>/models/upscale_models/`:

| Modelo | Tamanho | Indicado para |
|---|---|---|
| `RealESRGAN_x4plus_anime_6B.pth` ⭐ | ~17 MB | arte anime (padrão) |
| `4xNomos2_hq_dat2.safetensors` | ~67 MB | arte detalhada/realista |
| `RealESRGAN_x4plus.pth` | ~64 MB | fotos/uso geral |

Download do modelo padrão:
`https://github.com/xinntao/Real-ESRGAN/releases/tag/v0.2.2.4`

### Nó de removebg (para `comfyui_batch.py removebg` e o workflow `removebg_isnet_anime.json`)

No ComfyUI, abra **Manager → Install Custom Nodes** e instale **comfyui-art-venture**
(by sipherxyz). Reinicie o ComfyUI depois.

O subcomando `removebg` (ver CLI abaixo) processa N imagens em lote. O workflow JSON
é para uso manual no ComfyUI (referência / debug).

---

## Iniciar a UI (porta 7879)

```bash
# Opção 1 — clique duplo na raiz do projeto:
kaezan-tools.bat

# Opção 2 — terminal:
python tools/kaezan_tools_ui.py
```

O browser abre automaticamente em `http://localhost:7879`.
O ComfyUI precisa estar rodando em `8188`.

---

## Processar 1 asset (passo a passo)

1. Gere a imagem no ChatGPT (celular).
2. Copie o PNG para `output/inbox/<tipo>/<slug>/`.
3. Inicie a UI (`kaezan-tools.bat`) — o ComfyUI deve estar rodando.
4. Na UI: ajuste **Entrada** para `output/inbox/<tipo>/<slug>`, **Saída** para
   `output/upscaled/<tipo>/<slug>`, e clique **Upscale**.
5. Para assets que precisam de fundo transparente (ver matriz abaixo): na UI,
   mantenha a mesma **Entrada/Saída** e clique **Remove BG (ISNet)**.

### Matriz de pós-processo por tipo de asset

| Tipo | Upscale | Removebg | Transparente |
|---|---|---|---|
| Kaeli — idle-1/2/3 | ✅ | ✅ | sim |
| Kaeli — wallpaper, bg-landscape, bg-portrait, banner, thumb | ✅ | — | não |
| Item (ícone) | ✅ | ✅ | sim |
| Monstro art (card/bestiary) | ✅ | ✅ | sim |
| Background de página | ✅ | — | não |
| Logo | ✅ | ✅ | sim |

**Ordem obrigatória:** sempre upscale primeiro, removebg depois. O modelo de
upscale opera em RGB e produz imagem maior, o que melhora a qualidade da máscara.

---

## CLI — `comfyui_batch.py`

Interface sem UI para automação e scripts. Requer o ComfyUI rodando.

```bash
# Upscale 2x de todos os idles de uma Kaeli (com backup automático)
python tools/comfyui_batch.py upscale \
  --input  output/inbox/kaeli/velvet \
  --glob   "idle-*.png" \
  --output output/upscaled/kaeli/velvet \
  --backup

# Dry-run: lista o que seria processado sem executar
python tools/comfyui_batch.py upscale \
  --input output/inbox/kaeli/velvet --dry-run

# Upscale 4x (qualidade print)
python tools/comfyui_batch.py upscale \
  --input output/inbox/kaeli/velvet --scale 1.0

# Remove BG nos 3 idles de uma Kaeli (com backup automático)
python tools/comfyui_batch.py removebg \
  --input  output/upscaled/kaeli/velvet \
  --glob   "idle-*.png" \
  --output output/upscaled/kaeli/velvet \
  --backup

# Rodar qualquer workflow JSON em API format (não o formato UI do ComfyUI)
python tools/comfyui_batch.py run \
  --workflow tools/workflows/meu_workflow_api.json \
  --input    output/upscaled/kaeli/velvet \
  --glob     "*.png" \
  --output   output/resultado

# Ver versões de backup disponíveis
python tools/comfyui_batch.py restore \
  --backup-dir output/upscaled/_originais \
  --restore-to output/upscaled/kaeli/velvet \
  --list

# Restaurar versão anterior
python tools/comfyui_batch.py restore \
  --backup-dir output/upscaled/_originais \
  --restore-to output/upscaled/kaeli/velvet \
  --version 1
```

---

## GEN-00 — IPAdapter SDXL (pré-requisito da trilha de geração nativa)

> Pré-requisito do `roadmap_comfyui_geracao.md`. Desbloqueia o `IPAdapterUnifiedLoader`
> nos presets `PLUS (high strength)` e `PLUS FACE (portraits)` para SDXL.

### Estado do rig (2026-06-24)

| Modelo | Pasta SM | Status |
|---|---|---|
| `ip-adapter-plus-face_sdxl_vit-h.safetensors` | `IpAdapter/` | ✅ pronto |
| `ip-adapter-plus_sdxl_vit-h.safetensors` | `IpAdapter/` | ❌ só .bin existe |
| `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors` | `ClipVision/` | ✅ pronto |

O `extra_model_paths.yaml` (gerado pelo StabilityMatrix) já mapeia `ipadapter` →
`IpAdapter` e `clip_vision` → `ClipVision`. **Não precisa alterar o yaml.**

### Corrigir o .bin → .safetensors

O preset `PLUS (high strength)` procura `ip-adapter-plus_sdxl_vit-h.safetensors`.
Só o `.bin` está presente — converta com o Python do ComfyUI:

```bat
"C:\Kaezan\StabilityMatrix\Data\Packages\ComfyUI\venv\Scripts\python.exe" ^
    tools/gen00_convert_ipadapter.py
```

O script gera o `.safetensors` na mesma pasta e preserva o `.bin` original.
Após a conversão, **reinicie o ComfyUI** (recarrega a lista de modelos).

### Verificar o rig

```bash
python tools/comfyui_batch.py audit-rig
```

Consulta o ComfyUI em tempo real (`/object_info`) e reporta o status de cada preset
e modelo. Saída esperada quando OK:

```
  ✓  IPAdapterUnifiedLoader  (N presets)
  ✓  PLUS (high strength)
       ip-adapter-plus_sdxl_vit-h.safetensors
  ✓  PLUS FACE (portraits)
       ip-adapter-plus-face_sdxl_vit-h.safetensors
  ✓  CLIP-Vision: CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors
  ✅  Rig OK — GEN-00 completo.
```

### Verificação final (aceite do GEN-00)

Rode o `outpaint --style-ref` da Eloa sem crash — o `--style-ref` usa o preset
`PLUS (high strength)` do `IPAdapterUnifiedLoader`:

```bash
python tools/comfyui_batch.py outpaint \
  -i frontend/public/assets/kaelis/eloa/thumb.png \
  --slug eloa --style-ref --bottom 260
```

---

## GEN-01 — Kaeli premium NATIVA (txt2img, sem GPT)

> Primeira onda geradora do `roadmap_comfyui_geracao.md`. Gera arte de Kaeli **do
> zero** no rig local (NetaYume), com **enquadramento controlado por tipo** — resolve
> a dor-raiz: a `thumb` 1:1 **não corta os seios** (framing explícito + negativo
> anti-crop). Sem censura, sem depender do GPT Image. Depende do **GEN-00**.

### Style bible — `tools/kaeli_style_profiles.json`

Fonte única de verdade por Kaeli (igual o `kaeli_motion_profiles.json` é p/ vídeo).
Nada de prompt solto espalhado. Estrutura:

| Bloco | Papel |
|---|---|
| `_base` | checkpoint, `positive_prefix` (quality tags), `style` (style bible), `negative` (negative bible), sampler/cfg/steps |
| `_frames` | enquadramento por tipo: `thumb` (1:1), `idle` (2:3), `wallpaper` (16:9), `portrait` (9:16), `banner` (2:1), `square`. Cada um traz `width`/`height` (SDXL ~1 MP) + um fragmento `framing` |
| `<slug>` | por Kaeli: `positive` (identidade booru), `negative_extra`, `seed`, `lora` |

O prompt final é montado como
`_base.positive_prefix` + `<Kaeli>.positive` + `--prompt` (extra) + `_frames.<tipo>.framing` + `_base.style`.
Ordem de precedência de params: **flag CLI > perfil da Kaeli > `_base` > default do código**.

### CLI

```bash
# Dry-run (não precisa do ComfyUI) — mostra o prompt montado e o tamanho do frame
python tools/comfyui_batch.py gen --kaeli velvet --frame thumb --count 2 --dry-run

# Gera 3 thumbs da Velvet (1:1, busto inteiro no quadro)
python tools/comfyui_batch.py gen --kaeli velvet --frame thumb --count 3

# Idle 2:3 (corpo inteiro) com refino generativo 2× (detalhe real, tiled, 8 GB-safe)
python tools/comfyui_batch.py gen --kaeli seren --frame idle --hires 2.0

# Kaeli NOVA ainda fora do JSON: identidade vem do --prompt
python tools/comfyui_batch.py gen --kaeli mirai --frame thumb \
  --prompt "1girl, solo, silver twintails, blue eyes, futuristic bodysuit"
```

Saída: `output/gen/<slug>/<frame>-<i>.png` + um `.recipe.json` ao lado (seed +
params exatos p/ reproduzir). Aprovou um look? **trave o `seed`** no perfil da Kaeli
e copie o PNG p/ `frontend/public/assets/kaelis/<slug>/`.

### Parâmetros que importam

| Flag | Efeito |
|---|---|
| `--kaeli` / `--slug` | Qual perfil do style bible usar (e a pasta de saída). |
| `--frame` / `--type` | `thumb` · `idle` · `wallpaper` · `portrait` · `banner` · `square`. Define w×h + framing. |
| `--prompt` | Identidade/extra anexado (ou usado **sozinho** se a Kaeli não está no JSON). |
| `--hires` | **Upscale generativo ~N×** (hires-fix tiled — adiciona detalhe; ESRGAN só amplia). |
| `--seed` / `--count` | Seed base e nº de variações (incrementa por variação). `0` = perfil ou tempo. |
| `--lora` / `--lora-strength` | LoRA de identidade/estilo (default: do perfil). |
| `--checkpoint` | Troca o NetaYume por WAI/Animagine/etc. quando quiser outro look. |

> **8 GB-aware:** os frames já ficam perto de 1 MP (SDXL nativo). Pra resolução cheia,
> use `--hires 2.0` (refino tiled) em vez de rodar o frame gigante — rodar >1.5 MP
> direto estoura a VRAM (mesma lição do `skinvar`). Um job por vez.

---

## IMG-07 — Variante de skin (experimental)

> ⚠️ **Experimental.** Alternativa **grátis/local** ao GPT para criar *skins*
> (trocar roupa/cenário) de uma Kaeli. Só vale a pena se a consistência de rosto
> e pose ficar boa — senão, skins continuam no GPT.

Pega o `idle-1` como base e gera variações trocando roupa/cenário, **mantendo a
pose** (ControlNet) e o **rosto/identidade** (IPAdapter), via img2img:

```
idle-1.png ──┬─ Preprocessor (pose) ─→ ControlNet ─┐
             ├─ IPAdapter (rosto) ─────────────────┤→ KSampler(denoise) → variante
             └─ VAEEncode (img2img) ───────────────┘
```

### Pré-requisitos (além do ComfyUI)

Os **defaults são SDXL** (rig do projeto: RTX 4070 8 GB, checkpoints SDXL).

| Peça | Onde | Obs. |
|---|---|---|
| `ComfyUI_IPAdapter_plus` (cubiq) | custom node | `IPAdapterUnifiedLoader`, `IPAdapter` (rosto) |
| `comfyui_controlnet_aux` (Fannovel16) | custom node | preprocessors openpose/lineart/depth (**opcional** — `canny` usa o node core) |
| Checkpoint SDXL | `models/checkpoints/` | default `waiIllustriousSDXL_v160.safetensors` |
| ControlNet SDXL | `models/controlnet/` | p/ `openpose`, baixe um CN openpose SDXL (fp16 é mais leve nos 8 GB) |
| **CLIP-Vision ViT-H** | `models/clip_vision/` | `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors` — **exigido** pelo IPAdapter `vit-h` |
| IPAdapter SDXL | `models/ipadapter/` | `ip-adapter-plus-face_sdxl_vit-h` (preset PLUS FACE) |

> **Sem CLIP-Vision o IPAdapter não carrega.** Para um teste só da estrutura,
> use `--no-ipadapter` (não precisa de CLIP-Vision).
>
> **`canny` vs `openpose`:** o `canny` preserva o *contorno* → tende a **manter a
> roupa** (bom p/ variar só cenário/cor). Para **trocar a roupa** use `openpose`
> (preserva só a pose) — exige o `comfyui_controlnet_aux` + um CN openpose.

### CLI

```bash
# Dry-run (não precisa do ComfyUI) — só lista o que seria gerado
python tools/comfyui_batch.py skinvar \
  --prompt "elegant red winter dress, snowy castle background" \
  --input  output/upscaled/kaeli/velvet \
  --glob   "idle-1.png" --output output/skins/velvet \
  --name   winter --count 3 --dry-run

# Smoke test só de ControlNet (sem CLIP-Vision) — canny via node core
python tools/comfyui_batch.py skinvar \
  --prompt "elegant red winter dress, snowy castle background" \
  --input  frontend/public/assets/kaelis/velvet \
  --output output/skins/velvet \
  --name   winter --count 1 \
  --control-type canny --no-ipadapter --denoise 0.6

# Roupa NOVA limpa (sem IPAdapter): openpose + denoise alto + prompt booru
python tools/comfyui_batch.py skinvar \
  --prompt "1girl, solo, long black hair, red eyes, ornate red ballgown, white fur trim, off-shoulder, black thighhighs, snowy castle courtyard, night, full moon" \
  --negative "purple dress, black gown, gothic lolita, lowres, bad anatomy, worst quality, blurry, deformed face" \
  --input  frontend/public/assets/kaelis/velvet \
  --output output/skins/velvet --name winter --count 3 \
  --control-type openpose --control-model controlnet-openpose-sdxl-xinsir.safetensors \
  --max-mp 1.0 --denoise 0.85 --no-ipadapter
```

Saída: `<output>/<slug>/idle-1-<name>-<i>.png` (uma por seed).

### Parâmetros que importam

| Flag | Efeito |
|---|---|
| `--denoise` | Quanto muda: `0.6` sutil, `0.85` agressivo (default `0.6`). Troca de roupa quer **~0.85**. |
| `--control-type` | `canny` (core, mantém contorno) · `openpose` (só pose → **troca roupa**) · `lineart` · `depth`. |
| `--control-strength` | Fidelidade à estrutura da base (default `0.7`). |
| `--ipadapter-weight` | Fidelidade ao rosto: `0.2` solto, `0.8` fiel (default `0.8`). |
| `--no-ipadapter` | Pula a metade do rosto (rigs sem CLIP-Vision / **roupa nova limpa**). |
| `--max-mp` | **Redimensiona a base p/ ~N MP antes do img2img** (default `1.0`). Essencial em 8 GB. |
| `--hires` | **Upscale generativo ~N×** (hires-fix tiled, adiciona detalhe real). Ex: `2.0`. `0`=off. |
| `--hires-denoise` | Denoise do refino: `0.3` conserva, `0.45` reinventa (default `0.35`). |
| `--count` / `--seed` | Quantas variações e a seed base (incrementa por variação). |

### Receita comprovada (rig SDXL · RTX 4070 8 GB)

Testado com a Velvet (base 2172×2896). Lições:

- **Sempre `--max-mp 1.0`.** SDXL é treinado em ~1 MP; rodar a base nativa (2-3k px)
  **estoura a VRAM** (crash) e gera textura borrada. A 1 MP: ~40 s/imagem, estável.
  Reupscale o resultado depois pelo subcomando `upscale`.
- **Um job por vez** em 8 GB. Sweeps em alta-res derrubam o ComfyUI.
- **Checkpoint Illustrious/Pony** segue muito melhor **tags booru** (`1girl, red ballgown,
  white fur trim, thighhighs, ...`) do que frase natural. Use negativo **anti-cor**
  (`purple dress, black gown`) para forçar a troca de paleta.
- **Trade-off rosto × cor (IPAdapter PLUS FACE):** aplicado na imagem inteira, em qualquer
  peso que preserve o rosto ele também arrasta a paleta/roupa da base:
  | `--ipadapter-weight` | Resultado |
  |---|---|
  | `0.8` | rosto exato, **roupa volta à cor original** |
  | `0.35` | rosto exato, cor **misturada** |
  | `0.2` | corpete novo, saia volta ao original |
  | `--no-ipadapter` | **roupa 100 % nova/limpa**, rosto vem do prompt |
- **A base NEUTRA resolve o santo graal (1 passo).** Em vez do idle premium, use como `--input`
  uma **base neutra** (a Kaeli numa segunda pele cinza, A-pose, fundo liso — gerada pela skill
  `kaeli-asset-prompts`, modo "Base Neutra"). Como a referência do IPAdapter é cinza, ele
  **preserva o rosto sem arrastar a cor**. Recipe vencedora testada (Velvet):
  ```
  skinvar --input output/skins/velvet --glob base.png \
    --control-type openpose --control-model controlnet-openpose-sdxl-xinsir.safetensors \
    --max-mp 1.0 --denoise 0.85 --ipadapter-weight 0.6
  ```
  → vestido novo limpo **+** rosto exato da Velvet, num passo. **ip-weight 0.6 é o ponto-doce**;
  0.8 começa a "imprimir" o enquadramento vazio da base (figura pequena, fundo estranho).
- **Sem base neutra (usando o idle premium):** o IPAdapter contamina a cor → ou `--no-ipadapter`
  (roupa nova limpa, rosto do prompt) ou ip 0.3–0.5 (rosto travado, recolor parcial).
- **Qualidade/detalhe:** a imagem é *gerada* a ~1 MP — é aí que o detalhe nasce. O subcomando
  `upscale` (Real-ESRGAN) **amplia mas não cria detalhe** (fica grande e mole). Para detalhe real
  (renda, tecido, rosto) use o **`--hires` (upscale GENERATIVO, hires-fix)**: amplia ~N× e refaz um
  passe img2img de denoise baixo, **tiled** (8 GB-safe via `TiledDiffusion` + `VAEEncode/DecodeTiled`).
  Recipe completa testada (Velvet, base neutra):
  ```
  skinvar --input output/skins/velvet --glob base.png \
    --control-type openpose --control-model controlnet-openpose-sdxl-xinsir.safetensors \
    --max-mp 1.0 --denoise 0.85 --ipadapter-weight 0.6 \
    --hires 2.0 --hires-denoise 0.35
  ```
  → 1664×2496 com detalhe real, ~175 s nos 8 GB. `--hires-denoise` 0.3 conserva, 0.45 reinventa mais.

Também disponível na **UI :7879** (seção "Skin Variant") e como ferramenta MCP
`comfy_skinvar`. Workflow de referência: `tools/workflows/skin_variant_img2img.json`.

---

## CUT-03 — Loop de idle premium (`.webm`, experimental, 8 GB-aware)

> ⚠️ **Experimental / opt-in.** Caminho de idle de **alta qualidade**: um loop orgânico
> de "respiração" em vídeo, gerado no ComfyUI a partir do `idle-1`. Quando existe, o
> `<app-kaeli-idle>` toca o `.webm` no lugar do breathing CSS (CUT-02); sem ele, **nada
> muda** (cai no breathing). Render é **só no PC** (precisa GPU + nodes de LivePortrait).

### Fluxo

```
idle-1.png ──┐
             ├─ LivePortrait (driven by short breathing clip) ─→ frames ─→ VideoCombine → idle-loop.webm
clipe curto ─┘   (8 GB: fp16, ~512px, ~2–4 s, loop costurável/pingpong)
```

1. **Brief no celular:** skill `kaeli-motion-prompts` (modo idle) → prompt image-to-video.
2. **Imagem-base:** o `idle-1` já processado (`frontend/public/assets/kaelis/<slug>/idle-1.png`).
3. **Render (PC):** abra `tools/workflows/idle_loop_liveportrait.json` no ComfyUI, aponte o
   `LoadImage` para o `idle-1` e o `VHS_LoadVideo` para um clipe curto de respiração sutil,
   e rode. Saída `.webm` (VP9, `yuva420p` p/ preservar alpha).
4. **Integração:** copie o resultado para
   `frontend/public/assets/kaelis/<slug>/idle-loop.webm` e adicione `"idle-loop"` à lista
   daquela Kaeli em `frontend/public/assets/kaelis/manifest.json`. Pronto — a Kaeli passa a
   tocar o loop; as demais seguem no breathing CSS.

### Pré-requisitos (além do ComfyUI) — **validado no rig do projeto 2026-06-23**

| Peça | Status no rig | Obs. |
|---|---|---|
| `ComfyUI-LivePortraitKJ` (kijai) | ✅ instalado | `DownloadAndLoadLivePortraitModels`, `LivePortraitCropper`, `LivePortraitProcess`, `LivePortraitComposite` |
| `ComfyUI-VideoHelperSuite` (kosinkadink) | ✅ instalado | `VHS_LoadVideoPath` (driving) + `VHS_VideoCombine` (salva o vídeo) |
| `mediapipe` (pip) | ✅ 0.10.35 | usado pelo **`LivePortraitLoadMediaPipeCropper`** → dispensa InsightFace/`buffalo_l` (~300 MB) |
| Modelos LivePortrait | ⚠️ **colocar à mão** | 6 arquivos em `models/liveportrait/` (ver abaixo) |
| Driving clip | ✅ vem no node | `ComfyUI-LivePortraitKJ/assets/examples/driving/d0.mp4` (3 s) e outros |

#### Modelos: colocar à mão em `models/liveportrait/`

> ⚠️ **O auto-download do node falha neste rig.** A venv do ComfyUI (StabilityMatrix) tem um
> OpenSSL quebrado (`OPENSSL_Uplink ... no OPENSSL_Applink`) → qualquer download HTTPS via
> `huggingface_hub` **trava**. Por isso baixe os modelos **pelo navegador** (que tem rede) do
> repo **`Kijai/LivePortrait_safetensors`** e largue em
> `C:\Kaezan\StabilityMatrix\Data\Packages\ComfyUI\models\liveportrait\`:
>
> - `appearance_feature_extractor.safetensors`
> - `motion_extractor.safetensors`
> - `warping_module.safetensors`
> - `spade_generator.safetensors`
> - `stitching_retargeting_module.safetensors`
> - `landmark.onnx`
>
> Com a pasta presente, `DownloadAndLoadLivePortraitModels` **pula o download** e tudo roda
> offline (mediapipe é local). ~130 MB no total.

### Rodar

1. Copie o `idle-1` da Kaeli para `ComfyUI/input/` (ex.: `kz_velvet_idle1.png`).
2. Carregue `tools/workflows/idle_loop_liveportrait.json` no ComfyUI (ou submeta via `/prompt`).
3. Rode. Saída: `ComfyUI/output/kaeli_idle_loop_*.webm` (codec **AV1**).
4. Converta p/ o formato do frontend (VP9 **com alpha**, p/ a arte transparente sobrepor o
   `bg-portrait`) e recorte o loop, com o ffmpeg já instalado:
   ```bash
   ffmpeg -y -i kaeli_idle_loop_00001.webm \
     -c:v libvpx-vp9 -pix_fmt yuva420p -b:v 0 -crf 34 \
     frontend/public/assets/kaelis/<slug>/idle-loop.webm
   ```
   > Nota alpha: o `LivePortraitComposite` devolve `full_images` (RGB) + `mask`. Pra alpha real
   > no loop, junte a máscara (`JoinImageWithAlpha`) antes do `VHS_VideoCombine`, ou componha o
   > `idle-1` sobre o `bg-portrait` da Kaeli e gere um loop **opaco** (sem alpha).
5. Adicione `"idle-loop"` à lista daquela Kaeli em
   `frontend/public/assets/kaelis/manifest.json`. Pronto — ela passa a tocar o loop.

> **8 GB-aware:** `fp16`, `dsize 512`, clipe curto, `pingpong: true` (vai-e-volta = loop sem
> "pulo" com metade dos frames). `LivePortraitLoadCropper` (InsightFace) dá rosto mais preciso,
> mas exige baixar `buffalo_l` (~300 MB) — o MediaPipe cropper evita isso.

> **Atalho de CLI:** além de rodar manual no ComfyUI, há o subcomando
> `comfyui_batch.py idleloop` — enfileira o workflow, baixa o vídeo do `VHS_VideoCombine`
> (chave `gifs`/`videos` do `/history`, via `download_video`) e transcoda p/ VP9 `yuva420p`:
> ```bash
> python tools/comfyui_batch.py idleloop -i <head-crop>.png --slug velvet \
>   --output output/cutscenes/velvet/idle-loop-raw.webm \
>   --final  output/cutscenes/velvet/idle-loop.webm
> ```
>
> ⚠️ **Passe um HEAD-CROP, não o `idle-1` cheio.** O cropper MediaPipe (FaceLandmarker +
> BlazeFace *short-range*) **não detecta** o rosto numa figura de corpo inteiro (rosto pequeno
> demais no frame) → `No face detected in FIRST source image`. Crope o `idle-1` num retrato
> onde o rosto ocupe ~40 % do frame (≈ 2.4× a bbox do rosto). O `idleloop` **não** auto-cropa
> (o `comfyui_batch.py` roda no python do sistema, sem cv2/mediapipe — design stdlib-only); use o
> ComfyUI/venv p/ detectar+cropar, ou pré-corte à mão. A saída fica **busto/retrato opaco**
> (`LivePortraitComposite` é RGB → sem alpha, ver nota acima). Pra figura inteira com rosto vivo,
> seria preciso o cropper **InsightFace** (detecta rosto pequeno) + paste-back, ou o caminho **Wan**
> (`wanbust`) p/ peito/cabelo. Teste validado: `output/cutscenes/velvet/idle-loop.webm`.

### CUT-03 ALT — Busto vivo (Wan I2V): peito + cabelo + respiração

> ⚠️ **Experimental, mas validado contra os schemas reais do WanVideoWrapper no rig
> (2026-06-23).** O LivePortrait dirige **só o rosto**. Quando você quer **peito
> subindo/descendo e cabelo balançando** (look WW-style de summon), o caminho é
> **image-to-video** com **Wan2.1 I2V**. Entrada é a **thumb** (busto) — concentra o
> movimento e cabe na resolução baixa dos 8 GB. Saída é **RGB (sem alpha)**: pra compor
> sobre o `bg-portrait`, keyar/removebg por frame (a thumb tem fundo gradiente simples).

```
thumb.png ──→ Wan2.1 I2V 480p fp8 (block-swap + VAE tiling, VAE-only) ──→ frames ──→ VideoCombine → bust.webm
              (8 GB: ~49–81 frames, ~480–512p, 16 fps, pingpong = loop)
```

**Pré-requisitos** (o auto-download via HF trava neste rig — baixe pelo navegador). Os
arquivos abaixo vão na pasta **compartilhada** do StabilityMatrix `Data\Models\<Pasta>\`,
que vira junction p/ `ComfyUI/models/<pasta>/`:

| Peça | Status no rig | Arquivo / pasta |
|---|---|---|
| `ComfyUI-WanVideoWrapper` (Kijai) | ✅ instalado | `WanVideoModelLoader` · `WanVideoBlockSwap` · `WanVideoTextEncode` · `WanVideoImageToVideoEncode` · `WanVideoSampler` · `WanVideoDecode` |
| Wan2.1 I2V 480p fp8 | ✅ baixado | `wan2.1_i2v_480p_14B_fp8_e4m3fn.safetensors` → `DiffusionModels/` |
| Wan2.1 VAE | ✅ baixado | `wan_2.1_vae.safetensors` → `VAE/` |
| UMT5-XXL text encoder | ✅ baixado | `umt5-xxl-enc-bf16.safetensors` → `TextEncoders/` |
| CLIP-Vision | ➖ **não precisa** | `clip_embeds` é opcional no `WanVideoImageToVideoEncode` (só o `vae` é obrigatório) — workflow roda **VAE-only** |

> **Por que VAE-only (sem clip vision):** o `LoadWanVideoClipTextEncoder` valida `log_scale`
> no state dict e exige especificamente o **`open-clip-xlm-roberta-large-vit-huge-14`** — o
> `sigclip_vision_patch14_384` (ou o `CLIP-ViT-H-14-laion2B`) **não** servem e dão
> `Invalid CLIP model`. Como o `clip_embeds` é opcional, o workflow omite o clip vision e
> roda só com o VAE-encode da thumb — ótimo p/ idle sutil (fica fiel à imagem). Pra
> identidade ainda mais fiel/movimento mais guiado, dá pra baixar o `open-clip-xlm-roberta-
> large-vit-huge-14-fp16.safetensors` em `ClipVision/`, adicionar `LoadWanVideoClipTextEncoder`
> + `WanVideoClipVisionEncode` e ligar a saída no `clip_embeds` do encode.

> O `idle_bust_wan_i2v.json` foi **validado lendo os `INPUT_TYPES` dos nodes instalados**
> (nomes de node/inputs conferidos em `nodes.py`/`nodes_model_loading.py`/`nodes_sampler.py`).
> O subcomando faz **override por `class_type`**, então atualizações do pacote que mexam em
> inputs não quebram os overrides de prompt/frames/fps.

**Receita VALIDADA (v8, 2026-06-23)** — único comando que produziu o resultado aprovado (jiggle
sutil + respiração + cabelo + piscada orgânica):

```bash
# 1) gerar (512²)
python tools/comfyui_batch.py wanbust \
  -i frontend/public/assets/kaelis/velvet/thumb.png \
  --lora gameb.safetensors --lora-strength 0.4 --blocks-swap 40
# 2) upscalar p/ 1024² (sem regerar) → bust-up.webm
python tools/comfyui_batch.py wanupscale -i output/cutscenes/velvet/bust-raw.mp4 --slug velvet
```

Saída em `output/cutscenes/<slug>/`: `bust-up.webm` (1024² **final**) · `bust.webm` (512² fonte) ·
`bust-raw.mp4` + `.recipe.json` (seed) · `_experiments/` (versões de iteração).

> 📖 **Lições práticas, alavancas e armadilhas** (block-swap OOM, merge_loras, brightness pump do
> `--fast`, pingpong vs loop nativo, jiggle por LoRA, piscada = loteria de seed, upscale) estão
> consolidadas em [`docs/KNOWLEDGE_wan_idle_bust.md`](../docs/KNOWLEDGE_wan_idle_bust.md). Pra
> renderizar guiado (gerar + upscalar), use a skill **`kaeli-idle-video`**.

> **8 GB-aware:** fp8 + `WanVideoBlockSwap 40` + VAE tiling + `merge_loras=False` (LoRA on-the-fly,
> senão OOM). Transcode final é VP9 **`yuv420p`** (RGB, sem alpha — Wan não tem canal alpha; é o
> oposto do idle-loop do LivePortrait). `--fast` (12 steps) só p/ preview de movimento — o final
> precisa de steps cheios (senão dá brightness pump). Compare com o LivePortrait: o Wan entrega
> peito/cabelo/jiggle que faltavam, ao custo de mais tempo/VRAM.

---

## Convenção de `output/`

```
output/
  inbox/                         ← fila de entrada (drop GPT aqui)
    kaeli/<slug>/
    item/<slug>/
    mob/<slug>/
    background/<slug>/
    logo/<slug>/
    motion/<slug>/
  upscaled/                      ← saída do pós-processo
    kaeli/<slug>/                ← PNGs processados
    item/<slug>/
    ...
    _originais/                  ← backup automático (--backup)
      kaeli/<slug>/idle-1-v1.png ← versionado por execução
```

Ver [`output/inbox/README.md`](../output/inbox/README.md) para a convenção
completa de nomes de arquivo.

---

## Workflows JSON (`tools/workflows/`)

| Arquivo | Função |
|---|---|
| `upscale_2x_anime.json` | Real-ESRGAN 4x → scale 0.5 = net 2x |
| `removebg_isnet_anime.json` | Remoção de fundo com ISNet-anime (⚠️ 1 por vez) |
| `skin_variant_img2img.json` | **IMG-07** img2img + ControlNet (pose) + IPAdapter (rosto) — referência/debug |
| `idle_loop_liveportrait.json` | **CUT-03** loop de idle (LivePortrait) a partir do `idle-1` → `.webm` — referência/debug |
| `idle_bust_wan_i2v.json` | **CUT-03 ALT** busto vivo (Wan2.1 I2V VAE-only) a partir da `thumb` → `.webm` — base que o `wanbust` usa (LoRA injetada por flag) |
| `idle_bust_wan_full.json` | **CUT-03 ALT** versão **carregável na UI** do ComfyUI (LoRA já ligada + params v8, nós 👉 rotulados p/ tweak ao vivo) |
| `upscale_video_2x.json` | **CUT-03 ALT** upscale de vídeo **na UI** (ESRGAN/DAT 512²→1024²) — equivalente ao `wanupscale`, p/ rodar no ComfyUI |
| `idle_bust_<slug>.json` | **CUT-03 ALT** gerados por `comfyui_batch.py emit-ui` — workflow de UI por Kaeli (prompt/params do perfil já embutidos). Fonte: `kaeli_motion_profiles.json` |

Para criar um workflow novo: monte no ComfyUI, ative **Settings → Developer Mode**
e exporte com **Save (API Format)**. Execute com `comfyui_batch.py run --workflow`.

---

## Scripts legados (requerem `pip install`)

> Pré-existem à UI e ao ComfyUI. Use como fallback se o ComfyUI não estiver disponível.

### `upscale_anime.py` — Real-ESRGAN local

```bash
pip install realesrgan basicsr Pillow numpy opencv-python-headless
# GPU CUDA (10-20x mais rápido):
pip install realesrgan basicsr Pillow numpy opencv-python-headless torch torchvision \
  --index-url https://download.pytorch.org/whl/cu121

python tools/upscale_anime.py \
  --input  frontend/public/assets/kaelis \
  --output output/upscaled/kaeli/velvet \
  --glob   "idle-*.png"
# → pesos (~17 MB) baixados automaticamente em tools/weights/
```

### `remove_bg.py` — rembg + isnet-anime local

```bash
pip install rembg onnxruntime Pillow numpy scipy
# GPU:
pip install "rembg[gpu]" onnxruntime-gpu Pillow numpy scipy

python tools/remove_bg.py \
  --input output/upscaled/kaeli/velvet \
  --glob  "idle-*.png" \
  --output output/upscaled/kaeli/velvet
# → modelo (~170 MB) baixado automaticamente em ~/.u2net/
# → use --inplace --yes para sobrescrever sem prompt interativo
```

---

## MCP server — agentes disparam o pipeline (`comfyui_mcp.py`)

`tools/comfyui_mcp.py` expõe o pipeline a agentes Claude via MCP (stdio).
Registrado em `.claude/settings.json` como `comfyui`.

### Ferramentas disponíveis

| Ferramenta | O que faz |
|---|---|
| `comfy_status` | Verifica se o ComfyUI responde em `localhost:8188` |
| `comfy_upscale` | Upscale 2x de uma pasta (`input_dir` obrigatório) |
| `comfy_removebg` | Remove fundo em lote via ISNet-anime |
| `comfy_batch` | Pipeline completo por tipo (`asset_type` obrigatório) |
| `comfy_skinvar` | **IMG-07 (exp.)** variante de skin via img2img + ControlNet + IPAdapter |
| `comfy_list` | Lista arquivos em `output/inbox` ou `output/upscaled` |
| `comfy_validate` | Valida proporção / alpha / set (via `validate_assets.py`) |

### Exemplo de uso pelo agente

```
comfy_batch(asset_type="kaeli", dry_run=True)   → mostra o que seria processado
comfy_batch(asset_type="kaeli")                  → inbox → upscaled (upscale + removebg)
comfy_list(folder="output/upscaled", asset_type="kaeli", slug="velvet")
comfy_validate(asset_type="kaeli")
```

### Registro manual (se `.claude/settings.json` ainda não existir)

```json
{
  "mcpServers": {
    "comfyui": {
      "command": "python",
      "args": ["tools/comfyui_mcp.py"]
    }
  }
}
```

Salve como `.claude/settings.json` na raiz do projeto e reinicie o Claude Code.
O ComfyUI precisa estar rodando em `localhost:8188` para `comfy_upscale`,
`comfy_removebg` e `comfy_batch`. `comfy_list`, `comfy_validate` e
`comfy_status` funcionam sem o ComfyUI.

---

## Outras ferramentas nesta pasta

| Pasta | Função | Documentação |
|---|---|---|
| `AssetExtractor/` | Extrai sprites e dados do cliente Tibia | `README.md` (raiz) |
| `convert-monsters/` | Converte `.lua` de monstros para `monsters.json` | `README.md` (raiz) |
| `cinematics/` (Remotion) | Renderiza cutscenes de summon/reel em `.webm` | `tools/cinematics/README.md` |

Estas ferramentas **não fazem parte do pipeline GPT→ComfyUI** e não são
controladas pela UI de upscale.
