# Knowledge — Busto vivo idle (Wan I2V, CUT-03 ALT)

Conhecimento prático destilado da sessão de R&D de 2026-06-23 gerando o idle "busto vivo" da
Velvet (respiração + cabelo + jiggle de peito a partir da thumb). É o complemento "lições
aprendidas" da seção **CUT-03 ALT** do [`tools/README.md`](../tools/README.md). Quem renderiza é
o PC (ComfyUI + GPU); o brief de movimento sai pela skill `kaeli-motion-prompts`, e a **execução**
pela skill `kaeli-idle-video`.

> **TL;DR — a receita validada (v8).** Único comando que produziu o resultado aprovado:
> ```bash
> python tools/comfyui_batch.py wanbust \
>   -i frontend/public/assets/kaelis/<slug>/thumb.png \
>   --lora gameb.safetensors --lora-strength 0.4 --blocks-swap 40
> ```
> Tudo o mais (512², 49 frames, 16 fps, 25 steps, cfg 6, shift 5, unipc, noise_aug 0.02,
> latent 1.0, pingpong) é default do `idle_bust_wan_i2v.json`. v8 = **seed 242472**.

---

## 1. O rig e os pesos (ver memória `comfyui-rig-setup`)

- StabilityMatrix / ComfyUI, **RTX 4070 8 GB**. Pasta compartilhada `Data/Models/` → junction p/ `models/`.
- Nodes: **ComfyUI-WanVideoWrapper** (Kijai). Pesos: `wan2.1_i2v_480p_14B_fp8_e4m3fn` (DiffusionModels),
  `wan_2.1_vae` (VAE), `umt5-xxl-enc-bf16` (TextEncoders).
- **VAE-only:** o `clip_embeds` é OPCIONAL no `WanVideoImageToVideoEncode` (só o `vae` é obrigatório).
  Rodamos sem clip vision → dispensa baixar o `open-clip-xlm-roberta-large-vit-huge-14`.
  ⚠️ O `sigclip_vision_patch14_384` e o `CLIP-ViT-H-14-laion2B` **não** servem p/ o
  `LoadWanVideoClipTextEncoder` (ele valida `log_scale` e exige o xlm-roberta).

## 2. Armadilhas que custaram tempo (não repetir)

| Sintoma | Causa | Correção |
|---|---|---|
| `None > 0` crash no sampler | `WanVideoBlockSwap` em API-format não repassa opcionais; `init_blockswap` lê `vace_blocks_to_swap` sem default | passar `vace_blocks_to_swap: 0` (+ outros opcionais) explícito no nó — **já no JSON** |
| ComfyUI **crasha (OOM)** ao carregar a LoRA | `merge_loras: True` funde a LoRA no fp8 14B → estoura 8 GB | `merge_loras: False` (on-the-fly) — **já no `wanbust`**; mantém o modelo no offload device |
| **Brightness pump** (vídeo escurece/clareia, YAVG 21↔65) | `--fast` (12 steps) = sub-denoising | `--fast` só p/ julgar **movimento**; final em **25 steps** (brilho fica plano ~62.7) |
| Movimento "frenético / infarto" | loop **nativo** (`WanVideoLoopArgs`) injeta motion via latent-shift **e** toca em 3.25s (metade do pingpong) | usar **pingpong** (6s, mais calmo e suave p/ idle sutil) |
| Vídeo virou **imagem parada** | prompt **suprimindo** movimento ("minimal motion, low amplitude" + negativo "fast/large movement") matou blink/cabelo junto | calma vem da **amplitude** (settings), **nunca** suprimir movimento no prompt |
| "Parece que está falando / comendo" | seed re-sorteado animou a boca | `mouth opening, talking, eating` no negativo (blindagem) — mas ainda é loteria de seed |

## 3. As alavancas (flag → efeito)

| Flag / param | Efeito | Observação |
|---|---|---|
| `--lora-strength` | amplitude do **jiggle** | 0.4 sutil (bom), 0.5+ forte demais, **0.0 desliga** (pular em Kaeli recatada/armadura) |
| `--latent-strength` | **MENOR = mais movimento de corpo** | 1.0 = calmo/preso à imagem (v8); 0.85 = mais ativo. Muito baixo = drift/perda de nitidez |
| `--noise-aug` | mais alto = mais movimento geral | 0.02 calmo; subir adiciona motion (e um tiquinho de nitidez) |
| `--frames` | mais frames = movimento mais **lento/suave** | custa VRAM; é a alavanca p/ jiggle "física" em vez de tremor |
| `--native-loop` | loop forward-time (jiggle não inverte) **mas** injeta motion + sensação mais rápida | default é pingpong (preferido p/ idle calmo) |
| `--fast` | preview 12 steps (~13 min) | **só p/ movimento** — brilho instável, detalhe cru |
| `--blocks-swap` | mais alto = menos VRAM, mais lento | 40 = máx p/ o 14B; usar 40 com LoRA em 8 GB |

