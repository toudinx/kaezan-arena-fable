---
name: roadmap-from-plan
description: >-
  Converte um PLAN (de plan mode em ~/.claude/plans/, um plano colado pelo usuário, ou um
  conjunto de notas de design) num ROADMAP executável no formato dos roadmaps deste repo
  (docs/roadmap_refactor_kaelis.md e docs/roadmap_meta_gameplay.md): prompts NN auto-contidos,
  cada um com Modelo · Effort · Skill · Depende de · Aceite · Verificação, mais grafo de
  dependências e ondas de execução paralela. Use SEMPRE que o usuário pedir para "transformar
  este plano em roadmap", "criar um roadmap a partir do plan", "gerar roadmap detalhado",
  "quebrar este plano em prompts/tasks executáveis", ou disser "como o roadmap_refactor_kaelis"
  / "como os outros roadmaps". Não é para escrever o plano em si (isso é plan mode) — é para
  CONVERTER um plano já existente no formato de roadmap do projeto.
---

# Roadmap From Plan

Pega um plano (estratégia + decisões) e produz um **roadmap executável** no padrão deste repo:
uma sequência de prompts auto-contidos que um agente "frio" consegue implementar um a um, com
dependências explícitas e ondas de paralelização.

## Por que esta skill existe

Um plano descreve *o que fazer e por quê*. Mas quem executa cada pedaço (Opus, Codex, ou você num
outro dia) começa **sem o contexto da conversa**. O roadmap resolve isso: cada unidade de trabalho
carrega o próprio contexto, critérios de aceite e como verificar. É a diferença entre "tenho um
plano" e "consigo disparar `implemente o prompt NN do docs/<roadmap>.md` e o agente faz sozinho".

Este repo já tem dois roadmaps nesse formato — `docs/roadmap_refactor_kaelis.md` (referência canônica)
e `docs/roadmap_meta_gameplay.md`. Esta skill generaliza esse padrão para qualquer plano.

## Entrada

Um plano, vindo de uma destas fontes (pergunte se não estiver claro):
- Um arquivo de plan mode em `~/.claude/plans/*.md`.
- Texto colado pelo usuário.
- Notas de design espalhadas que o usuário aponta.

Se o plano estiver raso (sem decisões fechadas, sem arquivos-alvo), **não invente** — leia o código
relevante para ancorar os prompts em arquivos e funções reais, ou pergunte ao usuário o que falta.

## Fluxo

### Passo 1 — Extrair do plano
- **Contexto/Tese:** por que a mudança existe (problema → resultado esperado).
- **Decisões fechadas:** tudo que o usuário já cravou (não reabrir).
- **Invariantes:** restrições do repo que todo prompt respeita (puxe do `CLAUDE.md` raiz e do
  `backend/CLAUDE.md` — ex. determinismo do engine, constantes em `GameConfig.cs`, IDs estáveis,
  builds verdes).
- **Unidades de trabalho:** quebre o plano em pedaços pequenos o bastante p/ um agente fechar numa
  rodada, grandes o bastante p/ serem coerentes.

### Passo 2 — Ordenar e paralelizar
- Monte o **grafo de dependências** entre as unidades (o que precede o quê e por quê — dependência real, não cosmética).
- Detecte **conflito de arquivos**: duas unidades que editam o mesmo arquivo **não** paralelizam.
- Agrupe em **ondas**. Regra de ouro: duas unidades só rodam juntas se (a) deps fecharam **e**
  (b) não tocam o mesmo arquivo. Casamento natural neste repo: 1 Opus (design/engine) + 1 Codex (bounded) por onda.
- Para trabalho de medição/verificação (testes, simulador, build), coloque o instrumento de medição
  **primeiro** (captura baseline) e a verificação **por último** (precisa de tudo fechado).

### Passo 3 — Escolher Modelo · Effort por unidade
- **Opus 4.8** (`high`/`medium`): decisão de game design, invariantes de engine, balanceamento, qualquer coisa onde errar cascateia.
- **GPT-5.5 (Codex)** (`low`/`medium`): mudança bounded com regra já fechada e padrão a seguir (texto, CRUD copiando endpoint existente, limpeza de UI).
- Sinalize `use context7` nos prompts que consultam API de biblioteca (ASP.NET Core, EF Core, SignalR, Angular).
- Indique a **Skill** se alguma unidade depender de uma (senão "nenhuma").

### Passo 4 — Escrever o roadmap (formato canônico)
Escreva em `docs/roadmap_<tema>.md`, em **português**, espelhando `docs/roadmap_refactor_kaelis.md`.
Prefixo de prompt curto e único por roadmap (ex. `K-` kaelis, `MG-` meta-gameplay; escolha um livre p/ o tema).

Estrutura obrigatória:
1. **Título** + bloco `> Como usar este arquivo` (como disparar `implemente o prompt NN do docs/<arquivo>.md`; lista de campos por prompt; aviso "não confundir com" os outros roadmaps).
2. **Modelos & quando usar** (tabela Opus vs Codex).
3. **Invariantes inegociáveis** (puxados do CLAUDE.md).
4. **Tese** + **Decisões Fechadas**.
5. (Opcional) Tabelas de referência técnica do tema (mapeamentos, valores seed).
6. **Mapa de prompts (escopo)**: tabela `Prompt | Tema | Modelo | Effort | Depende de | Onda`.
7. **Execução paralela ⭐**: diagrama ASCII das ondas + lista de "conflitos que forçam sequencial".
8. **Um bloco por prompt**, no formato exato:

```
# NN-XX — Título  [⭐ se for fundação]

- **Modelo:** … · **Effort:** … · **Skill:** … · **Depende de:** … · **Paraleliza com:** … (Onda N)

**Objetivo:** o porquê + o quê, em 2-4 linhas.

**Contexto técnico / Arquivos prováveis:** caminhos reais (file_path), assinaturas, linhas-âncora.

**Tarefas:** bullets acionáveis.

**Aceite:** critérios objetivos e verificáveis.

**Verificação:** como provar que funcionou (build, sim, preview, testes).
```

9. **## Depois**: trabalho fora de escopo p/ não perder ideias.

### Passo 5 — Fechar
- Cada prompt deve ser **executável por um agente frio** (sem depender da conversa atual).
- Aceite e Verificação **objetivos** (build verde, número do simulador, screenshot — não "ficou bom").
- Marcação de progresso: prompts começam sem `[x]`; quem executa marca `[x] NN-XX` + 1 linha de resumo (convenção do CLAUDE.md raiz).
- Confirme com o usuário se o particionamento/ondas batem com a expectativa antes de considerar pronto.

## Regras de ouro
- **Não reabra decisões já fechadas** no plano — o roadmap as registra como "Decisões Fechadas".
- **Ancore em arquivos reais.** Prompt sem caminho/função concreta é desejo, não tarefa. Leia o código se faltar.
- **Dependência real, não cosmética.** Só encadeie quando B de fato precisa de A.
- **Conflito de arquivo mata paralelismo.** Na dúvida, sequencial.
- **Medição primeiro, verificação por último.**
- Português, espelhando o tom e a estrutura de `docs/roadmap_refactor_kaelis.md`.
