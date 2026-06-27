# kaezan_txt2img_results.md

Resultados do pipeline **TEXT-TO-IMAGE** do Kaezan — primeira personagem **Velvet**
(feiticeira gótica aristocrata). Tudo nasceu **só de texto**: sem IPAdapter, FaceID,
InstantID, img2img ou ControlNet. Rig: RTX 4070 Laptop **8 GB**. Seed de comparação
fixa: **19473621**.

> Modo de execução combinado com o usuário: **enxuto primeiro** — montar todos os
> workflows/entregáveis e rodar uma varredura focada (melhores presets em 1 seed +
> bracket de refine), em vez da matriz completa de 4 seeds. A expansão (mais seeds,
> teste de LoRA) está descrita e pronta para rodar se você aprovar a Velvet base.

---

## 1. Veredito rápido

**Vencedor:** **WAI Illustrious v16**, base **Euler A / normal / 28 / CFG 5.5** →
segundo passe **DPM++ 2M SDE / Karras / 14 / CFG 4.5 / denoise 0.22** com *latent
upscale 1.5×* → **1536×1536** decodificado *tiled*. **Sem LoRA.** Seed **19473621**.

Arquivo: `output/gen/velvet_txt2img/velvet_FINAL_wai_refine_d022_seed19473621.png`
Workflow: `kaezan_txt2img_final.json`

**Por que não o Animagine 4.0 (Modelo A) venceu, apesar de ser o "principal":** para
ESTE brief (gótico, paleta roxo-escuro, olhos crimson, clima sóbrio), os finetunes
**Illustrious** entregaram **olhos mais nítidos e simétricos** e **controle de cor
melhor**. O Animagine 4.0 deu um roxo **mais vívido/saturado** e olhos um tom mais
moles nos mesmos parâmetros — exatamente o "oversaturated purple" que o brief pede
para evitar. O Animagine 4.0 continua excelente e provavelmente vence em Kaelis de
paleta clara/colorida; aqui o Illustrious foi superior. (Licença: ambos permitem uso
comercial das imagens — ver audit §9.)

---

## 2. Matriz de comparação (A–H do spec §18)

Seed única 19473621, base 1024², refine a 1536².

| # | Config | Arquivo | Tempo | Nota geral |
|---|---|---|---|---|
| A | Animagine 4.0 + Euler A (28/cfg5.0) | velvet_animagine_eulera_cfg50_s28 | 83.8s | 3.6 |
| B | Animagine 4.0 + DPM++ 2M SDE Karras (28/cfg5.0) | velvet_animagine_dpmpp2msde_cfg50_s28 | ~80s | 3.5 |
| C | **WAI Illustrious + Euler A (28/cfg5.5)** | velvet_wai_eulera_cfg55_s28 | 80.5s | **4.4** |
| D | WAI Illustrious + DPM++ 2M SDE Karras (28/cfg5.0) | velvet_wai_dpmpp2msde_cfg50_s28 | 80.4s | 4.3 |
| E | Hassaku Illustrious + Euler A (28/cfg5.5) | velvet_hassaku_eulera_cfg55_s28 | 99.6s | 4.2 |
| F | **Vencedor C → refine 0.22 (1536²)** | velvet_FINAL_wai_refine_d022 | ~110s base+refine | **4.7** |
| G | Vencedor C → refine 0.18 | velvet_wai_refine_d018 | ~110s | 4.6 |
| H | Vencedor C → refine 0.30 | velvet_wai_refine_d030 | ~110s | 4.4 |

> O spec mapeia "Modelo A/B + sampler + refine + LoRA opcional" para A–H. Aqui a
> linha **H (LoRA opcional)** ficou **vazia de propósito**: a 1ª rodada é sem LoRA
> (spec §15) e o checkpoint sozinho já entregou nível gacha — só testar LoRA depois,
> uma por vez, se a licença for comercialmente compatível.

---

## 3. Avaliação 0–5 (finalistas, inspeção visual — spec §19)

