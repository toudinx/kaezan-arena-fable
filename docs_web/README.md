# docs_web — Trilha Claude Code Web

Esta pasta é o **território do Claude Code Web**. O web faz aqui o trabalho de **texto** que
mantém o projeto andando quando o desktop (implementação + teste) está indisponível: design,
pesquisa, conteúdo e roadmaps prontos para o desktop executar. **Nenhum código.**

> Antes de qualquer coisa, leia **[CLAUDE_WEB.md](CLAUDE_WEB.md)** — a doutrina (o que ler, onde
> escrever, formato de saída).

## Como usar

Você dispara um item de roadmap assim:

> *"rode o prompt NN do `docs_web/<roadmap>.md`"*

Escolha **qual** roadmap pela sua disponibilidade do dia (etiqueta de dependência):

| Roadmap | Etiqueta | O que produz | Skill |
|---|---|---|---|
| [roadmap_web_specs.md](roadmap_web_specs.md) | 🟢 Claude-only | Roadmaps de implementação prontos em `docs/roadmap_*.md` | `roadmap-from-plan` |
| [roadmap_web_research.md](roadmap_web_research.md) | 🟢 Claude-only | Mapeamento de gachas/animes/mangás, economia rival, conceitos de Kaeli | `franchise-mining` |
| [roadmap_web_lore.md](roadmap_web_lore.md) | 🟢 Claude-only | Expansão de lore/codex do universo Kaezan | inline |
| [roadmap_web_marketing.md](roadmap_web_marketing.md) | 🟢 Claude-only | Copy de loja, patch notes, captions | inline |
| [roadmap_web_skins.md](roadmap_web_skins.md) | 🟡 GPT Image | 8 prompts de imagem de uma skin de Kaeli | `kaeli-asset-prompts` |
| [roadmap_web_social.md](roadmap_web_social.md) | 🟡 GPT Image | Set de posts de social (prompts + caption + hashtags) | `kaeli-social-prompts` |

**Dia com tempo + GPT** → puxe os 🟡 (skins, social) e gere as imagens.
**Dia de pouco acesso** → puxe os 🟢 (specs, research, lore, marketing) — só dependem do Claude.

## O loop que mantém o projeto andando

```
DIA DE POUCO ACESSO (🟢, só Claude)              DIA COM TEMPO + GPT
─ research/economia → docs_web/research/*.md      ─ skins/social: prompts → você gera no GPT Image
─ conceito-kaeli   → docs_web/concepts/*.md       ─ desktop: implementa o que o web já especou
─ specs → docs/roadmap_*.md  ───────────────────► fila pronta p/ o desktop executar
```

O web **abastece** (fila de implementação + assets); o desktop **executa e testa**.

## Pastas de saída

- `specs/` — rascunhos/notas do motor de roadmaps (o roadmap final vai p/ `docs/`).
- `research/` — mapeamentos de franquias e economia rival.
- `concepts/` — pitches de novas Kaelis.
- `lore/` — expansão de codex.
- `marketing/` — copy de loja, patch notes, captions.
- `skins/` — docs de prompts de skin (8 prompts por skin).
- `social/` — docs de posts de social.

## Manutenção

- `roster_digest.md` é um snapshot manual do roster (vem de `Domain/Waifus.cs`). Quando o roster
  do backend mudar, atualize o digest **no desktop** — o web nunca lê o `.cs`.
