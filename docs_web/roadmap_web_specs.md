# Roadmap Web — Specs (motor de roadmaps)  🟢 Claude-only

> **Etiqueta: 🟢 Claude-only.** Só depende do Claude — roda em qualquer dia, inclusive nos de
> pouco acesso. **É o motor de momentum:** transforma pesquisa/conceitos em **roadmaps de
> implementação prontos** que o desktop executa quando você voltar. Leia
> [CLAUDE_WEB.md](CLAUDE_WEB.md) antes.
>
> **Como disparar:** *"rode o prompt SP-NN do `docs_web/roadmap_web_specs.md`"*.
>
> **Carve-out de escrita:** este é o único roadmap web que pode escrever em **`docs/roadmap_*.md`**
> (markdown, não código). Rascunhos/notas vão em `docs_web/specs/`.

## O que este roadmap faz

Pega um **candidato a roadmap desktop** já produzido pela pesquisa (`docs_web/research/*.md` ou
`docs_web/concepts/*.md`) e usa a skill **`roadmap-from-plan`** para emitir um roadmap executável
no formato canônico do repo (prompts NN auto-contidos, Modelo · Effort · Skill · Depende de ·
Aceite · Verificação, grafo de ondas). O desktop então dispara *"implemente o prompt NN do
docs/<roadmap>.md"*.

**Importante (limite do web):** o web ancora os prompts no que está no `README.md` + no candidato
de pesquisa. Onde um prompt precisaria de caminho/assinatura de código real que não está no README,
o web **deixa um TODO explícito "confirmar no desktop"** em vez de inventar. Um roadmap web é um
**rascunho forte**; a ancoragem fina em arquivos reais é um passo rápido de desktop.

## Itens

### [ ] SP-01 — Converter um candidato de pesquisa em roadmap desktop
- **Skill:** roadmap-from-plan · **Lê:** README.md + o doc de pesquisa nomeado (ex.
  `docs_web/research/wuthering-waves.md`) · **Escreve:** `docs/roadmap_<tema>.md` (+ notas em `docs_web/specs/`)
- **Objetivo:** pegar um candidato de pesquisa e produzir o roadmap de implementação pronto p/ o desktop.
- **Entrada:** o caminho do doc de pesquisa/conceito + qual recorte virar roadmap.
- **Saída:** `docs/roadmap_<tema>.md` no formato canônico, com TODOs "confirmar no desktop" onde faltar âncora de código.
- **Aceite:** roadmap segue o formato dos roadmaps existentes; cada prompt tem Aceite/Verificação objetivos; zero código tocado.

### [ ] SP-02 — Converter um conceito de Kaeli em roadmap de implementação
- **Skill:** roadmap-from-plan · **Lê:** README.md + roster_digest.md + `docs_web/concepts/<kaeli>.md` · **Escreve:** `docs/roadmap_kaeli_<nome>.md`
- **Objetivo:** transformar um pitch de Kaeli nova num roadmap (dados em `Waifus.cs`/`Classes.cs`,
  trait, skin, assets, integração) que o desktop implementa e testa.
- **Aceite:** roadmap cobre dados + trait + arte (aponta p/ a skill `kaeli-asset-prompts`) + integração; TODOs de âncora marcados.

### [ ] SP-03 — Backlog → roadmap temático
- **Skill:** roadmap-from-plan · **Lê:** README.md + as notas que você apontar · **Escreve:** `docs/roadmap_<tema>.md`
- **Objetivo:** quando você tem notas/ideias espalhadas (não pesquisa de franquia), o web as
  organiza num roadmap executável.

## Candidatos a roadmap desktop
> (vazio — preenchido conforme a pesquisa gera candidatos)