| Critério | A Animagine EulerA | C WAI EulerA | F WAI refine 0.22 |
|---|:--:|:--:|:--:|
| 1. Qualidade do rosto | 4 | 4.5 | 5 |
| 2. Aparência adulta | 4.5 | 4.5 | 4.5 |
| 3. Simetria dos olhos | 3.5 | 4.5 | 5 |
| 4. Definição da íris | 3.5 | 4.5 | 5 |
| 5. Cílios | 3.5 | 4 | 4.5 |
| 6. Formato do rosto | 4 | 4.5 | 4.5 |
| 7. Mechas do cabelo | 4.5 | 4 | 5 |
| 8. Separação cabelo/fundo | 4 | 4 | 4.5 |
| 9. Shading da pele | 4 | 4.5 | 4.5 |
| 10. Lineart↔pintura | 4 | 4.5 | 4.5 |
| 11. Renda | 3.5 | 4 | 5 |
| 12. Joias | 3.5 | 4.5 | 5 |
| 13. Iluminação | 4.5 | 4 | 4.5 |
| 14. Contraste dos escuros | 3.5 | 4.5 | 4.5 |
| 15. Profundidade do fundo | 4.5 | 4 | 4.5 |
| 16. Coerência do figurino | 4 | 4.5 | 4.5 |
| 17. Ausência de artefatos | 4 | 4.5 | 4.5 |
| 18. Não parecer "IA genérica" | 4 | 4.5 | 4.5 |
| 19. Leitura em 256² | 4 | 4.5 | 4.5 |
| 20. Potencial de gacha oficial | 4 | 4.5 | 5 |

**Observações de inspeção:**
- O refine 0.22 melhorou exatamente o que devia: **íris com gradiente**, cílios,
  **renda do choker/corset** mais densa, **gema violeta legível**, mechas
  individuais — **sem trocar rosto, expressão, cor dos olhos nem enquadramento**.
- Bracket de denoise: **0.18** preserva o rosto de forma quase idêntica (mais
  conservador); **0.22** é o ponto-doce (adiciona detalhe sem deriva); **0.30** já
  começa a mexer levemente em traços/contraste do rosto → **recomendado 0.18–0.22**.

---

## 4. Parâmetros finais (config de QUALIDADE — `kaezan_txt2img_final.json`)

```
Checkpoint     : waiIllustriousSDXL_v160.safetensors   (Illustrious SDXL)
VAE            : embutido no checkpoint
Base pass      : 1024×1024, Euler Ancestral, scheduler normal, 28 steps, CFG 5.5, denoise 1.0
Latent upscale : LatentUpscaleBy 1.5  (bislerp)  → 1536×1536
Refine pass    : DPM++ 2M SDE, Karras, 14 steps, CFG 4.5, denoise 0.22
Decode         : VAEDecodeTiled (tile 512, overlap 64)   ← 8 GB-safe, sem OOM
LoRA           : nenhuma
Seed vencedora : 19473621
```

**Config LEVE (`kaezan_txt2img_lightweight.json`)** para iteração rápida:
```
1024×1024, Euler Ancestral, normal, 24 steps, CFG 5.0/5.5, sem refine, sem LoRA  (~50–80s)
```

---

## 5. Respostas objetivas (spec §23)

- **Melhor traço (linework):** WAI Illustrious v16 (Illustrious family). Animagine 4.0
  fica mais "vívido/pintado", Illustrious mais limpo e premium para este brief.
- **Melhor olhos:** **Euler Ancestral** na base (mais nítido e simétrico que DPM++
  aqui); o **refine DPM++ 2M SDE** acrescenta o gradiente de íris por cima.
- **Melhor cabelo:** DPM++ 2M SDE Karras dá as mechas mais fluidas; a combinação
  base Euler A + refine DPM++ entrega fio individual + fluidez.
- **Melhor segundo denoise (preserva rosto):** **0.18–0.22** (0.22 = ponto-doce;
  0.30 começa a derivar).
- **Alguma LoRA ajudou?** Não testada na 1ª rodada (política do spec). O checkpoint
  sozinho já atingiu nível gacha; LoRA fica para um spike opcional controlado.
- **VRAM:** 8 GB total. Base 1024² cabe folgado; **1536² só é viável com VAE tiled**
  (validado, sem OOM). `VAEDecode` puro a 1536² é o risco de estouro.
