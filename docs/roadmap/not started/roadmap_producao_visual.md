# Roadmap — Produção Visual (imagem · movimento · sprites)

> **Como usar este arquivo.** Cada `IMG-NN` / `CUT-NN` / `SPR-NN` é uma unidade auto-contida. Dispare
> com **"implemente o prompt IMG-NN do `docs/roadmap_producao_visual.md`"**. Cada prompt declara
> **Modelo · Effort · Skill · Depende de · Aceite · Verificação**.
>
> **Metodologia:** ver `docs/WORKFLOW_imagem_e_cutscenes.md` (PC ↔ celular). O **brief** (prompt /
> spec) sai no **celular** pelas skills (`kaeli-asset-prompts`, `kaeli-motion-prompts`); o
> **pós-processo, render e integração** é sempre **no PC**, drenando a fila `output/inbox/`.
>
> **Não confundir com:** a trilha web (`docs_web/`) — texto/prompt apenas. Este roadmap toca `tools/`,
> `frontend/` e os assets em `output/` / `frontend/public/assets/`.

## As 3 etapas

Uma progressão do mais barato/fundacional ao mais difícil. Cada etapa **reusa** a anterior.

| Etapa | Prefixo | O que produz | Superfície dominante | Dificuldade |
|---|---|---|---|---|
| **1 · Imagem estática** | `IMG-*` | Arte 2D (Kaelis, items, mobs, backgrounds, logos): GPT gera → ComfyUI pós | 📱 brief + 💻 ComfyUI | baixa/média |
| **2 · Movimento & cutscenes** | `CUT-*` | Idle breathing, summon, skill FX, reels — reusa a arte da Etapa 1 | 📱 brief + 💻 Remotion/ComfyUI/in-engine | média/alta |
| **3 · Sprites autorais** | `SPR-*` | Sprite **jogável no grid** (Kaelis + bosses) — toca o renderer | 💻 R&D no PC | alta |

> **Ordem macro:** Etapa 1 é base (a 2 e a 3 consomem os assets dela). Dentro de cada etapa há ondas
> próprias. Etapas 2 e 3 podem andar em paralelo entre si depois que a 1 tiver assets suficientes.

## Invariantes (valem nas 3 etapas)
- **Backend autoritativo / determinismo intacto.** Cutscene e sprite são **camada visual** — nunca
  mudam simulação, tick, colisão, RNG, nem resultado de pull/combate.
- **Sem API paga.** GPT Image (geração) + ComfyUI / Remotion / in-engine (resto). Vídeo IA local
  **8 GB-aware** (RTX 4070 laptop → clipe curto/baixa-res).
- **Sprites in-game só via `AssetsService`**; arte de Kaeli (idle/banner/thumb) via `KaeliArtService`
  — **não misturar** as duas camadas (convenção do `docs/FRONTEND_REMAP.md`).
- **`prefers-reduced-motion` sempre respeitado** em animação in-engine.
- **IDs estáveis** (`waifu:*`, `monster:*`); slug = id sem prefixo; `manifest.json` em sincronia.
- **Híbrido (Etapa 3):** entidade sem sprite autoral **cai no Tibia** (fallback). Nada quebra.
- **Etapa 1 não toca o jogo** (só `tools/`, `output/` e PNGs de asset). Etapas 2/3 tocam `frontend/`.
- **Remotion isolado** em `tools/cinematics/` — não entra no bundle do Angular; só o `.webm` cruza.

---
---

# ETAPA 1 — Imagem estática (GPT → ComfyUI)

## Tese
O fluxo de arte 2D é: **GPT Image 2.0 gera** (Kaelis, items, monstros, backgrounds, logos) →
**ComfyUI local pós-processa** (upscale + remoção de fundo + crop) → entra no jogo. A geração fica no
GPT (escolha do projeto, já funciona bem); esta etapa **generaliza o pós-processo local grátis** para
**todos os tipos de asset** (hoje só trata Kaeli, e só o upscale roda em lote) e abre o ComfyUI para
**agentes** via MCP.

