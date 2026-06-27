# kaezan_txt2img_audit.md

Auditoria do ambiente ComfyUI para o pipeline **TEXT-TO-IMAGE** de personagens
originais do Kaezan (Velvet como primeira personagem). Gerado direto da API do
ComfyUI (`/system_stats`, `/object_info`) + inspeção de disco. Nenhum node ou
modelo foi sobrescrito; o único download foi o checkpoint Modelo A (ver §3).

## 1. Instalação do ComfyUI

| Item | Valor |
|---|---|
| Tipo | **StabilityMatrix** (gerenciado) — pacote ComfyUI |
| Caminho | `C:\Kaezan\StabilityMatrix\Data\Packages\ComfyUI\main.py` |
| Versão ComfyUI | **0.26.0** (`deploy_environment: local-git`) |
| Frontend | 1.45.19 |
| Python | 3.12.12 (não-embedded; venv do pacote) |
| PyTorch | **2.12.1+cu130** |
| Args de boot | `--preview-method auto --use-pytorch-cross-attention --enable-manager` |
| API | `http://localhost:8188` (REST; usada via `tools/comfyui_batch.py`) |

> O projeto fala com o ComfyUI **sem MCP**, via REST. Cross-attention é o do
> PyTorch (sem xformers) — relevante para o orçamento de VRAM.

## 2. GPU / VRAM

| Item | Valor |
|---|---|
| GPU | **NVIDIA GeForce RTX 4070 Laptop** |
| VRAM total | **8 GB** (8585 MB) |
| Driver | 595.97 |
| Backend | CUDA `cudaMallocAsync` |
| RAM sistema | 32 GB (≈13 GB livres no momento da auditoria) |

**VRAM durante geração (medido neste pipeline):**
- Base pass SDXL 1024×1024: cabe folgado (checkpoint ~6.9 GB carregado, ComfyUI
  faz offload conforme necessário).
- **Segundo passe a 1536×1536 com VAE *tiled*: validado, SEM OOM** (110.9 s o
  passe base+refine completo no WAI). O `VAEDecodeTiled` é o que torna o 1536²
  viável em 8 GB — `VAEDecode` puro a 1536² é o ponto de risco de OOM.

> **Constraint central do rig: 8 GB.** Por isso: batch=1 sempre, base perto de
> 1024² (SDXL nativo ~1 MP), refine via *latent upscale 1.5×* + decode *tiled*.
> Não rodar a base acima de 1024² nem batch>1.

## 3. Checkpoints

