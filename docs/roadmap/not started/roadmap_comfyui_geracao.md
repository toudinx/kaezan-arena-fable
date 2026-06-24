# Roadmap — ComfyUI como GERADOR nativo (Kaelis premium · consistência · poses · roupas · cenário · banner · vídeo)

> **Como usar este arquivo.** Cada `GEN-NN` é uma unidade auto-contida. Dispare com
> **"implemente o GEN-NN do `docs/roadmap/not started/roadmap_comfyui_geracao.md`"**. Cada item declara
> **Modelo · Effort · Depende de · Aceite · Verificação**. Roda **só no PC** (ComfyUI + RTX 4070 8 GB).
>
> **Relação com `roadmap_producao_visual.md`:** aquele roadmap assume *"GPT gera, ComfyUI só pós-processa"*.
> **Esta trilha desafia essa tese**: explora o ComfyUI como **gerador** — controle total de
> enquadramento/estilo/consistência, **sem censura** e **sem depender do GPT Image**. Se vingar, a
> Etapa 1 do `roadmap_producao_visual.md` passa a ser opcional (GPT vira só um atalho, não a fonte).

## Por que esta trilha existe (a dor que originou)

Sessão de 2026-06-24: as thumbs do GPT Image **cortavam os seios** das Kaelis (ruim p/ jiggle/idle) e o
GPT **não controla** enquadramento/estilo nem deixa gerar fan-service. O remendo (outpaint local —
ver `wanbust`/`outpaint` no `comfyui_batch.py`) salva o que já existe, mas a **raiz** é não gerar local.
Descobrimos que o rig **já tem quase tudo** pra gerar nativo — falta orquestrar.

## Inventário do rig (auditado 2026-06-24 — o que dá pra usar HOJE)

| Categoria | Instalado | Serve p/ |
|---|---|---|
| **Checkpoints SDXL** | `NetaYumev35` (glossy/render 2.5D), `waiIllustriousSDXL_v160` (anime chapado), `animagineXLV31`, `hassakuXLIllustrious`, `ponyDiffusionV6XL`, `pixelArtDiffusionXL` | base de geração (GEN-01) |
| **ControlNet** | `controlnet-openpose-sdxl-xinsir` (+ 1 `diffusion_pytorch_model` a identificar) | **poses** (GEN-03), estrutura (GEN-04) |
| **IPAdapter** | `ip-adapter-plus_sdxl_vit-h.bin`, `ip-adapter-plus-face_sdxl_vit-h.safetensors` | **consistência** rosto/estilo (GEN-02) |
| **CLIP-Vision** | `CLIP-ViT-H-14-laion2B`, `sigclip_vision_patch14_384` | requisito do IPAdapter |
| **Detailer / segment** | Ultralytics `bbox`+`segm`, `Sams` (SAM), AfterDetailer, Codeformer, GFPGAN | inpaint de **roupa** (GEN-04), mãos/olhos (GEN-11) |
| **Upscalers** | `4x-AnimeSharp`, `4xNomos2_hq_dat2`, `RealESRGAN_x4plus(_anime_6B)`, BSRGAN, SwinIR, LDSR, ScuNET | refino premium (GEN-11) |
| **Vídeo** | `SVD` (Stable Video Diffusion), Wan2.1 I2V (em `DiffusionModels`, já validado em CUT-03 ALT) | img→vídeo (GEN-10) |
| **LoRAs** | `gameb` (jiggle), `character_sheet`, `ROSprites`, `Dungeon_Squad_Illustrious`, `CharacterDesign-IZT` | sheets, sprites, jiggle |
| **Tagging** | DeepDanbooru | auto-tag de dataset p/ treinar LoRA (GEN-08) |

> **Falta instalar:** tooling de **treino de LoRA** (kohya_ss ou ComfyUI trainer) p/ identidade travada
> (GEN-08). ControlNet **depth/canny/scribble** SDXL (GEN-07, opcional). Resto é orquestração.

## Invariantes
- **Só PC, grátis/local, 8 GB-aware** (512–1024², block-swap/tiling onde precisar; SDXL ~1 MP nativo).
- **Não toca o jogo** — só `tools/`, `output/` e PNGs de asset. Integração no front é passo separado.
- **Fonte única de verdade por Kaeli**: estender `tools/kaeli_motion_profiles.json` (ou um
  `kaeli_style_profiles.json`) com prompt/seed/LoRA — nada de prompt solto espalhado.
