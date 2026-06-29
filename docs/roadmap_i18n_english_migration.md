# Roadmap — Migração PT-BR → Inglês (jogo + código)

> Playbook único da migração de idioma. Cada prompt é **auto-contido**: pode ser despachado num
> chat/agente separado. Ondas paralelizam por **propriedade disjunta de arquivos** (cada prompt é
> dono de arquivos que ninguém mais toca → zero conflito de merge).
>
> **Decisão:** hard-replace PT→EN agora, **sem** infra de i18n/catálogo. Um switch de idioma é uma
> fase futura, construída do zero depois. Este roadmap deixa o app 100% inglês como base limpa.

## §0 — Regras de trabalho

- **Frontend (tudo que o jogador vê) e código → inglês, sem exceção.** Strings de display do
  backend que chegam ao cliente contam como "frontend" → inglês.
- **Comentários de código → 100% inglês, sem exceção.**
- **Lore/specs/docs ficam em português:** `docs/**` (GDD, DESIGN_NOTES, KNOWLEDGE_*, WORKFLOW_*,
  roadmaps) e `docs_web/**`. **Não traduzir** — é o material que o usuário revisa. (Exceção: a
  política de idioma adicionada nos `CLAUDE.md`/`README`, prompt P08.)
- **IDs estáveis NÃO mudam** — renomear quebra persistência de conta. Idênticos:
  `waifu:*`, `skin:*`, `card:*`, `echo:*`, `banner:*`, `skill:*`, `trait:*`, espécies/IDs numéricos
  do Tibia (lookTypes/objetos). Traduzir **apenas** `.Name`, `.Title`, `.Description`,
  `.Personality`, `.Lore[]`, `.SkinDef.Description`, labels e mensagens — o texto de display ao lado
  do ID, nunca o ID.
- **Determinismo intocado:** texto não entra no tick. Não alterar lógica/RNG/constantes.
- **Cabeçalho fixo** a colar no topo de cada prompt P0x:
  > *"IDs estáveis (`waifu:* skin:* card:* echo:* banner:* skill:* trait:*`, espécies/IDs numéricos
  > do Tibia) NÃO mudam — só o texto de display ao lado. Use o glossário deste roadmap. Traduza
  > strings E comentários PT→EN apenas dos arquivos da minha lista; não toque em nada fora dela.
  > Ao final: `dotnet build` (backend) ou `npx ng build` (frontend) limpo."*

## §1 — Glossário canônico (todo prompt usa)

UI / domínio:

| PT | EN |
|---|---|
| Kaeli / Kaelis | **Kaeli / Kaelis** (mantém — nome próprio) |
| Caçada | Hunt |
| Recrutar / Recrutamento | Recruit |
| Convocação / Convocar | Summon |
| Mochila | Backpack |
| Bestiário | Bestiary |
| Início | Home |
| Ferramentas | Tools |
| Nível de conta | Account Level |
| Modos de jogo | Game Modes |
| Expedição | Expedition |
| Abismo Sem Fim | Endless Abyss |
| Boss Rush | Boss Rush |
| Raide de Esquadrão | Squad Raid |
| Tiers da caçada | Hunt Tiers |
| Escolher Kaeli | Choose Kaeli |
| Multiplicador | Multiplier |
| Mobs / Elites / Limpezas | Mobs / Elites / Clears |
| Recompensas do boss | Boss Rewards |
| Tentativas | Attempts |
| Contratos / Contratos Diários | Contracts / Daily Contracts |
| Destaque | Featured |
| Duplicatas | Duplicates |
| Taxas do banner | Banner Rates |
| Informações | Info |
| Fechar / Pular animação | Close / Skip animation |
| Vender 1 / Tudo | Sell 1 / All |
| Loot de venda | Sell value |
| Mochila vazia — vá caçar! | Empty backpack — go hunt! |
| abates / vitórias | kills / wins |
| masmorras / dungeons | dungeons |
| Sair | Quit / Leave |
| Som ligado / desligado | Sound on / off |
| Helper de combate / Auto-loot / Auto-heal | Combat helper / Auto-loot / Auto-heal |
| Postura → quebra / ECHO BREAK | Stance → break / ECHO BREAK |
| Saque | Loot |
| Ouro / Kaeros | Gold / Kaeros (Kaeros mantém) |

Slots (`backpack.ts`): Capacete→Helmet, Armadura→Armor, Arma→Weapon, Colar→Necklace, Anel→Ring,
Montaria→Mount.

Elementos (`ELEMENT_LABELS`): Físico→Physical, Fogo→Fire, Gelo→Ice, Energia→Energy, Terra→Earth,
Morte→Death, Sagrado→Holy, Suporte→Support.

Armas (`WEAPON_LABELS`): Corpo a corpo→Melee, Arco→Bow, Cajado→Wand.

