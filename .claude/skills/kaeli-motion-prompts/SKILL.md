---
name: kaeli-motion-prompts
description: >-
  Gera o brief de MOVIMENTO de uma Kaeli do Kaezan Arena Fable: para cada cutscene/animação, um
  prompt de image-to-video pronto pro ComfyUI (idle breathing, summon, reel) E/OU um roteiro (spec)
  de cutscene Remotion com timeline, assets e beats. Usa a mesma técnica de "bloco de identidade" das
  skills kaeli-asset-prompts/kaeli-social-prompts pra a personagem sair consistente. Use SEMPRE que o
  usuário pedir cutscene, animação, idle animado/breathing, vídeo, clipe, reel/teaser em vídeo,
  cinematic de summon/invocação ou de skill/habilidade de uma Kaeli (Velvet, Eloa, Seren, Mirai,
  etc.), ou disser "anima a [nome]", "quero uma cutscene de summon", "faz o idle respirar", "spec do
  Remotion pra X". É usável no celular (gera o brief; o RENDER é no PC). NÃO gera as imagens estáticas
  (isso é kaeli-asset-prompts) nem posts de social (kaeli-social-prompts) nem renderiza vídeo.
---

# Kaeli Motion Prompts

Produz o **brief** de uma cutscene/animação a partir da identidade de uma Kaeli: ou um **prompt
image-to-video** pro ComfyUI, ou uma **spec de cutscene Remotion**, ou ambos. Quem **renderiza** é o
PC (`docs/roadmap_producao_visual.md`, Etapa 2); esta skill só escreve o markdown — roda **no celular**.

## Por que esta skill existe

A produção de arte do projeto separa **celular** (texto/prompt/brief) de **PC** (GPU/render) — ver
`docs/WORKFLOW_imagem_e_cutscenes.md`. As imagens estáticas já têm `kaeli-asset-prompts`; o
**movimento** não tinha brief. Sem um brief consistente, cada cutscene reinventa a personagem e o
clima. Esta skill resolve igual às irmãs: **congela o "bloco de identidade"** da Kaeli e injeta em
todo prompt de vídeo / spec — a personagem nunca "muda" entre frames ou cenas.

> **Modo web (Claude Code Web).** Sob `docs_web/CLAUDE_WEB.md`: **não leia código** (`frontend/`,
> `tools/`). Pegue identidade e metadados em `docs_web/roster_digest.md`; escreva a saída em
> `docs_web/motion/<slug>-<tipo>.md`. Não gere imagem/vídeo, não builde.

## Fluxo

### Passo 1 — Bloco de identidade (consistência)
Pegue a identidade da Kaeli no `roster_digest.md`. Se ela tiver bloco congelado, use-o **sem alterar**.
Senão, monte um curto a partir da identidade visual do digest (cabelo, olhos, roupa, acessórios,
acento, mood) — mesma técnica de `kaeli-asset-prompts`. Esse bloco entra no topo de **todo** prompt de
vídeo e na spec do Remotion.

### Passo 2 — Escolher o tipo de movimento
Confirme com o usuário só o que faltar: **Kaeli**, **tipo** e **motor**. Tipos:

| Tipo | O que é | Motor recomendado | Roadmap |
|---|---|---|---|
| **idle** | breathing/physics sutil no idle | in-engine (CSS) ou ComfyUI LivePortrait (premium) | CUT-02 / CUT-03 |
| **summon** | cutscene de invocação/reveal | **Remotion** (`tools/cinematics/`, já provado) | CUT-04 |
| **skill** | cinematic ao soltar habilidade | FX in-engine (+ clipe opcional) | CUT-05 |
| **reel** | clipe 9:16/1:1 pra Instagram | Remotion (parallax/ken-burns) ou ComfyUI curto | CUT-06 |

> **Restrição de GPU (declare no brief):** alvo é **RTX 4070 laptop, 8 GB VRAM**. Prompts de ComfyUI
> de vídeo pedem **clipe curto / baixa-res / loop costurável**. Remotion roda em qualquer GPU.

### Passo 3 — Emitir o brief
Entregue blocos copiáveis. Para cada cutscene, conforme o motor:
- **ComfyUI** → um **prompt image-to-video** (bloco de identidade + movimento + restrição 8 GB +
  asset-base de entrada, ex. `idle-1.png`).