- **Toda geração grava `.recipe.json`** (seed + params) p/ reproduzir (padrão já usado em `wanbust`/`outpaint`).
- **IDs/slugs estáveis** (`waifu:*`); arte por Kaeli na pasta da Kaeli.

---

# Ondas

A progressão pedida (cada onda reusa a anterior). **Onda 0 desbloqueia tudo.**

## GEN-00 — Desbloquear IPAdapter + auditar/normalizar o rig  ⬅ pré-requisito
- **Modelo:** Codex · **Effort:** low · **Depende de:** —
- **Problema:** `IPAdapterUnifiedLoader` crasha com *"IPAdapter model not found"* mesmo com os modelos
  presentes — porque (a) a pasta é `IpAdapter`/`ClipVision` (StabilityMatrix) e o node espera
  `ipadapter`/`clip_vision`, e (b) o preset `PLUS (high strength)` procura `..._sdxl_vit-h.safetensors`
  e aqui o "plus" (não-face) só existe como `.bin`.
- **Tarefas:** mapear as pastas no `extra_model_paths.yaml` do ComfyUI (ou junction `ipadapter`→`IpAdapter`,
  `clip_vision`→`ClipVision`); converter/baixar o `ip-adapter-plus_sdxl_vit-h.safetensors`; testar
  `IPAdapterUnifiedLoader` com os presets `PLUS (high strength)` e `PLUS FACE`. Documentar em `tools/README.md`.
- **Aceite:** um workflow com IPAdapter roda sem erro nos dois presets.
- **Verificação:** re-rodar o `outpaint --style-ref` da Eloa sem crashar.

## GEN-01 — Receita de Kaeli premium NATIVA (txt2img)
- **Modelo:** Opus · **Effort:** high · **Depende de:** GEN-00
- **Objetivo:** fórmula reproduzível de geração do **zero** no NetaYume (e quando usar WAI/Animagine):
  quality tags, *style bible* (o look "premium" do projeto), negative bible, sampler/cfg/steps/hires,
  e **enquadramento correto por tipo** (thumb 1:1 sem cortar seio, idle 2:3, etc. — resolve a dor raiz).
- **Tarefas:** subcomando `gen` (txt2img) no `comfyui_batch.py`; `kaeli_style_profiles.json` (prompt
  base + por-Kaeli); hires-fix tiled (reusa o bloco do `_wf_skin_variant`); grava `.recipe.json`.
- **Aceite:** gerar 1 Kaeli nova comparável (ou melhor) à arte GPT, no aspecto certo, em 1 comando.
- **Verificação:** lado a lado vs. a thumb GPT atual; o usuário aprova o look.

## GEN-02 — Consistência a partir de 1 imagem (IPAdapter)
- **Modelo:** Opus · **Effort:** medium · **Depende de:** GEN-00, GEN-01
- **Objetivo:** dada 1 imagem de referência da Kaeli, gerar **nova arte com o mesmo rosto/estilo**
  (IPAdapter face + style, seed/params do perfil). É a consistência "mesmo vibe" (sub-LoRA).
- **Tarefas:** flag `--ref <img>` no `gen` (liga IPAdapter PLUS FACE + PLUS); calibrar peso (0.6–0.9).
- **Aceite:** 3 gerações da mesma Kaeli reconhecíveis como a mesma personagem.
- **Verificação:** o usuário identifica que é a mesma Kaeli nas 3.

## GEN-03 — Poses diferentes (ControlNet OpenPose)
- **Modelo:** Opus · **Effort:** medium · **Depende de:** GEN-02
- **Objetivo:** **mesma Kaeli, pose nova** — ControlNet OpenPose (esqueleto de uma pose de referência)
  + IPAdapter (identidade). Base p/ idle/banner/skill variados.
- **Tarefas:** `gen --pose <openpose_img|ref_img>` (DWPose/OpenPose preproc → openpose-sdxl-xinsir +
  IPAdapter); biblioteca de poses em `tools/poses/`.
- **Aceite:** a Kaeli em ≥3 poses distintas mantendo identidade.
- **Verificação:** as poses batem com os esqueletos; rosto consistente.

