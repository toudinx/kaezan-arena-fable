# Roadmap Web — Skins  🟡 Precisa GPT Image + seu tempo

> **Etiqueta: 🟡.** A saída é um conjunto de **8 prompts de imagem** que **você** ainda vai colar
> no GPT Image 2.0 / DALL·E para gerar a arte. Puxe num dia em que tenha tempo + acesso ao gerador.
> Leia [CLAUDE_WEB.md](CLAUDE_WEB.md) antes.
>
> **Como disparar:** *"rode o prompt SK-NN do `docs_web/roadmap_web_skins.md`"*.
> **Skill:** `kaeli-asset-prompts` (**modo skin**). **Lê:** README.md + roster_digest.md.
> **Escreve:** `docs_web/skins/<slug>-<tema>.md`.

## O que é uma "skin" aqui

Uma roupa alternativa (e cenário) de uma Kaeli existente, mantendo **rosto, cabelo e olhos** dela
(reconhecível) mas trocando **roupa + acessórios + cenário** pelo tema. Gera os mesmos **8 assets**
do set padrão: 3 idles, wallpaper, bg-landscape, bg-portrait, banner, thumb.

> No **modo skin** da skill, o bloco de identidade preserva os traços imutáveis (rosto/cabelo/olhos
> do digest) e substitui roupa/acessórios/cenário pelo tema. Mantém a consistência entre os 8.

## Itens (skins planejadas)

> Cada item: **Skill:** kaeli-asset-prompts (modo skin) · **Lê:** README + roster_digest ·
> **Escreve:** `docs_web/skins/<slug>-<tema>.md`. **Saída:** 8 prompts nomeados por arquivo +
> instruções de salvamento. **Aceite:** 8 blocos; rosto/cabelo/olhos idênticos entre os 8;
> cenário coerente com o tema.

- [ ] **SK-01 — Eloa · Verão** → `docs_web/skins/eloa-summer.md`
- [ ] **SK-02 — Velvet · Verão** → `docs_web/skins/velvet-summer.md`
- [ ] **SK-03 — Rin · Verão** → `docs_web/skins/rin-summer.md`
- [ ] **SK-04 — Lunara · Verão** → `docs_web/skins/lunara-summer.md`
- [ ] **SK-05 — Seren · Natal** → `docs_web/skins/seren-christmas.md`
- [ ] **SK-06 — Gaia · Natal** → `docs_web/skins/gaia-christmas.md`
- [ ] **SK-07 — Rynna · Ano Novo** → `docs_web/skins/rynna-newyear.md`

### Prompt recorrente
- [ ] **SK-NEW — Nova skin** → `docs_web/skins/<slug>-<tema>.md`
  - Dispare com: *"rode SK-NEW: skin da `<Kaeli>` tema `<X>`"*. A skill monta o bloco de
    identidade (modo skin) a partir do digest e emite os 8 prompts.

## Temas sugeridos (banco de ideias)
verão/praia · natal · ano novo · noiva · escolar · kimono/festival · gala/baile · halloween ·
versão "corrompida"/sombria · uniforme corporativo (vibe ZZZ) · maid/café.

## Depois de gerar
1. Gere os 8 prompts no GPT Image; salve em `frontend/public/assets/kaelis/<slug>-<tema>/` (ou na
   convenção de skin que o frontend usar — confirmar no desktop).
2. Registre a skin no jogo (entra como `SkinDef` em `Waifus.cs` — passo de **desktop**; vira
   candidato a roadmap se você quiser que a skin seja jogável e não só arte de divulgação).
