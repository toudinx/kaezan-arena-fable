# KNOWLEDGE — Fechar (e passar) o GPT na geração de Kaeli premium

> Análise de gap GEN-01. Referência viva: comparação **arte GPT (canônica)** vs **geração local
> NetaYume + hires** (caso Velvet, 2026-06-25). Objetivo declarado pelo usuário: o local só vale a
> pena se ficar **igual ou MELHOR** que o GPT — senão o GPT vence (mais simples, roda da rua, só
> incomoda a censura). Então não basta empatar: precisamos de eixos onde **superamos**.

## Imagens de referência
- GPT canônica: `frontend/public/assets/kaelis/velvet/thumb.png`
- Melhor local até agora: `output/gen/velvet_v3_hires/thumb-1.png`
  (NetaYume · seed 777002 · 1024² + hires tiled 1.5x denoise 0.4 · tags splash-art gacha)

## Onde o local JÁ empatou (sessão 06-25)
Tema, paleta roxa, tiara, ametista, atmosfera de catedral/velas, enquadramento 1:1 sem cortar o
busto (a dor-raiz), roupa não-comportada (ombro/decote). O **hires foi o que destravou o "traço"**.

---

## Gap elemento por elemento

| # | Elemento | GPT (canônica) | Nosso (v3_hires) | Lever p/ fechar/superar | Ferramenta / GEN | Prio |
|---|---|---|---|---|---|---|
| 1 | **Olhos** | íris c/ gradiente radial, 2-3 catchlights, vítreo, pupila nítida, pálpebra dupla | íris mais chapada, 1 brilho, menos profundidade | passe dedicado de rosto + tags de olho vítreo | **FaceDetailer (Impact Pack)** + GEN-11 | **P0** |
| 2 | **Cílios / aegyo-sal** | cílio grosso definido, sombra sob o olho (sedução/volume) | cílio simples, aegyo-sal ausente | FaceDetailer + `sharp eyelashes, aegyo-sal` | Impact Pack | **P0** |
| 3 | **Expressão** | pálpebra baixa, sedutora **sem vulgar** | neutra/estática | `half-lidded seductive eyes, sultry gaze, soft smile` | style bible (GEN-01) | P1 |
| 4 | **Pele / shading** | painterly, blush gradiente, AO sob queixo, subsurface | mais flat, pouco blush/AO | FaceDetailer + `subsurface scattering, blush`, hires denoise ↑ | Impact Pack | P1 |
| 5 | **Cabelo** | fios finos, brilho roxo-azul, **correntes/joias trançadas**, flyaways | mechas "em bloco", sem joia no cabelo, menos fio | `intricate hair with jewelry chains, fine hair strands, flyaway hair` + hires 2.0 | GEN-01 + GEN-11 | P1 |
| 6 | **Coroa / headpiece** | coroa gótica ornada (filigrana, espinhos) + flor lateral | tiara mais simples | `ornate gothic crown, intricate filigree, dark silver, side flower` | GEN-01 | P2 |
| 7 | **Roupa / renda** | renda preta rica, gemas costuradas, mangas sheer | renda ok mas menos densa, broche central genérico | `intricate black lace, beaded gems, sheer lace sleeves` + hires + (ControlNet ref) | GEN-01 / GEN-04 | P1 |
| 8 | **Mãos** | 1 mão no peito, dedos elegantes, anatomia ok | **escondidas** (braços ao lado, corte no quadril) | pose mão-no-peito (ControlNet) + **HandDetailer** | GEN-03 + GEN-11 | P2 |
| 9 | **Composição / pose** | crop íntimo, leve inclinação, assimetria cinematográfica | frontal **simétrica/dura**, rosto menor | quebrar simetria: `dynamic angle, head tilt, off-center` + seed sweep / ControlNet | GEN-01 / GEN-03 | P1 |
| 10 | **Iluminação** | chiaroscuro, rim forte, HDR, pretos profundos | mais lavada/uniforme, menos contraste | `dramatic chiaroscuro, high contrast, strong rim light` + grade no pós | GEN-01 + pós | P1 |
| 11 | **Fundo / bokeh** | bokeh pintado, profundidade, arquitetura desfocada | catedral **simétrica/gamey**, vitral centralizado | `painterly bokeh, blurred background, asymmetric` ou gerar bg à parte | GEN-05 / GEN-06 | P2 |
| 12 | **Cor / grading** | roxos ricos + âmbar de vela + pele quente, saturado controlado | mais dessaturado/frio, pretos rasos | grade no pós (curvas/saturação) — ganho barato | tool de pós (PIL/ffmpeg) | P1 |
| 13 | **Traço base / render** | semi-real glossy de alta produção (DALL·E) | NetaYume glossy mas 1 nível abaixo | **spike de checkpoint/style-LoRA** + LoRA por Kaeli | GEN-08 + backlog | **P0-teto** |