## GEN-04 — Roupas diferentes (inpaint regional, mantendo rosto)
- **Modelo:** Opus · **Effort:** high · **Depende de:** GEN-03 · **Reusa:** IMG-07 (`skinvar`)
- **Objetivo:** trocar a **roupa** sem mexer no rosto. Evolui o `skinvar` (hoje img2img global) p/
  **inpaint mascarado**: SAM/segm detecta corpo→roupa, inpaint só ali; rosto fica intacto (como o
  `outpaint` recola o rosto hoje).
- **Tarefas:** `_wf_outfit_swap` (SAM/Ultralytics segm → máscara de roupa → inpaint NetaYume); prompts
  de roupa no `kaeli_style_profiles.json`; opção de manter pose (ControlNet) e identidade (IPAdapter).
- **Aceite:** mesma Kaeli/rosto/pose, roupa nova coerente, **sem** o look chapado destoar (lição da sessão 06-24).
- **Verificação:** 2 skins por Kaeli; rosto idêntico, roupa nítida.

## GEN-05 — Trocar cenário + proporção, mantendo personagem
- **Modelo:** Opus · **Effort:** high · **Depende de:** GEN-04 · **Reusa:** `outpaint` (sessão 06-24)
- **Objetivo:** pegar a Kaeli pronta e (a) **trocar o fundo** (segmenta personagem, inpaint do cenário)
  e (b) **mudar a proporção** (outpaint p/ 1:1 ↔ 9:16 ↔ 16:9 ↔ 2:1) sem distorcer/cortar.
- **Tarefas:** `_wf_bg_swap` (segm da personagem → inpaint do fundo por prompt); generalizar o `outpaint`
  p/ qualquer lado/aspecto (já faz bottom; abrir left/right/top + presets de aspecto).
- **Aceite:** a mesma Kaeli em 3 proporções e 2 cenários, identidade preservada.
- **Verificação:** sem costura visível; aspecto exato.

## GEN-06 — Banner (2:1) e wallpaper (16:9 / 9:16) premium
- **Modelo:** Opus · **Effort:** medium · **Depende de:** GEN-05
- **Objetivo:** entregáveis de banner/wallpaper: Kaeli + cenário compostos na proporção certa
  (regional prompt p/ "personagem à direita, cenário épico à esquerda"), refino premium.
- **Tarefas:** presets de composição por tipo (reusa matriz de proporções do `roadmap_producao_visual`);
  Regional Prompter / área de atenção; hires-fix.
- **Aceite:** 1 banner + 1 wallpaper por Kaeli, prontos p/ o front.
- **Verificação:** proporção/qualidade batem com os slots de UI existentes.

## GEN-07 — Edição livre: dada imagem + prompt, alterar OU recriar
- **Modelo:** Opus · **Effort:** medium · **Depende de:** GEN-01
- **Objetivo:** ferramenta geral img2img/inpaint: "muda a cor do vestido p/ vermelho", "adiciona uma
  coroa", "redesenha nesse estilo". Máscara opcional (inpaint) ou global (img2img/restyle).
- **Tarefas:** subcomando `edit -i <img> -p <prompt> [--mask <m>] [--denoise]`; opção de máscara por
  texto (SAM "clica" via prompt) ou desenhada; restyle (denoise alto) vs. tweak (denoise baixo).
- **Aceite:** 3 edições distintas (cor, adição de item, restyle) num comando cada.
- **Verificação:** a edição respeita a máscara; o resto não muda.

## GEN-08 — Identidade TRAVADA via LoRA de personagem (teto de consistência)
- **Modelo:** Opus · **Effort:** high · **Depende de:** GEN-02/03 (geram o dataset) · **Relação:** [[img-08-faceid-then-lora]]
- **Objetivo:** treinar **1 LoRA por Kaeli** (~15–30 imagens consistentes geradas nas ondas anteriores,
  auto-tag com DeepDanbooru) → identidade idêntica em qualquer pose/roupa/cenário. É o teto que o
  IPAdapter não alcança.
- **Tarefas:** instalar kohya_ss/Comfy trainer; pipeline de dataset (GEN-02/03 → curadoria → tag);
  receita de treino SDXL 8 GB-aware; registrar a LoRA no `kaeli_style_profiles.json`.
- **Aceite:** geração com a LoRA da Kaeli = rosto idêntico, sem IPAdapter.
- **Verificação:** blind test — o usuário não distingue da arte canônica.