## O que já existe
- `tools/kaezan_tools_ui.py` — UI web (porta 7879) que dispara jobs de batch.
- `tools/comfyui_batch.py` — orquestrador dos workflows no ComfyUI.
- `tools/upscale_anime.py` (✅ funciona em lote) · `tools/remove_bg.py` (⚠️ **batch quebrado**).
- `tools/workflows/upscale_2x_anime.json` · `removebg_isnet_anime.json`.
- `output/upscaled/<kaeli>/…` e `output/upscaled/_originais/<kaeli>/*-vN.png`.

## Matriz de pós-processo por tipo de asset
| Tipo                                | upscale | removebg | proporção    | observação                       |
| ----------------------------------- | ------- | -------- | ------------ | -------------------------------- |
| Kaeli — idle-1/2/3                  | sim     | **sim**  | ~2:3         | transparente                     |
| Kaeli — wallpaper/bg-landscape      | sim     | não      | 16:9         |                                  |
| Kaeli — bg-portrait                 | sim     | não      | 9:16         |                                  |
| Kaeli — banner                      | sim     | não      | 2:1          |                                  |
| Kaeli — thumb                       | sim     | não      | 1:1          |                                  |
| **Item** (ícone)                    | sim     | **sim**  | 1:1          | transparente                     |
| **Monstro art** (common/elite/boss) | sim     | **sim**  | conforme uso | card/bestiary (≠ sprite in-game) |
| **Background de página**            | sim     | não      | 16:9 / 9:16  |                                  |
| **Logo**                            | sim     | **sim**  | variável     | transparente                     |

> Geração desses assets continua no GPT (brief no celular via `kaeli-asset-prompts`). O Comfy só faz o
> **pós**. Destinos de item/monstro/logo que ainda não existem: deixar TODO "confirmar no desktop".

## IMG-00 — Fila de intake (`output/inbox/`)  ⭐
- **Modelo:** Codex · **Effort:** low · **Skill:** nenhuma · **Depende de:** — · (Onda 1, paraleliza c/ IMG-01)
- **Objetivo:** materializar a **fila de hand-off** do `docs/WORKFLOW_imagem_e_cutscenes.md`: a pasta
  `output/inbox/<tipo>/<slug>/` onde as imagens-base geradas no ChatGPT (celular) caem antes do
  pós-processo. `<tipo>` = `kaeli|item|mob|background|logo|motion`; `<slug>` = id sem prefixo.
- **Tarefas:** criar a árvore com `.gitkeep` por tipo + um `output/inbox/README.md` curto explicando a
  convenção e que o batch (IMG-03) lê daqui e escreve em `output/upscaled/`.
- **Aceite:** a árvore existe e o doc explica onde largar cada tipo de imagem-base.

## IMG-01 — Documentar o pipeline atual  ⭐
- **Modelo:** Codex · **Effort:** low · **Skill:** nenhuma · **Depende de:** — · (Onda 1)
- **Objetivo:** `tools/README.md` explicando o fluxo GPT→ComfyUI, como subir a UI (7879),
  pré-requisitos (ComfyUI + modelos), a convenção de `output/` (incluindo o `inbox/` do IMG-00),
  e o link p/ `docs/WORKFLOW_imagem_e_cutscenes.md`.
- **Aceite:** alguém novo processa 1 asset lendo só o doc.

## IMG-02 — Consertar o removebg em lote  ⭐
- **Modelo:** Codex · **Effort:** medium · **Skill:** nenhuma · **Depende de:** IMG-01 · (Onda 1)
- **Objetivo:** o removebg (ISNet anime) tem que rodar em **lote** igual ao upscale. Diagnosticar
  por que só o upscale funciona em batch e corrigir no `comfyui_batch.py` + `remove_bg.py` + UI.
- **Aceite:** apontar uma pasta com N imagens e receber N PNGs com alpha real, sem rodar 1 a 1.
- **Verificação:** lote nos 3 idles de uma Kaeli → 3 transparentes corretos.

## IMG-03 — Batch genérico por tipo de asset  ⭐
- **Modelo:** Codex · **Effort:** medium · **Skill:** nenhuma · **Depende de:** IMG-02 · (Onda 2)
- **Objetivo:** generalizar o batch além de Kaeli: um **config por tipo** (upscale on/off, removebg
  on/off, proporção-alvo, destino) cobrindo Kaeli/item/monstro/background/logo (ver matriz acima).
