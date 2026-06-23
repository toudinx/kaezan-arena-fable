# CUT-01 — Spike de idle breathing (relatório de decisão)

**Status:** ✅ concluído · **Etapa 2 (movimento)** do `roadmap_producao_visual.md` · gate do idle in-engine.
**Pergunta:** *como* o idle das Kaelis deve "respirar"? Avaliado para **1 Kaeli (Velvet)**, que tem arte autoral.

## Candidatas avaliadas

| # | Técnica | Onde roda | Custo / Kaeli | VRAM | Qualidade percebida |
|---|---------|-----------|---------------|------|---------------------|
| **(a)** | **Transform CSS senoidal** sobre a pose (translateY + scale leve, ancorado nos pés), `@keyframes` em loop | in-engine, runtime | **zero** (nenhum asset novo; vale para as 7 Kaelis de imediato) | 0 | sutil, "vivo", determinístico; não tenta simular dobra de tecido/cabelo |
| **(b)** | **Loop `.webm` via ComfyUI LivePortrait** a partir de `idle-1` | PC, pré-render | 1 asset/Kaeli + tempo de geração + costura de loop | **alto** (8 GB → clipe curto/baixa-res) | orgânico de verdade (peito/cabelo/tecido), mas pesa pipeline e storage; risco de "pulo" no loop |
| **(c)** | **Remotion** gerando loop curto de breathing | PC, pré-render → webm | 1 asset/Kaeli, render por composição | baixa (qualquer GPU) | controlado, mas para *breathing* puro reduz a um transform pré-renderizado — mesma estética da (a) só que como vídeo (mais peso, menos flexível) |

## Recomendação

- **Shippar já: (a) transform CSS senoidal.** Custo zero, cobre as 7 Kaelis no mesmo instante, determinístico,
  respeita `prefers-reduced-motion`, e não compete com o crossfade de poses existente (transform na `.art`,
  opacity nas `.layer` — eixos disjuntos). É o que **CUT-02** promove à camada definitiva.
- **Premium opt-in depois: (b) `.webm` via LivePortrait** apenas onde valer (destaque de banner), com **fallback**
  para o breathing CSS quando não houver webm — exatamente o escopo de **CUT-03**.
- **(c) Remotion** fica **descartada para breathing** (não agrega sobre a (a); Remotion segue sendo a espinha
  dorsal para *summon/reels*, não para respiração de idle).

## Prova (camada de teste, técnica (a))

Implementada uma camada **removível** em [`kaeli-idle.ts`](../../frontend/src/app/core/ui/kaeli-idle.ts):
input `[breathing]` (default `true` no spike) que aplica a classe `.breathe` (`@keyframes kaeli-breathe`,
período `--breathe-period` 4.5s, `transform-origin: center bottom`). Amplitude proposital baixa:
`translateY(-0.7%)` + `scaleY(1.012)`/`scaleX(0.994)` no pico.

- **Build:** `npx ng build` limpo (só warnings de CSS-budget pré-existentes em outras páginas).
- **Deslocamento medido (Velvet, harness isolado no preview):** topo da figura sobe **~8.71px** num quadro de
  460px (≈1.9%) entre repouso e pico, **com os pés fixos** (origem na base). Sutil, sem layout shift.
- **`prefers-reduced-motion`:** congelado por guarda dupla — `[class.breathe]="breathing() && !reduceMotion"`
  no template **e** `.breathe { animation: none }` na media-query.

## Reverter / promover

Spike isolado em 3 pontos de `kaeli-idle.ts`: o input `breathing`, a classe `.breathe` no template e o
bloco `@keyframes kaeli-breathe` + `.breathe` no CSS. **CUT-02** parametriza amplitude/período e o liga por
padrão nas Kaelis com arte; para reverter, remover esses 3 pontos.