## GEN-09 — Expression / emotion sheet
- **Modelo:** Codex · **Effort:** medium · **Depende de:** GEN-08 (ou GEN-02)
- **Objetivo:** variações de **expressão** (neutra/feliz/brava/triste/provocante) p/ diálogo/UI/reels.
- **Tarefas:** `gen --expr <lista>` (mesma seed/identidade, muda só a expressão via prompt/inpaint de rosto).
- **Aceite:** sheet de 5 expressões por Kaeli, identidade estável.

## GEN-10 — Vídeo img→/prompt→ (continuar a exploração)
- **Modelo:** Opus · **Effort:** high · **Depende de:** GEN-01 · **Reusa:** CUT-03 ALT (Wan I2V, validado)
- **Objetivo:** ampliar o vídeo: (a) Wan I2V já faz busto vivo idle; (b) testar **SVD** (instalado) como
  alternativa; (c) **wallpaper/banner animado**; (d) prompt→vídeo curto; (e) futuro: FX de gameplay.
- **Tarefas:** consolidar `wanbust`/`wanupscale`; spike SVD vs Wan (qualidade/VRAM/tempo); aplicar Wan
  sobre os entregáveis de GEN-05/06 (banner/wallpaper animado).
- **Aceite:** 1 wallpaper animado + comparativo SVD×Wan documentado.
- **Verificação:** loop suave, 8 GB-safe, sem "infarto" (lição do KNOWLEDGE_wan_idle_bust).

## GEN-11 — Refino premium (hires-fix + detailer de mãos/olhos)
- **Modelo:** Codex · **Effort:** medium · **Depende de:** GEN-01 · **Reusa:** IMG-06 (facerestore)
- **Objetivo:** passe final de qualidade: hires-fix tiled (detalhe real) + FaceDetailer (rosto) +
  HandDetailer (mãos — o clássico ponto fraco) + upscale escolhido por conteúdo (Nomos2 vs AnimeSharp,
  lição da sessão 06-24 sobre fishnet/quadriculado).
- **Aceite:** mãos/olhos corrigidos automaticamente no passe final.

## GEN-12 — Sprites / pixel autoral (ponte p/ Etapa 3)
- **Modelo:** Opus · **Effort:** high · **Depende de:** GEN-08 · **Relação:** `SPR-*` do `roadmap_producao_visual`
- **Objetivo:** explorar `pixelArtDiffusionXL` + LoRA `ROSprites`/`character_sheet` p/ gerar sprites
  autorais da Kaeli (jogáveis no grid) — alimenta a Etapa 3 do outro roadmap.
- **Aceite:** 1 sprite-sheet de teste de 1 Kaeli no estilo do renderer.

---

# Backlog / ideias extras (não priorizadas)

- **Style bible por Kaeli** — `kaeli_style_profiles.json` como fonte única (prompt+seed+LoRA+negative),
  igual já fazemos com `kaeli_motion_profiles.json` p/ vídeo; `emit-ui` gera workflow por Kaeli.
- **Cena multi-Kaeli** (banner de evento com várias) via Regional Prompter / GLIGEN (instalado).
- **Sketch→imagem** (ControlNet scribble/canny): você rabisca a pose/composição, o modelo pinta.
- **Turnaround / reference sheet** (LoRA `character_sheet`) p/ bootstrap do dataset de LoRA (GEN-08).
- **Relighting / day-night** da mesma cena (banner variando por horário/evento).
- **Itens/mobs/logo nativos** — generalizar a geração além de Kaeli (fecha a matriz do `roadmap_producao_visual`).
- **NSFW/fan-service controlado** localmente (o que o GPT censura — IMG-08 já apontou isso).
- **Integração na UI :7879 e no MCP** — botões/ferramentas por GEN (reusa IMG-05).
- **Galeria/seed-explorer** — gerar grid de seeds e escolher (acelera a "loteria de seed").
- **Auto-curadoria** — script que ranqueia gerações (CLIP score / face-match) p/ montar dataset de LoRA.

---

# Ordem sugerida

`GEN-00` (desbloqueio) → `GEN-01` (gerar premium) → `GEN-02` (consistência) → `GEN-03` (poses) →
`GEN-04` (roupas) → `GEN-05` (cenário/aspecto) → `GEN-06` (banner/wallpaper). Em paralelo a partir do
GEN-02: `GEN-10` (vídeo) e `GEN-11` (refino). `GEN-08` (LoRA) quando GEN-02/03 tiverem gerado dataset
suficiente — é o multiplicador de qualidade de tudo que vem depois. `GEN-07` (edição) pode entrar cedo,
logo após GEN-01.