## 4. Jiggle / fan service (LoRA de motion)

- Precisa de **motion LoRA** + trigger word. Usamos **`gameb.safetensors`** (breast physics, Wan2.1,
  `modelspec.architecture: wan2.1/lora`), trigger **`shaking breasts`** (já no prompt default).
- **Motion** LoRA (mexe no movimento) ≠ **style** LoRA (mexe no traço). Só motion preserva a
  identidade → seguro **padronizar no roster** via `--lora-strength` (dial por Kaeli; 0 = sem jiggle).
- Prompt-only (sem LoRA) dá jiggle fraco. A LoRA é o que entrega o "vida própria".

## 5. Loop e piscada (o limite do método)

- **Pingpong** = toca 0→48→0 (6s); o último frame **é** o primeiro → loop costurado de graça.
  Espelha o movimento em torno dos **3s** (centro). Para idle sutil (amplitude baixa) isso é suave.
- **Piscada = loteria de seed, NÃO controlável.** A piscada está "assada" nos pixels gerados; não dá
  pra editar o timing como camada separada. O pingpong espelha: **uma piscada no meio da fonte
  (frame ~24) → piscadas em ~1.5s e ~4.5s** (espaçadas). Mas forçar "uma piscada no meio" só por
  prompt/seed é sorte. Tentar tunar a piscada por prompt arriscou artefato de boca. **Conclusão:
  aceitar a piscada orgânica do seed bom; não vale queimar runs caçando timing.**
- Saída é **RGB (sem alpha)** → p/ compor sobre `bg-portrait`, keyar por frame (a thumb tem fundo
  gradiente simples).

### Tuning de olho POR PERSONAGEM (aprendido na Eloa)

Kaelis com **olhar sultry/meio-cerrado de base** (ex: Eloa) derivam pra "cara de sono" ao animar —
os olhos caem ao longo do clipe. Lições do tuning dela:
- **"Sono" e "piscada" são a MESMA geometria** (olho fechado), diferindo só em duração/grau. Logo:
  - ❌ **Nunca** ponha `half-closed eyes / half-lidded / narrowed eyes` no negativo p/ tirar o sono —
    isso **mata a piscada junto** (a piscada É olho fechando).
  - ✅ Use só **palavras de humor** no negativo: `sleepy, drowsy`. Combatem o estado *sustentado*
    sonolento sem proibir o blink *transitório*.
- **`start_latent_strength` é o controle real da deriva sonolenta** (não o prompt): 1.0 = olhos
  firmes mas blink fraco; 0.92 = blink cheio mas sonolenta; **0.95 = meio-termo**.
- **Single blink:** positivo `blinks once slowly` **+ blindagem de boca no negativo**
  (`mouth opening, talking, eating`) — sem a blindagem, instrução de piscada no prompt anima a boca
  ("comendo"). Com pingpong, 1 blink na fonte vira 2 espaçados/simétricos no loop (natural).
- Resumo: **olho é a fronteira difícil** (cada alavanca troca por outra; parte é loteria de seed).
  Mire em "bom o suficiente" e use a **UI pra seed-hunt rápido** (steps 12). Anti-sono é ajuste
  **por-personagem** (Velvet não precisou; Eloa sim) — não botar no default global.

### ⚠️ Steps a mais NEM SEMPRE melhora (aprendido na Eloa)

Steps não só "afinam" — **mudam a realização**. Eloa (seed 249461): **12 steps acertou os dois
olhos**; **25 steps no mesmo seed QUEBROU o olho esquerdo** (derreteu). Subir steps pode divergir e
piorar rosto/olhos. Práticas:
- **Não regere "pra melhorar" só subindo steps** — pode estragar o que estava bom.
- **12 steps pode ser o KEEPER** (não só preview), desde que o brilho esteja estável (cheque YAVG —
  Eloa: plano a 12; Velvet: 12 deu pump → precisou 25). É **por seed/personagem**, não regra fixa.
- Achou um run bom? **Trave** (seed + steps no `.recipe.json`) e não mexa às cegas. Eloa final = **12 steps**.

## 6. Velocidade de iteração

- O custo é a **geração** (`steps × frames × resolução + load do modelo`), **não** o transcode webm
  (ffmpeg, ~segundos — o `.webm` é só o formato web do `.mp4` cru, é até menor).