Pasta: `C:\Kaezan\StabilityMatrix\Data\Models\StableDiffusion\`

| Arquivo | Arquitetura | Tamanho | Papel neste pipeline |
|---|---|---|---|
| **animagine-xl-4.0-opt.safetensors** | SDXL | 6.46 GB | **Modelo A (principal)** — baixado nesta task |
| animagineXLV31_v31.safetensors | SDXL | 6.46 GB | Animagine 3.1 (já tinha; comparação) |
| **waiIllustriousSDXL_v160.safetensors** | SDXL (Illustrious) | 6.46 GB | **Modelo B (benchmark)** |
| **hassakuXLIllustrious_betaV06.safetensors** | SDXL (Illustrious) | 6.46 GB | **Modelo B alt (benchmark)** |
| ponyDiffusionV6XL_v6...safetensors | SDXL (Pony) | 6.46 GB | não usado (Pony tem prompt-craft próprio) |
| NetaYumev35_pretrained_all_in_one.safetensors | Lumina 2 | 9.9 GB | default do GEN-01; não-SDXL |
| pixelArtDiffusionXL_spriteShaper.safetensors | SDXL | 6.46 GB | pixel-art (não aplica) |

**Decisão de modelo (aprovada pelo usuário — abordagem híbrida):** baixar só o
**Animagine XL 4.0 Opt** (licença comercial limpa) como Modelo A; usar
**WAI Illustrious v16 + Hassaku** (já instalados, finetunes Illustrious com licença
comercial clara) como slot benchmark — **evitando o Illustrious XL v2.0 Stable**,
cujos termos comerciais são ambíguos/restritivos e conflitam com a regra "tudo
precisa permitir uso comercial".

### Download do Modelo A (com proveniência)
- **Origem oficial:** `huggingface.co/cagliostrolab/animagine-xl-4.0`
  (Cagliostro Research Lab — autores do Animagine).
- **Arquivo:** `animagine-xl-4.0-opt.safetensors`
- **Tamanho verificado (HEAD):** `6.938.350.040` bytes (6.46 GiB) — validado contra
  o `Content-Length` ao final do download.
- **Arquitetura:** SDXL 1.0 base finetune (compatível com o rig 8 GB).
- VAE: usar o **embutido no checkpoint** (não precisa VAE externo).

## 4. text_encoders / diffusion_models / VAEs

- **text_encoders / diffusion_models avulsos:** nenhum relevante (o fluxo usa
  `CheckpointLoaderSimple`, que já traz UNet+CLIP+VAE no `.safetensors`).
- **VAEs avulsas** (`VAELoader`): `wan_2.1_vae.safetensors` (para o pipeline de
  vídeo Wan), `pixel_space`. **Nenhuma necessária aqui** — usamos o VAE do checkpoint.

## 5. LoRAs (13) — pasta `...\Models\Lora\`

| Arquivo | Observação | Uso na 1ª rodada |
|---|---|---|
| GachaSplash.safetensors | estilo splash de gacha | candidata (opcional, 1 por vez) |
| DetailedEyes_V3.safetensors | olhos detalhados | candidata (opcional) |
| StS-Illustrious-Detail-Slider-v1.0 | slider de detalhe | candidata (opcional) |
| iLLC0lorL1nes.safetensors | lineart colorido | candidata (opcional) |
| CharacterDesign-IZT-V1 | design de personagem | candidata (opcional) |
| GBF_Illustrious / GachaSplash / gameb | estilos de jogo | — |
| StS_IllustXL_Breast_Size_Slider | slider de busto | **NÃO usar** (spec proíbe inflar busto) |
| 748cmSDXL, character_sheet, ROSprites-10, Dungeon_Squad_*, E7BB... | diversos | — |

> **Política da 1ª rodada: SEM LoRA** (spec §15). LoRAs só entram depois, uma por
> vez, e só se a licença for comercialmente compatível (verificar caso a caso no
> Civitai). Nomes com "detail/quality/gacha" **não** são garantia de ajuda.

## 6. Upscale models — `ESRGAN/` + `RealESRGAN/`

`4x-AnimeSharp.pth`, `4xNomos2_hq_dat2.safetensors`, `ESRGAN.pth`,
`RealESRGAN_x4plus.pth`, `RealESRGAN_x4plus_anime_6B.pth`.

> Não usados no pipeline txt2img em si (o "segundo passe" é generativo via *latent
> upscale*, que adiciona detalhe real; ESRGAN só suaviza). Disponíveis se quiser um
> upscale final pós-geração.

## 7. CLIP-Vision / ControlNet / embeddings

- **CLIP-Vision** (`CLIPVisionLoader`): `CLIP-ViT-H-14-laion2B-s32B-b79K`,
  `sigclip_vision_patch14_384`. **Não usados** (sem IPAdapter/FaceID — exigência do spec).
- **ControlNet** (`ControlNetLoader`): `controlnet-openpose-sdxl-xinsir`,
  `diffusion_pytorch_model` (canny SDXL). **Não usados** na versão inicial (spec).
- **embeddings:** nenhum textual-inversion relevante mapeado.

## 8. Nodes-chave confirmados (via `/object_info`, nomes REAIS)

`CheckpointLoaderSimple`, `CLIPTextEncode`, `EmptyLatentImage`, `KSampler`,
`VAEDecode`, `SaveImage`, `LatentUpscaleBy`, `VAEDecodeTiled`,
`ImageScaleToTotalPixels`, `UpscaleModelLoader`, `LoraLoader`, `FaceDetailer`,
`UltralyticsDetectorProvider` — **todos presentes**. Total de nodes registrados: alto
(Impact Pack, IPAdapter, controlnet_aux, TiledDiffusion etc. instalados).

**Samplers disponíveis (relevantes):** `euler_ancestral`, `dpmpp_2m`,
`dpmpp_2m_sde`, `dpmpp_3m_sde`, `dpmpp_sde`, `euler`, `dpmpp_2s_ancestral`, etc.
**Schedulers:** `normal`, `karras`, `simple`, `sgm_uniform`, `exponential`, `beta`,
`kl_optimal` — cobrem o que o spec pede (normal + Karras).

## 9. Licenças (resumo — confirmar o card de cada modelo antes de produção)

| Modelo | Licença | Uso comercial das imagens |
|---|---|---|
| **Animagine XL 4.0 Opt** | Fair AI Public License 1.0-SD | **Permitido.** Derivados/redistribuição do *modelo* devem manter a licença e publicar modificações (janela de 30 dias). As **imagens geradas** são de uso comercial livre. |
| WAI Illustrious v16 | base Illustrious-XL = Fair AI Public License 1.0-SD | Geralmente permitido para *outputs* — **confirmar o card no Civitai** (modelo da comunidade). |
| Hassaku XL Illustrious | idem (Illustrious-derivado) | idem — **confirmar card**. |
| ~~Illustrious XL v2.0 Stable~~ | termos comerciais ambíguos/restritivos | **EVITADO** de propósito (motivo da abordagem híbrida). |
| LoRAs | variam, muitas desconhecidas | **1ª rodada sem LoRA.** Verificar por-LoRA antes de uso comercial. |

> Para um jogo que vai shippar, o caminho comercialmente mais seguro é o
> **Animagine XL 4.0** (licença bem documentada) + outputs. Para os finetunes da
> comunidade (WAI/Hassaku), confirme a aba *License/Permissions* do Civitai —
> normalmente liberam uso comercial de imagens, mas o ônus de checar é seu.

## 10. Regras operacionais derivadas do rig

1. `batch_size = 1` sempre.
2. Base pass ≈ 1024² (1 MP). Não subir a base acima disso em 8 GB.
3. Segundo passe: `LatentUpscaleBy 1.5` → KSampler denoise baixo → `VAEDecodeTiled`.
4. Se faltar VRAM: cair para upscale 1.35 (≈1376²/1408²), nunca abaixar a base.
5. Sem IPAdapter/FaceID/ControlNet/img2img nesta fase (exigência do spec).
