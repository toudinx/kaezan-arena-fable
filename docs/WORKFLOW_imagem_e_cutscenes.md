# WORKFLOW — Imagem & Cutscenes (PC ↔ celular, sem API paga)

> **O que é este doc.** A **metodologia** de produção de arte e movimento do Kaezan Arena Fable: o que
> fazer **no celular** (rodar prompts pro ChatGPT) vs. **no PC** (ComfyUI / Remotion / integração), e a
> **fila de hand-off** que liga as duas pontas. Os *roadmaps* dizem **o que** construir; este doc diz
> **onde e como** você trabalha no dia a dia.
>
> **Roadmap que este workflow orquestra:** `docs/roadmap_producao_visual.md` — um roadmap, 3 etapas:
> - **Etapa 1 · Imagem estática** (`IMG-*`) — GPT gera → ComfyUI pós-processa.
> - **Etapa 2 · Movimento & cutscenes** (`CUT-*`) — idle / summon / skill / reels.
> - **Etapa 3 · Sprites autorais** (`SPR-*`) — sprites jogáveis no grid (R&D).
>
> **Invariante de custo:** **nenhuma API paga.** Geração de imagem-base no **GPT Image** (que você já
> usa) ou no ComfyUI local; todo o resto é **ComfyUI / Remotion / in-engine**, grátis e local.

---

## A regra de ouro

> **Texto / prompt / decisão → celular.** **GPU / código / render → PC.**

Tudo que é *escrever um prompt, montar um roteiro, decidir identidade visual* não precisa de máquina
forte e roda em qualquer lugar — é **celular**. Tudo que *consome GPU, toca código ou renderiza um
arquivo final* (upscale, removebg, vídeo, integração no jogo) é **PC**. A fila de hand-off (abaixo)
é a ponte: o celular produz o **brief**, o PC produz o **artefato**.

Isso espelha a estrutura de trilhas que o repo já tem:

| Trilha | Superfície | Escreve em | Toca código? | Gera mídia? |
|---|---|---|---|---|
| **Web** (`docs_web/`, ver `docs_web/CLAUDE_WEB.md`) | **Celular** / Claude Code Web | `docs_web/**` (+ `docs/roadmap_*.md`) | ❌ nunca | ❌ você gera no ChatGPT |
| **Desktop** (`docs/roadmap/`) | **PC** / Claude Code desktop | `tools/`, `frontend/`, `output/` | ✅ | ✅ ComfyUI / Remotion |

---

## Superfície 1 — Celular (ideação + geração)

**Ferramentas:** Claude Code Web + skills + app do ChatGPT.
**O que dá pra fazer sem o PC:**