- **Tarefas:** tabela de tipos; seleção de tipo na UI; idempotência; ler de `output/inbox/<tipo>/`
  por padrão (IMG-00) e copiar pro destino certo.
- **Aceite:** processar um lote de itens e um de backgrounds, cada um com o tratamento correto.

## IMG-04 — Crop + validação por tipo
- **Modelo:** Codex · **Effort:** low · **Skill:** nenhuma · **Depende de:** IMG-03 · (Onda 3)
- **Objetivo:** crop centralizado p/ a proporção-alvo + validador que falha se faltar alpha onde
  devia, se a proporção estiver fora, ou se faltar arquivo no set (ex. os 8 de uma Kaeli).
- **Aceite:** validação numa pasta reporta OK/erros por asset.

## IMG-05 — MCP do ComfyUI local (agentes disparam workflows)  ⭐
- **Modelo:** Codex · **Effort:** medium · **Skill:** nenhuma · **Depende de:** IMG-03 · (Onda 3, paraleliza c/ IMG-04)
- **Objetivo:** expor o pipeline a **agentes** via MCP, apontando pro ComfyUI local — rodar
  upscale/removebg/batch por chamada de ferramenta, sem a UI. Avaliar reusar um MCP de ComfyUI
  open-source (ex. `shawnrushefsky/comfyui-mcp`) vs. um wrapper fino sobre o `comfyui_batch.py`.
  **Grátis/local, sem API paga.**
- **Aceite:** um agente consegue disparar o pós-processo de uma pasta e receber os finais.

## IMG-06 — Consistência de rosto (face detailer/restore)
- **Modelo:** Codex · **Effort:** medium · **Skill:** nenhuma · **Depende de:** IMG-03 · (Onda 3)
- **Objetivo:** workflow opcional de *face detailer*/restore no upscale p/ rosto nítido e consistente
  entre idle/banner/thumb; toggle no batch.

## IMG-07 — (Experimental) Variante de skin via img2img local
- **Modelo:** Opus · **Effort:** high · **Skill:** nenhuma · **Depende de:** IMG-03 · (Onda 4)
- **Objetivo:** explorar img2img + ControlNet/IPAdapter pegando o `idle-1` como base e trocando
  roupa/cenário mantendo pose e rosto — alternativa **grátis** ao GPT para skins. Só vale se a
  consistência ficar boa; senão, skins seguem no GPT.

## Execução da Etapa 1
- **Onda 1:** IMG-00 (intake) ‖ IMG-01 (doc) → IMG-02 (conserto do removebg, prioridade).
- **Onda 2:** IMG-03 (batch genérico, lê do intake).
- **Onda 3:** IMG-04 + IMG-05 + IMG-06 (todos dependem do batch; tocam partes distintas).
- **Onda 4:** IMG-07.

---
---

# ETAPA 2 — Movimento & cutscenes (idle · summon · skill · reels)

## Tese
O jogo já tem os ingredientes — falta **movimento** que faça o premium "respirar". Em vez de uma
solução única, três **motores** cobrem casos diferentes, do mais barato ao mais caro:

| Motor | Onde roda | Custo | Bom para |
|---|---|---|---|
| **In-engine** (Angular CSS / canvas) | no jogo, runtime | grátis, determinístico | idle breathing, FX de skill |
| **Remotion** (`tools/cinematics/`) | PC, pré-render → webm | grátis, **qualquer GPU** | summon/reveal, reels, intros |
| **ComfyUI-vídeo** (LTX/AnimateDiff/LivePortrait) | PC, GPU | grátis local, **pesado** | loops orgânicos de idle, experimental |

**Prioridade declarada: idle in-game primeiro.** Depois summon (Remotion já provado), depois skill e
reels. **Remotion é a espinha dorsal**; **ComfyUI-vídeo é apoio** (8 GB → clipes curtos/baixa-res).

## O que já existe (reusar, não reescrever)
- **Idle**: `<app-kaeli-idle>` (`frontend/src/app/core/ui/kaeli-idle.ts`) já faz **crossfade** das 3
  poses (`idle-1/2/3`) a cada 7s, com fallback p/ sprite Tibia e `prefers-reduced-motion`.
