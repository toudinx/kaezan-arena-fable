# CLAUDE_WEB.md — Doutrina da trilha Claude Code Web

> **Leia isto antes de qualquer prompt da trilha `docs_web/`.** Este arquivo define o que o
> agente rodando no **Claude Code Web** pode e não pode fazer neste repo. O `CLAUDE.md` raiz
> continua valendo; aqui é a camada específica do web.

## Por que esta trilha existe

Vão existir períodos (ex. 2 dias seguidos) em que o dono do projeto **não tem o desktop** e,
portanto, não pode implementar nem testar features. O objetivo desta trilha é **o projeto não
parar**: o web faz o trabalho de **texto** (design, pesquisa, conteúdo, e roadmaps prontos) que
**não precisa do jogo rodando** e não compete por tokens com a implementação no desktop.

O loop de **implementação + teste continua 100% no desktop**. O web nunca implementa feature.

## Regras inegociáveis

1. **O web só escreve markdown.** Nada de código.
   - **Pode escrever** em: `docs_web/**` e — exclusivamente o motor de roadmaps — `docs/roadmap_*.md`.
   - **Nunca escreve** em `backend/`, `frontend/`, `tools/`, nem em config (`.csproj`, `settings`,
     `package.json`, etc.). Nunca builda, nunca roda `dotnet`/`ng`, nunca roda o jogo.
2. **O web lê pouco e de propósito.** Fontes permitidas:
   - `README.md` (documentação viva — fonte primária).
   - `docs_web/roster_digest.md` (snapshot do roster — use em vez de ler `Domain/Waifus.cs`).
   - O roadmap web que está sendo executado + qualquer doc **explicitamente nomeado no prompt**.
   - **Não varra** `backend/`, `frontend/`, `tools/`. Se a tarefa parece exigir ler código, ela
     **não é** uma tarefa de web — registre como candidata a roadmap desktop e pare.
3. **Entrega sempre copiável.** Prompts de imagem em blocos de código nomeados pelo arquivo de
   destino; docs de pesquisa/roadmap em markdown estruturado.
4. **Pesquisa fecha com gancho.** Todo entregável de pesquisa termina numa seção
   **"Candidatos a roadmap desktop"** — é o que o motor `roadmap_web_specs.md` consome depois.
5. **Marcação de progresso.** Concluiu um item de roadmap? Marque `[x]` nele + 1 linha de resumo,
   igual aos roadmaps de implementação do repo.

## Etiqueta de dependência (como escolher o que fazer no dia)

Cada roadmap web declara no topo a sua **etiqueta**. Escolha pelo seu acesso do dia:

- 🟢 **Claude-only** — self-contained, roda em qualquer dia, só depende do Claude.
  (`roadmap_web_specs`, `roadmap_web_research`, `roadmap_web_lore`, `roadmap_web_marketing`)
- 🟡 **Precisa GPT Image + seu tempo** — a saída é um conjunto de prompts que **você** ainda vai
  colar no GPT Image 2.0 / DALL·E para gerar as imagens. (`roadmap_web_skins`, `roadmap_web_social`)

## Como disparar

> *"rode o prompt NN do `docs_web/<roadmap>.md`"* — o agente lê este arquivo + o roadmap + as
> fontes permitidas, executa **um** item e escreve a saída no destino indicado pelo item.

## Por que convenção e não trava no `settings.json`

O mesmo repo é usado **no desktop** para implementar (precisa escrever em `backend/`/`frontend/`).
Um `deny` global de Write travaria a implementação. Por isso o isolamento é por **convenção**
(este arquivo + prompts auto-contidos), não por permissão dura.