- **Remotion** → uma **spec** (timeline em beats, lista de assets, movimento por beat, cores dos
  tokens `Cathedral Ink + Aurum`) que o PC executa em `tools/cinematics/` (skill `remotion-best-practices`).

### Passo 4 — Fechar
Lembre: o **render é no PC**; aponte o destino (`frontend/public/assets/kaelis/<slug>/idle-loop.webm`
ou `frontend/public/assets/cinematics/<comp>.webm`) e que a integração no jogo é passo de desktop.

## Templates

Em todos: comece com o **bloco de identidade**, depois o corpo. Substitua `[...]`.

### idle (image-to-video, ComfyUI — breathing premium)

```
[BLOCO DE IDENTIDADE]

Image-to-video, subtle idle "breathing" loop from this single full-body character image.
Motion: gentle chest/shoulder breathing, faint hair/cloth sway, slow blink. NO walking, NO camera
move, NO scene change — she stays in place. Keep the exact design and the transparent/clean background.
Seamless loop (first and last frame match). Short clip, low resolution, loop-friendly.
[Run on ComfyUI — RTX 4070 laptop, 8 GB VRAM: keep it short/low-res, e.g. LivePortrait/AnimateDiff.]
Input asset: idle-1.png
```

### summon (spec de cutscene Remotion)

```
## Spec — Summon cutscene · <Nome> (Remotion)
Composição: <Nome>Summon · base: GachaSummon (tools/cinematics) · 12–15s @30fps · 1920×1080
Assets: <slug>/thumb.png + <slug>/bg-landscape.png   (thumb tem fundo próprio; NÃO usar idle recortado)
Cor de acento: [acento da Kaeli] · iris #7b6bf2 = antecipação · aurum #e8a93c = recompensa 5★
Beats:
  1 (0–3s)   cenário escuro: [cenário ancorado do digest], leve névoa.
  2 (3–7s)   círculo arcano carregando na cor de antecipação (iris) + energia subindo.
  3 (7–9s)   burst na cor de acento → card de invocação sobe (thumb em moldura aurum 5★ + glow).
  4 (9–12s)  partículas/embers subindo; light sweep no card.
  5 (12–15s) placa preenche: ★★★★★ · <NOME>.
Mood: [mood da Kaeli]. Render no PC: npm run deploy → frontend/public/assets/cinematics/.
```

### skill (FX in-engine — brief de direção)

```
## Spec — Skill FX · <Nome> · shape <single|beam|nova|area|cone|buff>
Acento: [cor do elemento]. Disparo: no evento visual do tick (a cutscene NÃO altera a simulação).
Beats curtos (<1s): wind-up → release ([formato do shape]) → impacto/decay.
[descrição do FX por shape: ex. nova = anel expandindo na cor do elemento + flash central].
Render: canvas in-engine (renderer.ts). Clipe pré-renderizado só p/ ultimate (opcional, Remotion).
```

### reel (Remotion ou ComfyUI curto)

```
[BLOCO DE IDENTIDADE]

Marketing reel, [9:16 vertical / 1:1 square], 5–8s. Source: wallpaper.png (ou banner.png).
Motion: slow parallax/ken-burns push-in + drifting [acento] particles; subtle rim-light pulse.
Leave headroom for caption overlay. On-brand dark-fantasy. Seamless loop if possible.
[Remotion em qualquer GPU; se ComfyUI, manter curto p/ 8 GB.] Render no PC → social.
```

## Exemplo (Velvet — summon, Remotion)

Bloco de identidade (do digest, sem alterar): cabelo roxo escuro longo, olhos vermelhos brilhantes,
vestido gothic lolita preto-e-roxo, tiara de orelha de gato. Cenário: catedral gótica noturna, cristal
roxo. Acento: roxo. → entra na spec acima como `VelvetSummon`, reusando `velvet/thumb.png` +
`velvet/bg-landscape.png` (já existem em `tools/cinematics/public/velvet/`).

## Notas
- **Uma Kaeli por brief.** Consistência > variedade: todos os clipes dela compartilham o bloco.
- **Só nossas Kaelis** — nunca IP de terceiros.
- Isto gera **brief (prompt/spec)**, não vídeo. O render é no PC (`docs/roadmap_producao_visual.md`, Etapa 2).
- Para os 8 assets estáticos use `kaeli-asset-prompts`; para posts, `kaeli-social-prompts`.