- **Reveal de pull**: overlay CSS cinematográfico em `frontend/src/app/pages/recruit/recruit.ts`
  (círculo arcano + burst + card por raridade) — já bom; cutscene de vídeo é o *upgrade*.
- **Remotion**: `tools/cinematics/` renderiza a composição **`GachaSummon`** (15s @30fps 1920×1080),
  **já parametrizada** (`name`, `title`, `thumbSrc`, `bgSrc`) — `npm run deploy` renderiza o webm e
  copia pra `frontend/public/assets/cinematics/`. Requer **FFmpeg** no PATH.

## CUT-01 — Spike de idle breathing  ⭐ (gate do idle)
- **Modelo:** Opus · **Effort:** high · **Skill:** `frontend-design` · **Depende de:** — · (Onda 1)
- **Objetivo:** decidir **como** o idle vai "respirar". Avaliar p/ **1 Kaeli** (Velvet, que tem arte):
  - **(a) transform CSS senoidal** sutil sobre a pose (scale/translate/skew leve em loop) — in-engine,
    grátis, determinístico, zero asset novo;
  - **(b) loop webm via ComfyUI LivePortrait** a partir de `idle-1` — orgânico, mas pesa GPU e vira
    1 asset por Kaeli;
  - **(c) Remotion** gerando um loop curto de breathing.
  Comparar por **qualidade percebida × custo por Kaeli × VRAM**. Recomendar a vencedora p/ shippar já
  e a opção premium (provável: **(a) agora**, **(b) opt-in depois**).
- **Arquivos prováveis:** `frontend/src/app/core/ui/kaeli-idle.ts` (camada de teste removível), este doc.
- **Aceite:** 1 Kaeli "respira" no preview pela técnica recomendada; relatório de 1 página com a decisão.
- **Verificação:** `npx ng build` limpo; `preview_screenshot` em 2 momentos mostra a pose deslocada;
  `prefers-reduced-motion` congela o movimento.

## CUT-02 — Idle breathing in-engine  ⭐
- **Modelo:** Opus · **Effort:** medium · **Skill:** `frontend-design` · **Depende de:** CUT-01 · (Onda 2)
- **Objetivo:** implementar a camada de breathing vencedora **sob** o crossfade existente do
  `<app-kaeli-idle>` (não substituir o crossfade — somar). Movimento sutil e contínuo (respiração),
  sem brigar com a troca de pose a cada 7s. Sem assets novos.
- **Tarefas:** animação CSS em loop (`@keyframes`) parametrizável (amplitude/período); desligar sob
  `prefers-reduced-motion`; garantir que não vaza timer e não causa layout shift.
- **Aceite:** as Kaelis com arte respiram na página Kaelis (`pages/kaelis/kaelis.ts`); fallback de
  sprite intacto; build verde.
- **Verificação:** `npx ng build` limpo; preview mostra o breathing; reduced-motion corta; console limpo.

## CUT-03 — Idle loop premium via ComfyUI (experimental, 8 GB-aware)
- **Modelo:** Opus · **Effort:** high · **Skill:** nenhuma (ComfyUI local) · **Depende de:** CUT-02 · (Onda 3)
- **Objetivo:** caminho **opt-in** de idle de alta qualidade: `<app-kaeli-idle>` toca um **`.webm`** de
  breathing **quando existir** (registrado no `manifest.json`), com **fallback** pro breathing CSS
  (CUT-02). Gerar **1 loop de teste** no ComfyUI (LivePortrait a partir de `idle-1`) respeitando os
  **8 GB de VRAM** (resolução/duração contidas, loop costurável).
- **Brief no celular:** `kaeli-motion-prompts` (modo idle) gera o prompt image-to-video.
- **Arquivos prováveis:** `tools/` (workflow ComfyUI), `frontend/.../kaeli-idle.ts`,
  `frontend/public/assets/kaelis/<slug>/idle-loop.webm`, `manifest.json`.
- **Aceite:** 1 Kaeli com `idle-loop.webm` toca o loop; as demais caem no breathing CSS sem regressão.
- **Verificação:** abrir o webm (loop sem "pulo", tamanho/duração razoáveis); `npx ng build` limpo;
  preview confirma o loop e o fallback.

