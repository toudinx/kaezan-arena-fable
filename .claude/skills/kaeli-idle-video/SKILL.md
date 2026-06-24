---
name: kaeli-idle-video
description: >-
  RENDERIZA o vídeo idle "busto vivo" de uma Kaeli do Kaezan Arena Fable a partir da imagem THUMB,
  rodando o pipeline Wan2.1 I2V local (ComfyUI) via `tools/comfyui_batch.py wanbust`: respiração de
  ombros/peito + cabelo balançando + jiggle (LoRA) + piscada orgânica, em loop pingpong. Use SEMPRE
  que o usuário pedir para GERAR/RENDERIZAR/FAZER o idle animado, o "busto vivo", o webm de idle, ou
  "anima a thumb da [Kaeli]", "faz o vídeo de idle igual o da Velvet", "gera o bust.webm da [nome]".
  Roda SÓ no PC (precisa ComfyUI + GPU + os pesos Wan baixados). NÃO é a kaeli-motion-prompts (essa só
  escreve o brief/prompt, não renderiza) nem a kaeli-asset-prompts (imagens estáticas). Receita
  validada e lições em docs/KNOWLEDGE_wan_idle_bust.md.
---

# Kaeli Idle Video (busto vivo, Wan I2V)

Renderiza o idle animado de uma Kaeli a partir da **thumb**, com a receita validada na sessão de
2026-06-23 (resultado "v8"). Orquestra o `tools/comfyui_batch.py wanbust`; o conhecimento completo
(armadilhas, alavancas, por quês) está em [`docs/KNOWLEDGE_wan_idle_bust.md`](../../../docs/KNOWLEDGE_wan_idle_bust.md).

> **Só PC.** Precisa ComfyUI rodando (StabilityMatrix, RTX 4070 8 GB), o node
> `ComfyUI-WanVideoWrapper`, os pesos Wan2.1 I2V e a LoRA de jiggle `gameb.safetensors` em
> `models/loras/`. Sem isso, não roda — não tente no celular.

## Receita canônica (faça isto primeiro)

```bash
python tools/comfyui_batch.py wanbust \
  -i frontend/public/assets/kaelis/<slug>/thumb.png \
  --lora gameb.safetensors --lora-strength 0.4 --blocks-swap 40
```

- Saída: `output/cutscenes/<slug>/bust.webm` (512², 16 fps, ~6 s pingpong, RGB) + `bust-raw.mp4` +
  `bust-raw.recipe.json` (seed registrado p/ reproduzir).
- Defaults do `tools/workflows/idle_bust_wan_i2v.json`: 49 frames, 25 steps, cfg 6, shift 5, unipc,
  `noise_aug 0.02`, `latent 1.0`, pingpong, prompt de idle+jiggle. **Não precisa mexer** p/ o padrão.
- 1ª run ~25 min (carrega o fp8 16 GB com block-swap). Avise o usuário.

## Passos

1. **Pré-checagem.** Confirme que a thumb existe e que o ComfyUI responde
   (`curl -s http://127.0.0.1:8188/system_stats`). Se offline, peça pro usuário dar **Launch** no
   StabilityMatrix. Confirme `gameb.safetensors` em `models/loras/` (ou peça o nome da LoRA de motion).
2. **Rode a receita canônica** em background (é longo). Quando terminar, **não** confie só em frames
   estáticos pra julgar — o movimento (piscada/jiggle) é temporal; peça pro usuário **assistir** o
   `bust.webm`. (Você pode extrair frames p/ checar brilho/loop, mas detecção automática de piscada
   é não-confiável — ver KNOWLEDGE §5.)
3. **Ajuste fino** conforme o feedback, usando as flags (uma run por ajuste; `--fast` p/ preview):

   | Feedback | Ajuste |
   |---|---|
   | corpo estático demais | `--latent-strength 0.9` (menor = mais movimento) ou `--noise-aug 0.03` |
   | jiggle fraco | `--lora-strength 0.5` · jiggle forte demais | `--lora-strength 0.3` |
   | movimento rápido/frenético | mantenha **pingpong** (não use `--native-loop`) ou suba `--frames 65` |
   | brilho oscila (escurece) | é o `--fast` — rode o **final sem `--fast`** (25 steps) |
   | sem jiggle em Kaeli recatada | `--lora-strength 0` (pula a LoRA) |