- **Tempo médio:** base 1024² ≈ **50–85 s**; base+refine 1536² ≈ **110 s** (primeiro
  job de cada checkpoint soma o tempo de load do `.safetensors`).
- **Melhor config de qualidade:** §4 (final.json).
- **Melhor config leve:** §4 (lightweight.json).
- **Prompt positivo final:** `velvet_positive_prompt.txt`.
- **Prompt negativo final:** `velvet_negative_prompt.txt`.
- **Seed vencedora:** **19473621**.

---

## 6. Problemas remanescentes

1. **Consistência de identidade:** text-to-image cria o *design*, mas a **mesma**
   Velvet não se reproduz pixel-a-pixel entre seeds/poses. Resolve-se com uma
   **Character LoRA** (plano §8) treinada num dataset curado da Velvet vencedora.
2. **Cílios/íris ainda variam** levemente seed-a-seed; o refine estabiliza, mas
   um `FaceDetailer` leve (denoise 0.15–0.18) é o plano B se algum seed sair com
   olho fraco (opcional, spec §14 — **não foi necessário** no seed vencedor).
3. **Mãos:** a base foi gerada **sem mãos** (mais seguro). A variante "uma mão no
   colo" deve ser testada por último e descartada se piorar rosto/figurino.
4. **Animagine 4.0 saturado:** se quiser usá-lo, baixar CFG (~4.0) e tirar tags de
   cor fortes do positivo; ainda assim o Illustrious foi melhor aqui.

---

## 7. Próximos passos para uma Velvet consistente

1. Travar seed **19473621** + `kaezan_txt2img_final.json` como a Velvet canônica.
2. Gerar **30–60 variações controladas** (mesma config, seeds vizinhas + pequenas
   variações de expressão/ângulo no Composition Block).
3. Curar **15–25** imagens consistentes: retrato frontal, três-quartos, perfil,
   busto, corpo inteiro, expressões, detalhes do figurino.
4. **Só então** treinar a Character LoRA (plano §8). **Não treinar nesta task.**

---

## 8. Plano FUTURO — Character LoRA da Velvet (não executar agora)

**Objetivo:** preservar rosto, cabelo, roupa, acessórios, silhueta e paleta da Velvet
em centenas de imagens, mantendo o **Style Anchor** responsável pelo acabamento geral.

**Dataset (15–25 imgs curadas do passo §7):**
- 6–8 retratos/bustos (frontal, ¾, perfil) — variar levemente expressão e luz.
- 3–5 corpo inteiro / cowboy shot (definir a silhueta e o figurino completo).
- 3–5 close de detalhe (tiara, choker+gema, renda do corset) — ancora os acessórios.
- 2–3 expressões (neutra, leve sorriso, séria).
- Fundo simples/variado para a LoRA não decorar o cenário.
- Resolução de treino 1024² (buckets SDXL), tags estilo booru consistentes; usar um
  **token raro** dedicado (ex: `vel_kaezan`) como trigger.

**Treino (SDXL, 8 GB-friendly):**
- Ferramenta: kohya_ss / sd-scripts (LoRA SDXL).
- Rank/alpha: 16/8 a 32/16 (começar 16/8). Network: LoRA (não LoCon na 1ª).
- LR ~1e-4 (unet) / 5e-5 (text encoder), cosine, ~10–20 épocas, batch 1 + grad accum.
- 8 GB: `--network_train_unet_only` se faltar VRAM; gradient checkpointing; fp16/bf16.
- Validar com a MESMA seed/prompt da base; rejeitar se a LoRA inflar busto, mudar a
  cor dos olhos, "colar" o fundo do dataset ou aproximar de franquia conhecida.

**Uso depois de pronta:** `gen ... --lora vel_kaezan.safetensors:0.7` + Style Anchor.
Peso típico 0.6–0.8; a identidade vem da LoRA, o acabamento continua no Style Anchor.

> **Limitação honesta:** text-to-image puro NÃO garante o mesmo rosto em escala. A
> Character LoRA é o passo que converte "um design bonito" em "uma personagem
> reproduzível" para banners, wallpapers, idles e skins.

---

## 9. Entregáveis (nesta pasta `tools/txt2img_kaezan/`)