## CUT-04 — Variantes de summon em Remotion
- **Modelo:** Codex (produção) + Opus (revisão) · **Effort:** medium · **Skill:** `remotion-best-practices` · **Depende de:** Etapa 1 (assets) · (Onda 1, paraleliza c/ CUT-01)
- **Objetivo:** generalizar a cutscene **`GachaSummon`** (já parametrizada) p/ as 7 Kaelis e por
  **elemento/raridade**: derivar cor de acento e cenário dos tokens `Cathedral Ink + Aurum`
  (`frontend/src/styles.css`) e do `roster_digest`. Registrar `<Composition>` por Kaeli em
  `tools/cinematics/src/Root.tsx` reusando `thumb` + `bg-landscape`.
- **Brief no celular:** `kaeli-motion-prompts` (modo summon) gera o roteiro/beats.
- **Aceite:** ≥2 Kaelis com webm de summon próprio renderizado e copiado p/
  `frontend/public/assets/cinematics/`; estilo coerente com o jogo.
- **Verificação:** `npm run deploy` no `tools/cinematics/`; abrir os webm; `npm run still` p/ checagem
  rápida de layout.

## CUT-05 — Cinematic de skill (combate)
- **Modelo:** Opus · **Effort:** high · **Skill:** `frontend-design` · **Depende de:** CUT-02 · (Onda 3)
- **Objetivo:** dar "peso" ao soltar skill **sem** quebrar o engine determinístico. Camada de **FX
  in-engine** no canvas do jogo (`frontend/src/app/core/renderer.ts`) disparada por evento visual do
  tick (flash/partícula/zoom por *shape* de skill — `single|beam|nova|area|cone|buff`), e **opcional**
  um clipe pré-renderizado p/ ultimates. **A cutscene não altera a simulação** — só lê o que o backend
  já manda.
- **Aceite:** ao menos 1 shape de skill ganha FX legível em combate; tick/colisão/resultado idênticos;
  build verde.
- **Verificação:** `npx ng build` limpo; preview de um combate mostra o FX; confronto determinístico
  intacto (mesmo seed → mesmo resultado).

## CUT-06 — Reels de marketing (9:16 / 1:1)
- **Modelo:** Codex · **Effort:** medium · **Skill:** `remotion-best-practices` · **Depende de:** CUT-04 · (Onda 2)
- **Objetivo:** clipes curtos p/ Instagram a partir de `wallpaper`/`banner` de uma Kaeli — parallax /
  ken-burns + motion de partículas no Remotion (e/ou ComfyUI curto p/ um plano orgânico). Cruza com
  `docs_web/roadmap_web_social.md` (pilar "reels/story 9:16").
- **Brief no celular:** `kaeli-motion-prompts` (modo reel).
- **Aceite:** 1 reel 9:16 e 1 quadrado 1:1 renderizados, reaproveitando assets prontos, sem API paga.
- **Verificação:** abrir os clipes; duração/tamanho de social; estilo on-brand.

## Execução da Etapa 2
```
Onda 1   CUT-01 (Opus · spike idle)      ‖  CUT-04 (Codex · summon Remotion)
              │  gate do idle in-engine          arquivos disjuntos: frontend vs tools/cinematics
              ▼                                        │
Onda 2   CUT-02 (Opus · breathing)       ‖  CUT-06 (Codex · reels)  [após CUT-04]
              │
              ▼
Onda 3   CUT-03 (idle webm) ‖ CUT-05 (skill FX)   [tocam partes distintas]
```
- **CUT-01 é o gate** do idle in-engine (CUT-02 → CUT-03). **CUT-04** (Remotion) é independente.
  Pratos cheios (CUT-01, CUT-02, CUT-05) no **Opus high**; produção bounded (CUT-04, CUT-06) no **Codex**.

---
---

# ETAPA 3 — Sprites in-game autorais (Kaelis + bosses)

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