4. **Preview rápido vs final.** Pra dial de movimento use `--fast` (12 steps, ~13 min, só julga
   movimento — brilho/detalhe ruins). Pro entregável final rode **sem `--fast`**. Mantenha
   `--blocks-swap 40` e `--lora` fixos entre runs p/ o ComfyUI cachear o modelo.

5. **Upscale (entregável final).** A geração é 512² ("macio"). Suba pra 1024² SEM regerar:
   ```bash
   python tools/comfyui_batch.py wanupscale -i output/cutscenes/<slug>/bust-raw.mp4 --slug <slug>
   ```
   Upscale de vídeo num job só (ESRGAN/DAT no batch, tiled em 8 GB) → `bust-up.webm` (1024²). Default
   `4xNomos2_hq_dat2` (ficou limpo na Velvet, sem cara de foto); troque com `--model <anime>` se a
   pele/cabelo "fotografar". Sempre upscale do **`bust-raw.mp4`** (menos compressão que o `.webm`).
   ~13 min. Peça pro usuário conferir **flicker temporal** (upscale frame-a-frame pode tremer).
   **Na UI:** o mesmo upscale está em `tools/workflows/upscale_video_2x.json` (cole o caminho do
   `.mp4`, ajuste a escala no nó 👉, Queue) — pro usuário fazer no ComfyUI sem CLI.

## Convenção de pasta (`output/cutscenes/<slug>/`)

```
bust-up.webm          ← 1024² FINAL (entregável)        bust.webm     ← 512² geração-fonte
bust-up-raw.mp4                                          bust-raw.mp4
bust-raw.recipe.json  ← seed/params (reproduzir)        _experiments/ ← versões de iteração (arquivar aqui)
```
Se for iterar guardando versões pra comparar, jogue os backups em `_experiments/` (não polua a raiz).

## Afinar na UI do ComfyUI (autônomo — sem me acionar p/ cada param)

Pra dialar sem runs cegas, carregue **`tools/workflows/idle_bust_wan_full.json`** no ComfyUI
(arrasta no canvas / Workflow → Open). Vem com a LoRA já ligada e os params do v8. Os nós com
**👉** no título são os que você tweaka ao vivo (vê o preview do KSampler, aborta run ruim):

- **👉 JIGGLE** (`WanVideoLoraSelect.strength`): 0.0 desliga · 0.3 sutil · 0.4 padrão · 0.55 forte
- **👉 MOVIMENTO** (`WanVideoImageToVideoEncode.start_latent_strength`): 1.0 calmo · 0.9 ativo (menor=mais);
  `num_frames` = duração; `noise_aug_strength` sobe movimento
- **👉 PROMPT** (`WanVideoTextEncode`): mantenha `shaking breasts` p/ a LoRA ativar
- **👉 seed/steps** (`WanVideoSampler`): seed varia a piscada; **steps 12 = preview rápido**, 25 = final
- **👉 THUMB** (`LoadImage`): suba a imagem da Kaeli

Fluxo: **dial na UI → quando achar o ponto, me passe os números** → eu reproduzo na CLI pro
**final + upscale + organização** (a UI não loga seed/params nem transcoda/nomeia). Mudanças
**estruturais** (adicionar clip-vision, trocar sampler, novo nó) = me aciona; **tweak de param** = você faz.

## Perfis por Kaeli (data-driven — sem editar prompt à mão)

`tools/kaeli_motion_profiles.json` guarda o tuning **por Kaeli**. O `wanbust` acha o perfil pelo
**slug** (de `--slug` ou da pasta da thumb) e aplica sozinho: `positive_extra` (anexa ao prompt),
`negative_extra` (anexa ao negativo) e overrides (`lora_strength`, `latent_strength`, `noise_aug`).
**Flags explícitas vencem o perfil; perfil vence o default.** Então pra uma Kaeli já perfilada:

```bash
# a Eloa puxa wings + anti-sono + latent 0.94 + lora 0.4 do perfil, sem -p manual:
python tools/comfyui_batch.py wanbust -i frontend/public/assets/kaelis/eloa/thumb.png \
  --lora gameb.safetensors --blocks-swap 40
```

**Adicionar/ajustar uma Kaeli = só editar o JSON** (nada de código). Schema por slug:
```json
"eloa": {
  "positive_extra": "gentle blink, blinks once, angel wings gently moving, feathers swaying softly",
  "negative_extra": "sleepy, drowsy",
  "lora_strength": 0.4,
  "latent_strength": 0.94
}
```
Fluxo p/ Kaeli nova: **dial na UI → achou o ponto → grava o perfil no JSON** (lê os valores do PNG/mp4
que a UI salva: arrasta o PNG no ComfyUI ou a metadata `comment` do mp4 traz tudo). Daí em diante,
`wanbust --slug <kaeli>` reproduz sem prompt manual. `--no-profile` ignora o perfil.

**Workflow de UI por Kaeli (o perfil dentro do ComfyUI).** Pra ter o prompt/params certos já na UI
sem digitar nada, gere um workflow por Kaeli a partir dos perfis:
```bash
python tools/comfyui_batch.py emit-ui --slug all   # ou --slug eloa
```
Isso escreve `tools/workflows/idle_bust_<slug>.json` (prompt+params da Kaeli embutidos). Carrega o da
Kaeli no ComfyUI, sobe a thumb, Queue. "Trocar de Kaeli" = carregar outro arquivo. Adicionar Kaeli =
editar `kaeli_motion_profiles.json` + `emit-ui` de novo. **Fonte única da verdade = o JSON de perfis.**

**Estrutura base+específico (prompt em dois boxes).** No `idle_bust_wan_full.json` (template) e nos
gerados, o prompt vem de nós **`StringConcatenate`**: `string_a` = BASE (fixo, comum a todas) ·
`string_b` = ESPECÍFICO da Kaeli (wings, anti-sono, etc.) → alimenta o `WanVideoTextEncode`. Pra um
workflow novo à mão: **copie o template e mude só o `string_b`** (PROMPT+ e NEG+). O `emit-ui` faz
isso automático a partir do `positive_extra`/`negative_extra` do perfil.

## Presets (atalhos de CLI)

| Preset | Comando (troque `<slug>`) |
|---|---|
| **v8 / padrão** | `wanbust -i .../<slug>/thumb.png --lora gameb.safetensors --lora-strength 0.4 --blocks-swap 40` |
| **calmo** (jiggle sutil) | `… --lora-strength 0.3 --latent-strength 1.0 --noise-aug 0.02` |
| **jiggle forte** (fan service) | `… --lora-strength 0.55 --latent-strength 0.92 --noise-aug 0.03` |
| **corpo ativo** (mais respiração) | `… --lora-strength 0.4 --latent-strength 0.88 --noise-aug 0.04` |
| **sem jiggle** (recatada/armadura) | `… --lora-strength 0` |
| **preview** (qualquer um, +rápido) | acrescente `--fast` (12 steps; só p/ julgar movimento) |

## Limites conhecidos (não prometa o que não dá)

- **Timing de piscada não é controlável** — está assado nos pixels; é loteria de seed. Não queime
  runs caçando; aceite a piscada orgânica de um seed bom. (KNOWLEDGE §5)
- Mexer no prompt p/ controlar piscada tende a causar **boca animada** ("falando/comendo").
- Saída é **RGB sem alpha** → p/ compor sobre `bg-portrait`, keyar por frame.
- Nitidez "macia" do 512 é separada: resolve depois com **upscale** (Real-ESRGAN), sem regerar.

## Integração (opcional, depois de aprovado)

Copie o `bust.webm` p/ `frontend/public/assets/kaelis/<slug>/` e registre conforme a convenção de
cutscene/summon WW-style (ver `docs/roadmap/.../roadmap_producao_visual.md` CUT-03 e a memória
`recruit-reveal-decision` sobre quando integrar webm de summon).