---

## Plano priorizado (instalar tudo de uma vez, depois iterar com alvo)

**Onda A — destravar o rosto (maior salto, ataca o tell nº1):**
1. Instalar **ComfyUI-Impact-Pack** (Manager) → libera **FaceDetailer** + **HandDetailer** + Ultralytics.
   Baixar também `face_yolov8m.pt` (bbox) e `hand_yolov8s.pt` se quiser mão.
2. Rodar `facerestore` com checkpoint **NetaYume** (não o SD1.5 default) e prompt de olho vítreo.

**Onda B — style bible afinado (P1 sem instalar nada):**
3. No `kaeli_style_profiles.json`: tags de olho/expressão/cabelo-joia/renda/coroa, e quebrar simetria
   (`dynamic angle, head tilt, off-center composition`) + iluminação (`chiaroscuro, high contrast`).
4. **Hires 2.0** (já provado nos 8 GB com VRAM fresca) + escolher upscaler por conteúdo
   (Nomos2 p/ realismo, AnimeSharp p/ linha).

**Onda C — ganhos baratos de cor + mãos/pose:**
5. **Color-grade no pós** (PIL/ffmpeg): +saturação, +contraste, pretos mais profundos. Ganho grande, custo ~0.
6. Pose **mão-no-peito** via ControlNet (GEN-03) + HandDetailer — só quando o rosto já estiver no nível.

**Onda D — o TETO (onde a gente passa o GPT de vez):**
7. **Spike de checkpoint/style-LoRA** pro "traço" base — testar 2-3 checkpoints/merges glossy e travar o
   melhor por tipo de Kaeli. É o que decide *empatar vs superar* o render base.
8. **LoRA por Kaeli (GEN-08)** — rosto idêntico em qualquer pose/roupa/cena. **O GPT não faz isso.**

---

## Onde a gente NÃO empata o GPT — e onde a gente SUPERA

**GPT continua mais fácil em:** rodar de qualquer lugar (nuvem), zero setup, prompt em linguagem natural.
Isso não dá pra bater localmente — é conveniência, não qualidade.

**Onde o local PASSA o GPT (a tese desta trilha):**
- **Sem censura** — fan-service/poses que o GPT recusa (IMG-08).
- **Consistência travada** — LoRA por Kaeli = mesmo rosto em 50 assets; GPT re-sorteia o rosto toda vez.
- **Controle total** — enquadramento exato (busto sem corte), pose (ControlNet), composição (regional),
  proporção (outpaint) — o GPT chuta.
- **Resolução/refino** — hires + upscale dão saída maior e mais nítida que o output do GPT.
- **Custo/escala** — gerar 100 variações de seed pra escolher a melhor é grátis local.

**Conclusão:** empatar o *traço* base exige a Onda D (checkpoint/LoRA). Mas em **consistência, controle,
censura e escala** o local já nasce superior — é por aí que "fazer melhor que o GPT" se sustenta, não só
no pixel-a-pixel de um único frame.