1. **Rodar uma skill** que monta os prompts a partir da identidade da Kaeli (técnica do "bloco de
   identidade", lendo `docs_web/roster_digest.md`):
   - `kaeli-asset-prompts` → os **8 assets** estáticos (idles, wallpaper, bg-*, banner, thumb).
   - `kaeli-social-prompts` → posts de Instagram (imagem + caption + hashtags).
   - **`kaeli-motion-prompts`** (nova) → **prompts image-to-video** (ComfyUI) + **specs de cutscene
     Remotion** — o brief que o PC vai renderizar.
2. A skill escreve markdown em `docs_web/` (`skins/`, `social/`, `motion/`). Tudo **copiável**.
3. **Você** cola o prompt no app do ChatGPT (GPT Image) e gera a **imagem-base**.
4. Guarda a imagem-base pra processar no PC (ver fila abaixo).

**O celular nunca** roda ComfyUI, nunca renderiza vídeo, nunca toca `frontend/`/`tools/`. Se a tarefa
parece exigir isso, ela **não é** de celular — vira candidata a prompt de roadmap desktop.

---

## Superfície 2 — PC (pós-processo + render + integração)

**Ferramentas:** Claude Code desktop + ComfyUI local + Remotion (`tools/cinematics/`).
**O que só o PC faz:**

- **Pós-processo de imagem (ComfyUI):** upscale, removebg, crop — via UI `tools/kaezan_tools_ui.py`
  (porta :7879) e `tools/comfyui_batch.py`. Roadmap: `roadmap_producao_visual.md` (Etapa 1).
- **Render de cutscene (Remotion):** `tools/cinematics/` — `npm run deploy` renderiza o webm e copia
  pra `frontend/public/assets/cinematics/`. Roda em **qualquer GPU** (é compositing, não IA).
- **Vídeo local IA (ComfyUI):** LTX / AnimateDiff / LivePortrait p/ clipes curtos. Sua GPU
  (**RTX 4070 laptop, 8 GB VRAM**) dá conta de clipes curtos/baixa-res — é **apoio**, não a espinha
  dorsal. Roadmap: `roadmap_producao_visual.md` (Etapa 2).
- **Integração no jogo:** wiring no Angular (`<app-kaeli-idle>`, `recruit.ts`), `manifest.json`,
  sprites no renderer. Só desktop.

---

## A fila de hand-off (intake → pós → jogo)

O elo entre as duas superfícies é uma pasta de **intake**. O celular enche; o PC esvazia.

```
[CELULAR]  skill → prompt em docs_web/…           (brief)
              │  você cola no ChatGPT (GPT Image)
              ▼
           imagem-base crua
              │  você salva em:
              ▼
   output/inbox/<tipo>/<slug>/…                    (fila de entrada)
              │
========================  PC  ========================
              ▼
   ComfyUI batch (upscale / removebg / crop)        (roadmap_producao_visual · Etapa 1)
              │
              ▼
   output/upscaled/<slug>/…                          (processado + backup dos originais)
              │  copiar pro destino final
              ▼
   frontend/public/assets/kaelis/<slug>/…  +  manifest.json
```

- `<tipo>` = `kaeli | item | mob | background | logo | motion`. `<slug>` = id sem prefixo
  (`waifu:velvet` → `velvet`).
- Para **movimento**, o brief de `kaeli-motion-prompts` aponta o destino: prompt image-to-video → o PC
  roda no ComfyUI; spec de Remotion → o PC executa em `tools/cinematics/`.
- A etapa "ComfyUI batch" é exatamente o que `roadmap_producao_visual.md` generaliza (Etapa 1, IMG-03 em diante).

---

## Tabela mestre — quem faz o quê, onde

| Tarefa | Superfície | Ferramenta | Skill / Roadmap |
|---|---|---|---|
| 8 assets de uma Kaeli (prompts) | 📱 Celular | ChatGPT (GPT Image) | `kaeli-asset-prompts` |
| Skin / tema alternativo (prompts) | 📱 Celular | ChatGPT | `kaeli-asset-prompts` (modo skin) · `roadmap_web_skins` |
| Posts de Instagram | 📱 Celular | ChatGPT | `kaeli-social-prompts` · `roadmap_web_social` |
| Spec de cutscene / prompt de vídeo | 📱 Celular | — (gera o brief) | **`kaeli-motion-prompts`** |
| Upscale / removebg / crop em lote | 💻 PC | ComfyUI (`:7879`) | Etapa 1 (IMG-*) |
| Idle breathing in-engine | 💻 PC | Angular (`kaeli-idle.ts`) | Etapa 2 (CUT-02) |
| Loop de idle premium (webm) | 💻 PC | ComfyUI (LivePortrait) | Etapa 2 (CUT-03) |
| Cutscene de summon / reveal | 💻 PC | Remotion (`tools/cinematics/`) | Etapa 2 (CUT-04) |
| Cinematic de skill (combate) | 💻 PC | Canvas in-engine (+Remotion opcional) | Etapa 2 (CUT-05) |
| Reels de marketing 9:16/1:1 | 💻 PC | Remotion / ComfyUI curto | Etapa 2 (CUT-06) |
| Sprite jogável no grid | 💻 PC | ComfyUI + renderer | Etapa 3 (SPR-*) |

---

## Por que esta separação (e não tudo no PC)

O dono do projeto passa períodos **sem o desktop**. O gargalo nunca é gerar a imagem-base no ChatGPT
(roda no celular); é o **pós-processo e a integração** (precisam de GPU/código). Separar deixa o
trabalho de **texto/brief acumular no celular** e o PC só **drenar a fila** quando estiver disponível —
o projeto não para. É a mesma tese do `docs_web/CLAUDE_WEB.md`, estendida para arte e movimento.

## Depois (fora deste workflow por enquanto)
- Geração de imagem-base **100% local** (Flux/SDXL no ComfyUI) p/ largar o GPT — só se um dia o GPT
  deixar de servir. Hoje o GPT entrega e é a escolha do projeto.
- MCP do ComfyUI (IMG-05) deixa um **agente no PC** drenar a fila sem a UI — automatiza a Superfície 2.