- `kaezan_txt2img_audit.md` — auditoria do rig/modelos/licenças.
- `kaezan_txt2img_01_baseline.json` — passe único 1024².
- `kaezan_txt2img_02_refine.json` — base + refine 1536² tiled.
- `kaezan_txt2img_final.json` — **config vencedora** (WAI + refine 0.22).
- `kaezan_txt2img_lightweight.json` — config leve de iteração.
- `kaezan_txt2img_results.md` — este arquivo.
- `kaezan_txt2img_contact_sheet.png` — grade visual + thumbs 256².
- `kaezan_style_anchor.txt` — bloco de acabamento reutilizável (todas as Kaelis).
- `velvet_character_block.txt` — identidade substituível da Velvet.
- `velvet_positive_prompt.txt` / `velvet_negative_prompt.txt` — prompts finais.
- imagens em `output/gen/velvet_txt2img/`.

**Validação:** todos os workflows são API-format e foram **executados de verdade** no
ComfyUI (não dependem de node ausente). Para carregar no canvas do ComfyUI, converta
com `python tools/comfyui_batch.py emit-ui -w <arquivo>.json` (API→UI), ou rode direto
com `python tools/comfyui_batch.py run -w <arquivo>.json`.

---

## 10. Upgrade de qualidade (rodada de LoRA + hires generativo)

**Sintoma:** comparado à referência premium, o refine 0.22 saía "macio/chapado" — faltava
densidade de detalhe (renda, íris, contraste de splash art). **O fix NÃO foi upscaling de
pixel** (ESRGAN sozinho só suaviza); foi **re-renderizar com mais detalhe** via:

1. **Hires generativo** (TiledDiffusion + VAEEncode/DecodeTiled, 8 GB-safe) a **1.5× →
   1536²** com **denoise 0.40** (vs 0.22) — o passe passa a ADICIONAR detalhe, não só limpar.
2. **Stack de LoRA de detalhe/gacha** (rodada do spec §15, agora executada):
   `DetailedEyes_V3 @0.45` + `StS-Illustrious-Detail-Slider-v1.0 @0.40` + `GachaSplash @0.40`.
3. **Upscale final ESRGAN** `4x-AnimeSharp → 0.5` = **3072²** (nível wallpaper), por cima
   do render já detalhado.

**Resultado:** olhos crimson glossy com gradiente, renda do choker/corset nítida, mechas
separadas, rim light dramático — **nível da referência**. Arquivos:
- `velvet_Q2_hires040_eyesDetail_seed19473621.png` (eyes+detail slider)
- `velvet_Q3_hires040_eyesDetailGacha_seed19473621.png` (**+ GachaSplash = vencedor**)
- `velvet_Q3_FINAL_upscaled_3072_seed19473621.png` (3072² final)

**Receita de qualidade atualizada (`kaezan_txt2img_final.json`):**
```
base   : WAI Illustrious v16, Euler A / normal / 28 / CFG 5.5, 1024², seed 19473621
LoRA   : DetailedEyes_V3:0.45 + StS-Illustrious-Detail-Slider:0.40 + GachaSplash:0.40
hires  : TiledDiffusion 1.5x -> 1536², denoise 0.40, 18 steps  (VAEEncode/DecodeTiled)
upscale: ESRGAN 4x-AnimeSharp -> 0.5 = 3072²  (passe separado, opcional)
tempo  : ~120-150s o hires (1º job soma load); +29s o ESRGAN
```

> ⚠️ **LICENÇA das LoRAs — verificar antes de shippar.** `DetailedEyes_V3`,
> `StS-Illustrious-Detail-Slider` e `GachaSplash` são LoRAs da comunidade (Civitai) com
> licença a **confirmar no card** de cada uma (aba *License/Permissions*). Se alguma não
> permitir uso comercial: o **fallback** é a config sem-LoRA (`kaezan_txt2img_02_refine.json`,
> finish um pouco menor) ou trocar por uma LoRA de detalhe comercialmente clara. O
> **checkpoint** (WAI/Illustrious) segue como o item de licença principal a confirmar
> (ver audit §9). As LoRAs são *detail enhancers* genéricos (não-identidade, não-artista).