Condições (`GameConfig.ConditionLabelPt`): veneno→poison, queimadura→burn, eletrocussão→shock,
sangramento→bleed, maldição→curse, congelamento→freeze, afogamento→drown, ofuscamento→dazzle.
→ **renomear o dicionário** `ConditionLabelPt` → `ConditionLabel` e atualizar o uso em
`GameWorld.cs` (`$"morta por {...}"` → `$"killed by {...}"`). Não é ID persistido.

Erros (`GameHub.cs`): "tier desconhecido"→"unknown tier"; "Kaeli não recrutada"→"Kaeli not
recruited"; "requer conta nível {N}"→"requires account level {N}".

Material de Eco: "Estilhaço de Eco · T{tier}" → "Echo Shard · T{tier}".

**Lore/descrições de Kaeli:** traduzir preservando **tom e atmosfera** (não literal). A cópia PT
canônica vive em `docs_web/` (lore) como fonte de revisão; o backend embarca a versão EN. Se
divergirem, os docs são a fonte de verdade.

## §2 — Ondas (grafo de execução)

```
Onda 0:  P00 (glossário/este doc)
              │  (todos os abaixo dependem só do glossário)
Onda 1:  P01  P02  P03  P04  P05  P06  P07     ← paralelos (arquivos disjuntos)
              │   (P02 renomeia ConditionLabelPt; P03 atualiza o uso — coordenar)
Onda 2:  P08 (docs/política)                    ← paralelo com a Onda 1
Onda 3:  P09 (integração + build + smoke)        ← depois de tudo mergeado
```

## §3 — Prompts

### P00 — Glossário & política · **Opus 4.8 · M** · ✅ (este documento)
Cria este roadmap com glossário e ondas. **Aceite:** glossário completo, ondas mapeadas.

### P01 — Backend: conteúdo/lore das Kaelis · **Opus 4.8 · effort high** · ✅
Traduziu Waifus/Classes/Cards/Biomes.cs (lore, skills, traits, cards, classes, biomas, skins +
comentários) PT→EN; IDs intactos; `dotnet build` limpo.
- **Arquivos (dono):** `backend/src/KaezanArenaFable.Api/Domain/Waifus.cs`, `Domain/Classes.cs`,
  `Domain/Cards.cs`, `Domain/Biomes.cs`
- **Faz:** traduzir `.Name`/`.Title`/`.Description`/`.Personality`/`.Lore[]` de Kaelis; nomes+
  descrições de skills, traits e cards (common/rare/echo); nomes+descrições de classes; nomes/
  atmosfera de biomas; descrições de skin — **+ todos os comentários**. Manter todos os IDs.
- **Aceite:** zero PT (diacríticos/palavras) nesses arquivos; IDs intactos; `dotnet build` limpo.

### P02 — Backend: config/labels · **Sonnet 4.6 · effort medium** · ✅
Concluída: `GameConfig.cs` está com labels/material/condition em inglês, `ConditionLabel` renomeado e comentários PT remanescentes migrados.
- **Arquivos (dono):** `Domain/GameConfig.cs`
- **Faz:** renomear `ConditionLabelPt`→`ConditionLabel` (+valores EN, @286–290); gear/material
  ("Estilhaço de Eco · T{tier}"→"Echo Shard · T{tier}", @728); demais labels; **+ os muitos
  comentários de design**. Constantes de simulação permanecem (só o comentário muda).
- **Coordenar:** P02 renomeia o dicionário; **P03** atualiza o uso em `GameWorld.cs`. Despache
  P02 e P03 juntos ou em sequência curta.
- **Aceite:** sem PT; `dotnet build` limpo.

### P03 — Backend: engine · **Sonnet 4.6 · effort medium** · ✅
Traduziu comentários e textos de evento/run-end em `Engine/GameWorld.cs`, `DungeonGenerator.cs` e
`GameDtos.cs`; `ConditionLabel` usado; sem alteração intencional de lógica/RNG.
- **Arquivos (dono):** `Engine/GameWorld.cs`, `Engine/DungeonGenerator.cs`, `Engine/GameDtos.cs`
- **Faz:** mensagens run-end (`$"morta por {...}"`→`$"killed by {...}"`, @3748/@4250); atualizar
  referência a `ConditionLabel`; **+ todos os comentários** (centenas). **Não** alterar lógica/RNG.
- **Aceite:** sem PT; `dotnet build` limpo; nenhuma mudança comportamental.

### P04 — Backend: meta / api / hubs / content · **Sonnet 4.6 · effort medium** · ✅
- **Arquivos (dono):** `Meta/GachaService.cs`, `Meta/DailyService.cs`, `Hubs/GameHub.cs`,
  `Api/MetaEndpoints.cs`, `Content/ContentStore.cs` (+ demais `Meta/`, `Api/`, `Content/` com PT)
- **Faz:** nomes/descrições de banners; templates de daily ("Derrote {n}x {species}..."→
  "Defeat {n}x {species}..."); erros de Hub/API; **+ comentários**. Manter `banner:*` e demais IDs.
