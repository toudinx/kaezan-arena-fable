# Roadmap Web — Research (mapeamento de franquias)  🟢 Claude-only

> **Etiqueta: 🟢 Claude-only.** Roda em qualquer dia. Mapeia obras (gachas, animes, mangás)
> contra o universo Kaezan e gera material que vira fila do desktop. Leia
> [CLAUDE_WEB.md](CLAUDE_WEB.md) antes.
>
> **Como disparar:** *"rode o prompt RS-NN do `docs_web/roadmap_web_research.md`"*.
> **Skill:** `franchise-mining` (modos: `gacha` · `anime/manga` · `economia-rival` · `conceito-kaeli` · `scout`).
> **Escreve em:** `docs_web/research/` (mapeamentos, economia) e `docs_web/concepts/` (Kaelis).
> Todo entregável fecha em **"Candidatos a roadmap desktop"**.

## Alvos nomeados

### Gachas (modo `gacha` + `economia-rival`)
- [ ] **RS-01 — Wuthering Waves** → `docs_web/research/wuthering-waves.md`
- [ ] **RS-02 — Genshin Impact** → `docs_web/research/genshin-impact.md`
- [ ] **RS-03 — Honkai: Star Rail** → `docs_web/research/honkai-star-rail.md`
- [ ] **RS-04 — Nikke** → `docs_web/research/nikke.md`
- [ ] **RS-05 — Zenless Zone Zero** → `docs_web/research/zenless-zone-zero.md`

### Animes / mangás (modo `anime/manga`)
- [ ] **RS-06 — Solo Leveling** → `docs_web/research/solo-leveling.md`
- [ ] **RS-07 — Mushoku Tensei** → `docs_web/research/mushoku-tensei.md`
- [ ] **RS-08 — Douluo Dalu (Soul Land)** → `docs_web/research/douluo-dalu.md`
- [ ] **RS-09 — Tales of Demons and Gods** → `docs_web/research/tales-of-demons-and-gods.md`

### Prompts recorrentes (rode quando quiser)
- [ ] **RS-SCOUT — Scout de obras novas** (modo `scout`) → `docs_web/research/scout-<data>.md`
  - Indica **3 obras novas** (gacha/anime/mangá/light novel) que combinam com o universo Kaezan
    e **por quê**, com 1 gancho mecânico e 1 de lore cada. Não repete obras já mapeadas.
- [ ] **RS-CONCEPT — Conceito de Kaeli nova** (modo `conceito-kaeli`) → `docs_web/concepts/<nome>.md`
  - A partir de um mapeamento já feito (ou de um arquétipo/raça que falta no roster), propõe uma
    Kaeli: arquétipo, elemento (cobrir buracos da matriz), *shape* de kit, trait de assinatura,
    identidade visual e cenário ancorado. Fecha com candidatos a roadmap desktop.
- [ ] **RS-ECON — Economia/pity de um rival** (modo `economia-rival`) → `docs_web/research/econ-<jogo>.md`
  - Mapeia pity, taxas, soft/hard pity, moedas, custo de pull, pacing de banner de um gacha em
    **números acionáveis** comparáveis aos nossos (banners/pity já citados no README).

## Formato de cada entregável (resumo — a skill detalha)
1. **Resumo da obra** (1 parágrafo) + **por que combina com Kaezan**.
2. **Lore hooks** adaptáveis (regiões, facções, conceitos) — sem copiar nomes registrados.
3. **Mecânicas adaptáveis** ao engine (mapeadas a *shapes* de skill / sistemas existentes).
4. **Arquétipos / Kaelis** que a obra inspira (elemento + papel + traço).
5. **Candidatos a roadmap desktop** (o gancho para `roadmap_web_specs.md`).

## Observações
- **Nunca copiar** nomes/IPs registrados — extrair *ideias, regras e composição de sistemas*
  (mesma filosofia do `docs/DESIGN_NOTES.md`). Tudo reescrito no universo Kaezan.
- Priorizar elementos/raças/papéis **pouco usados** no roster atual (ver `roster_digest.md`).