- 1ª run ~25 min (carrega o fp8 16 GB com block-swap em 8 GB). Runs seguintes mais rápidas se o
  ComfyUI **cachear o modelo** → manter `--blocks-swap`/`--lora` fixos entre runs.
- Fluxo: **dial do movimento com `--fast`** (12 steps) → quando bom, **run final sem `--fast`**
  (25 steps, nítido). Cada run grava um **sidecar `.recipe.json`** com o seed (reprodutibilidade).

## 7. Upscale (512² → 1024²) — fecha o pipeline

A geração sai 512² ("macia"). O subcomando **`wanupscale`** amplia o clipe **sem regerar** (ESRGAN/DAT
frame-a-frame num job só do ComfyUI; tiled fallback p/ caber em 8 GB):

```bash
python tools/comfyui_batch.py wanupscale -i output/cutscenes/<slug>/bust-raw.mp4 --slug <slug>
```

- Default `4xNomos2_hq_dat2` (4x × 0.5 = **net 2x** → 1024²). Validado na Velvet: nítido (fios de
  cabelo, íris, renda), **sem "fotografar"** a pele apesar de ser modelo de foto. ~13 min.
- Upscale sempre do **`bust-raw.mp4`** (menos compressão que o `.webm` já transcodado).
- Se a pele/cabelo ganharem textura fotográfica → `--model <anime>` (ex: `4x-AnimeSharp`,
  `RealESRGAN_x4plus_anime_6B` — baixar em `models/upscale_models/`).
- Risco a conferir: **flicker temporal** (cada frame upscalado independente). ESRGAN é determinístico,
  então costuma ser estável; confirmar assistindo.

## 8. Resultado canônico e convenção de pasta

`output/cutscenes/<slug>/`:
```
bust-up.webm          ← 1024² FINAL (entregável)        bust.webm     ← 512² geração-fonte
bust-up-raw.mp4                                          bust-raw.mp4
bust-raw.recipe.json  ← seed/params (reproduzir)        _experiments/ ← versões de iteração
```
Velvet (v8): respiração de ombros/peito + cabelo + jiggle sutil + piscada orgânica + brilho estável,
1024² após o upscale. Pipeline completo = **`wanbust` (gerar) → `wanupscale` (1024²)**, ambos via a
skill `kaeli-idle-video`. Replicável em qualquer Kaeli (a LoRA de jiggle é dialável por `--lora-strength`).

## 9. Escala: UI p/ afinar, CLI p/ replicar

O R&D (achar a receita) é **custo único**, já pago. Pra as próximas (7 Kaelis × ~3 idles ≈ 21+
animações), o gargalo é **afinar sem runs cegas**:

- **Afinar = ComfyUI UI.** Carregue `tools/workflows/idle_bust_wan_full.json` (LoRA ligada, params v8,
  nós 👉 rotulados) → tweak ao vivo com preview, aborta run ruim no meio. Autônomo: tweak de param é
  do usuário; mudança estrutural (nó novo) aciona o assistente.
- **Replicar/finalizar = CLI.** `wanbust` (presets na skill `kaeli-idle-video`) + `wanupscale` —
  reproduzível, seed logado, transcode/nome/pasta automáticos, lote.
- **Upscale também tem versão UI:** `tools/workflows/upscale_video_2x.json` (cole o caminho do `.mp4`,
  Queue) — equivalente ao `wanupscale`, p/ fechar o fluxo todo no ComfyUI.
- **Padronizar o enquadramento** das thumbs (busto consistente) faz a receita transferir melhor → menos
  tuning por asset.
- **Perfis por Kaeli (data-driven):** `tools/kaeli_motion_profiles.json` guarda `positive_extra`,
  `negative_extra` e overrides (`lora_strength`/`latent_strength`/`noise_aug`) **por slug**. O `wanbust`
  aplica sozinho (flag > perfil > default). Nova Kaeli = editar o JSON, sem código. É a "memória" do
  tuning de cada uma (ex: Eloa = wings + anti-sono + latent 0.94). Fluxo: dial na UI → grava o perfil →
  `wanbust --slug <kaeli>` reproduz sem prompt manual. Os valores saem do PNG/mp4 que a UI salva
  (arrasta o PNG no ComfyUI, ou lê a metadata `comment` do mp4 — **a workflow inteira fica embutida lá**).

**Extensões futuras (mesmo pipeline):** wallpaper/banner **animados** — só muda o aspect; o Wan 480p é
nativamente **832×480 (paisagem)**, ótimo p/ wallpaper (`--width 832 --height 480`). Animação **em
gameplay** é outra trilha (sprites/tempo real, SPR-*), não este vídeo pré-renderizado.