## SPR-01 — Spike de técnica de sprite  ⭐
- **Modelo:** Opus · **Effort:** high · **Skill:** nenhuma · **Depende de:** — · (Onda 1)
- **Objetivo:** avaliar 2–3 abordagens para um sheet direcional consistente de **1 Kaeli de teste**
  e escolher a vencedora por **consistência entre direções/frames × custo por personagem**.
  Candidatas: (a) gerar pose-base e derivar direções por img2img/ControlNet; (b) modelo de
  pixel/sprite dedicado; (c) render 3D→2D; (d) IA + acabamento manual.
- **Aceite:** 1 Kaeli renderizada no grid de teste com ≥4 direções reconhecíveis como a mesma personagem.
- **Verificação:** carregar no jogo via um caminho de teste e andar nas 4 direções.

## SPR-02 — Contrato de sprite autoral no frontend
- **Modelo:** Opus · **Effort:** medium · **Skill:** nenhuma · **Depende de:** SPR-01 · (Onda 2)
- **Objetivo:** definir como o `AssetsService` carrega um sprite **não-Tibia**: formato de sheet,
  manifest (direções/frames), e se mantém recolor ou fixa cor. Implementar o caminho de carga +
  o **fallback** para Tibia quando não houver sprite autoral.
- **Contexto:** `frontend/src/app/core/assets.service.ts`, `renderer.ts`, `manifest`/`types.ts`.
- **Aceite:** uma entidade com sprite autoral renderiza por ele; sem sprite, usa Tibia, sem regressão.

## SPR-03 — Produzir as 7 Kaelis como sprite in-game
- **Modelo:** Codex (produção) + Opus (revisão) · **Effort:** high · **Skill:** nenhuma · **Depende de:** SPR-02 · (Onda 3)
- **Objetivo:** gerar e integrar os sheets das 7 Kaelis pela técnica vencedora, usando o
  `roster_digest`/assets existentes como referência de identidade.
- **Aceite:** as 7 Kaelis aparecem no grid com sprite autoral; build verde; fallback intacto.

## SPR-04 — Produzir os bosses
- **Modelo:** Codex + Opus · **Effort:** high · **Skill:** nenhuma · **Depende de:** SPR-02 · (Onda 3, paraleliza c/ SPR-03)
- **Objetivo:** sheets autorais dos bosses (1 por tier), mantendo `monster:*` estável.
- **Aceite:** cada boss renderiza com sprite autoral; mobs comuns/elites seguem no Tibia.

## SPR-05 — Polimento + escala visual
- **Modelo:** Opus · **Effort:** medium · **Skill:** nenhuma · **Depende de:** SPR-03, SPR-04 · (Onda 4)
- **Objetivo:** afinar escala/ancoragem/animação no grid, sombras e leitura em combate; documentar
  o processo de "nova entidade autoral" no `tools/README.md`.

## Execução da Etapa 3
- **Onda 1:** SPR-01 (spike — gate de tudo).
- **Onda 2:** SPR-02 (contrato no frontend).
- **Onda 3:** SPR-03 + SPR-04 (produção, partes distintas → paralelizam).
- **Onda 4:** SPR-05.

---
---

## Ordem entre etapas & "Depois"

- **Etapa 1 primeiro** (assets 2D são a matéria-prima das outras duas). Assim que houver assets
  suficientes de 1 Kaeli, **Etapa 2** (movimento) e **Etapa 3** (sprites) podem andar **em paralelo**
  entre si — tocam partes distintas (`tools/cinematics` + `kaeli-idle` vs. `renderer/AssetsService`).
- Dentro da Etapa 2, **idle (CUT-01→03)** é a prioridade; **summon (CUT-04)** é independente e provado.
- **Etapa 3 é a mais difícil** — SPR-01 (spike) é o gate; só escalar (SPR-03/04) se a técnica provar
  consistência barata.

### Depois
- Integrar o webm de summon no `recruit.ts` (tocar em 5★ destaque, skip → cai no reveal CSS) — seam
  já documentado no `tools/cinematics/README.md`.
- Geração de imagem-base **100% local** (Flux/SDXL no ComfyUI) p/ largar o GPT — fora de escopo
  enquanto o GPT entregar.
- Outfits autorais para mais monstros (além de boss) e addons/skins jogáveis em sprite autoral, se o
  spike (SPR-01) provar que escala barato.