- **Aceite:** sem PT; `dotnet build` limpo.

### P05 — Frontend: core (constantes + serviços) · **Sonnet 4.6 · effort small** · [x]
Traduziu labels/comentários do `core` e componentes UI compartilhados PT→EN; `npx ng build` sem erros.
- **Arquivos (dono):** `frontend/src/app/core/types.ts` (`ELEMENT_LABELS`, `WEAPON_LABELS`,
  @822–837), `core/game-modes.ts` (`GAME_MODES`), demais `core/*.ts` com strings/comentários PT
  (assets.service, renderer, game-client, sound, api.service).
- **Aceite:** sem PT; `npx ng build` limpo.

### [x] P06 — Frontend: páginas do jogador · **Sonnet 4.6 · effort medium**
Concluída: páginas do jogador, shell e `index.html` migrados para EN (templates, aria/title, toasts e comentários); `npx ng build` limpo.
- **Arquivos (dono):** `pages/home/`, `pages/hunt/`, `pages/mode/`, `pages/recruit/`,
  `pages/backpack/` (inclui `slotLabel`), `pages/kaelis/`, `pages/bestiary/`, `pages/game/`,
  `pages/prerun/`, `shell/`, `index.html` (title + confirmar `lang="en"`).
- **Faz:** texto de template, `aria-label`/`title`/`placeholder`, toasts/mensagens, **+ comentários**.
  Nota: várias strings já vêm sem acento ("Convocacao", "Informacoes") — traduzir normalmente.
- **Aceite:** sem PT; aria-labels em inglês; `npx ng build` limpo.

### P07 — Frontend: páginas de admin · **Sonnet 4.6 · effort medium** · ✅
Traduziu `pages/admin/**` (abas, labels, mensagens, breadcrumbs/eyebrows e comentários) PT→EN; `npx ng build` limpo.
- **Arquivos (dono):** `pages/admin/**` (admin.ts, kaeli-studio, kaeli-wardrobe, item-editor,
  monster-editor, role-tuning-editor, creature-preview)
- **Faz:** abas ("Monstros/Items/Skins/Papéis/Banners"→"Monsters/Items/Skins/Roles/Banners"),
  labels, mensagens de status, eyebrows ("Acervo visual", "Biblioteca Canary"), **+ comentários**.
- **Aceite:** sem PT; `npx ng build` limpo.

### P08 — Docs: política de idioma · **Opus 4.8 · effort medium** · ✅ (feito junto do plano)
- **Arquivos (dono):** `CLAUDE.md` (raiz), `frontend/CLAUDE.md`, `README.md`, `docs/STYLE_GUIDE.md`,
  `docs_web/CLAUDE_WEB.md` (confirmar PT). Adiciona a regra de idioma; atualiza termos de UI do
  README para inglês. **Não** traduz o corpo de lore dos demais docs.
- **Aceite:** regra de idioma presente nos `CLAUDE.md`; README reflete termos EN.

### [x] P09 — Integração & verificação · **Opus 4.8 · effort medium**
Concluída: varredura PT zerada fora de docs, builds backend/frontend limpos e smoke Home/Hunt/Recruit/Backpack/run/Admin em inglês.
- **Depende de:** P01–P08 mergeados.
- **Faz:** varredura de PT remanescente, build dos dois lados, smoke no preview.
- **Varredura (read-only)** — Grep por diacríticos/âncoras PT em `.cs`/`.ts`/`.html`, ignorando
  `docs/**` e `docs_web/**`:
  `[ãâáàçéêíóôõú]|\b(de|para|com|não|você|caçada|mochila|nível)\b`. Tratar achados.
- **Aceite:** `dotnet build` + `npx ng build` limpos; varredura sem achados de UI/código fora de
  docs; smoke (Home → Hunt → Recruit → Backpack → run → /admin) 100% inglês.

## §4 — Verificação end-to-end (P09)

1. `cd backend && dotnet build` — sem erros.
2. `cd frontend && npx ng build` — sem erros.
3. Grep do regex acima em `backend/**.cs`, `frontend/**.ts`, `frontend/**.html` (excl. docs) → 0.
4. Subir backend (`dotnet run`) + `npm start`; via preview tools navegar Home → Hunt → Recruit →
   Backpack → entrar numa run → `/admin`; confirmar inglês em todas as telas e run-end
   ("killed by …"). `preview_snapshot`/`preview_screenshot` como prova.
5. Spot-check de comentários em `GameWorld.cs`/`GameConfig.cs`/páginas — em inglês.

## §5 — Fase futura (fora deste roadmap) — Switch de idioma

Quando as implementações estiverem prontas: introduzir um seam de i18n (serviço de tradução baseado
em signals no frontend + locale por conta resolvido no backend autoritativo) e reintroduzir o PT
como locale opcional. Construído do zero então — esta migração entrega a base 100% inglês.
