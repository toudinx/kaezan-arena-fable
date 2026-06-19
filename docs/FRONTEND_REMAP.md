# FRONTEND_REMAP — Redesign premium do frontend (alvo: Wuthering Waves)

> **Como usar este arquivo.** Cada `PROMPT N` abaixo é uma unidade de trabalho **auto-contida**:
> o agente que executa começa "frio", então o prompt já traz todo o contexto que ele precisa.
> Você dispara com **"execute o prompt N do `docs/FRONTEND_REMAP.md`"** e o agente faz o resto.
>
> Cada prompt declara: **Modelo · Effort · Skill · Depende de · Critério de aceite · Verificação.**
> Execute em ordem — há dependências reais (o design system precede tudo).
>
> **Não confundir com:** `docs/ROADMAP.md` (Codex) e `docs/FABLE_TRACK.md` (Fable engine).
> Este arquivo é só o remap visual do frontend. Nenhum prompt aqui toca no backend/engine.

---

## Modelos & quando usar

| Modelo | Papel | Effort típico | Por quê |
|---|---|---|---|
| **Claude Code Opus 4.8** | Decisões de "gosto", linguagem visual, arquitetura de serviço, animação | `high` / `max` | Altitude errada aqui cascateia pra todas as telas. Vale o modelo premium. |
| **GPT-5.5 (Codex)** | Conversão de componentes já especificados, seguindo padrão estabelecido | `medium` | Tarefas bounded, "siga o padrão da tela X". Barato e rápido. |

A skill **`remotion-best-practices`** só aparece no prompt opcional de cutscene de vídeo.
Use **`use context7`** ao consultar API do Angular 21 (signals, control flow, defer).

### Disponibilidade de skill por agente (importante)

A skill **`frontend-design` está instalada só no Claude Code** (`--agent claude-code`) — o
**GPT-5.5/Codex não a enxerga.** Por isso:
- **Prompts no Opus 4.8** (1, 3, 5, 6, 9, 10): invocam `frontend-design` — é onde mora a decisão
  de gosto/estética de alta altitude.
- **Prompts no GPT-5.5/Codex** (4, 7, 8): **não dependem da skill.** Eles seguem o
  `docs/STYLE_GUIDE.md` (gerado no Prompt 1) + replicam o padrão da Home já redesenhada (Prompt 3).
  São conversões bounded — o Codex copia a linguagem visual já estabelecida, não a inventa.

> Se você quiser instalar `frontend-design` também no Codex no futuro, dá — mas é desnecessário:
> o `STYLE_GUIDE.md` é o contrato compartilhado entre os dois modelos.

---

## Invariantes do frontend (todo prompt respeita)

- Componentes **standalone com template inline** (padrão do repo). Signals, não RxJS.
- **`AssetsService` é só para sprites do Tibia em-jogo** (drawOutfit/drawObject/...). A arte nova
  de personagem (idle/wallpaper/banner) é uma **preocupação separada** → `KaeliArtService`.
  Não misturar as duas. O renderer Canvas do jogo (`core/renderer.ts`) **não muda** neste remap.
- IDs estáveis (`waifu:*`) não renomear.
- `npx ng build` deve passar sem erros ao fim de cada prompt.
- **Admin (`/admin`) é ferramental — NÃO redesenhar.** Só sai da nav principal (Prompt 7).

---

## Decisão de assets (já tomada)

```
frontend/public/assets/kaelis/
  velvet/
    idle-1.png        # pose neutra, transparente, corpo inteiro
    idle-2.png        # mão no colar, transparente
    idle-3.png        # mão no cabelo, transparente
    wallpaper.png     # cena completa landscape (Velvet dentro) — Home Hub
    bg-landscape.png  # catedral vazia landscape — camada de parallax
    bg-portrait.png   # catedral portrait 9:16 — fundo da página Kaelis
    banner.png        # Velvet à direita, 2:1 — página Recrutar
    thumb.png         # busto quadrado — roster/cards/seleção
  _placeholder/
    (gerado no Prompt 0 — fallback temático por elemento)
```

**Fallback (Kaelis sem arte):** `KaeliArtService` devolve `null` para os campos ausentes; os
componentes caem em (a) `app-outfit-preview` (sprite Tibia) para idle e (b) gradiente por elemento
para fundos. **Nunca** reusar a arte da Velvet em outra Kaeli.

---

## Mapa de telas (escopo)

| Tela | Arquivo | Prompt | Modelo |
|---|---|---|---|
| Design system + primitivos | `styles.css` + `core/ui/*` | 1 | Opus 4.8 |
| KaeliArt service + `<app-kaeli-idle>` | `core/kaeli-art.service.ts` + `core/ui/kaeli-idle.ts` | 2 | Opus 4.8 |
| Home Hub | `pages/home/home.ts` | 3 | Opus 4.8 |
| Recrutar / Banner | `pages/recruit/recruit.ts` | 4 | GPT-5.5 |
| Reveal de convocação | `pages/recruit/recruit.ts` | 5 | Opus 4.8 |
| Página Kaelis + idle 7s | `pages/kaelis/kaelis.ts` | 6 | Opus 4.8 |
| Shell / nav + admin discreto | `shell/shell.ts`, `app.routes.ts` | 7 | GPT-5.5 |
| Telas secundárias | hunt, backpack, bestiary, mode, prerun | 8 | GPT-5.5 |
| Polish & verificação | global | 9 | Opus 4.8 |
| (Opcional) Cutscene 5★ Remotion | novo projeto `tools/cinematics` | 10 | Opus 4.8 |

---

# PROMPT 0 — Intake de assets & scaffold de pastas

- **Modelo:** GPT-5.5 · **Effort:** low · **Skill:** nenhuma · **Depende de:** —

**Objetivo.** Preparar a estrutura de pastas e os placeholders de fallback. **Tarefa de
preparação** — não mexe em componentes.

**Pré-requisito humano (você).** Coloque os 8 arquivos da Velvet em
`frontend/public/assets/kaelis/velvet/` com os nomes exatos listados na seção "Decisão de assets".

**Tarefas do agente:**
1. Garanta a árvore `frontend/public/assets/kaelis/velvet/` e `.../​_placeholder/`.
2. Crie `frontend/public/assets/kaelis/_placeholder/` com 6 gradientes temáticos (1 por elemento:
   physical, fire, ice, energy, earth, holy, death — pode agrupar physical/earth) como `.svg`
   leves (gradiente radial escuro → cor do elemento). São o fundo de fallback de wallpaper/banner.
3. Crie `frontend/public/assets/kaelis/manifest.json` listando quais ids têm arte
   (`{"waifu:velvet": ["idle-1","idle-2","idle-3","wallpaper","bg-landscape","bg-portrait","banner","thumb"]}`).
4. Valide que os 8 arquivos da Velvet existem; se faltar algum, liste no output e **não** falhe o build.

**Critério de aceite:** árvore criada, manifest válido, placeholders presentes.
**Verificação:** `ls frontend/public/assets/kaelis/velvet/` mostra 8 arquivos.

---

# PROMPT 1 — Design system & primitivos de UI  ⭐ fundação

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** `frontend-design` · **Depende de:** 0
- **[x] Concluído** — `styles.css` reescrito como token system "Cathedral Ink + Aurum" (íris p/ UI,
  aurum p/ recompensa, vidro c/ crystal edge); Fraunces+Sora em `index.html`; primitivos
  `ui-button`/`ui-panel`/`currency-pill`/`rarity-stars` em `core/ui/`; `docs/STYLE_GUIDE.md` escrito.
  Build limpo; Home renderiza repaginada sem erro de console (só 500s de backend offline).

**Objetivo.** Estabelecer a **linguagem visual premium** que todas as telas vão seguir. Hoje o
tema é plano: teal `#2dd4bf` usado pra tudo, painéis chapados, 44 linhas de `styles.css`. Alvo:
profundidade de gacha AAA (Wuthering Waves) — superfícies de vidro, hierarquia tipográfica forte,
cor com intenção (acento por raridade/elemento, não um teal genérico em tudo).

**Contexto técnico.**
- Global atual: `frontend/src/styles.css` (lê antes de reescrever).
- Classes globais em uso hoje (manter os nomes p/ não quebrar telas ainda não migradas):
  `.btn`, `.btn.secondary`, `.btn.gold`, `.panel`, `.pixel`. Pode redesenhá-las, não removê-las.
- `RARITY_COLORS` existe em `core/types.ts`.

**Tarefas:**
1. Reescrever `styles.css` como um **design-token system** em CSS custom properties no `:root`:
   - Paleta base (fundo em camadas: `--bg-0..--bg-3`, superfícies de vidro com `backdrop-filter`).
   - Acento por raridade (3★/4★/5★) e por elemento (7 elementos) como variáveis.
   - Escala tipográfica (importe **uma** fonte display + uma de UI; self-host ou Google Fonts).
     Tibia/gótico premium pede display com personalidade (ex: Cinzel/Marcellus p/ títulos) +
     sans limpa (ex: Inter/Sora) p/ corpo. Decida com olho de design, não default.
   - Tokens de espaçamento, raio, sombra/elevação, e **motion** (durations + easings nomeados).
2. Redesenhar os primitivos globais (`.btn`, `.panel`) no novo idioma + adicionar utilitários
   de vidro (`.glass`, `.glass-strong`), `.pill` (moeda), `.stars` (raridade).
3. Criar componentes standalone reutilizáveis em `frontend/src/app/core/ui/`:
   - `ui-button.ts` (variantes: primary/gold/ghost; estados loading/disabled).
   - `ui-panel.ts` (glass card com header opcional).
   - `currency-pill.ts` (ícone + valor + botão "+" opcional).
   - `rarity-stars.ts` (★ por raridade, cor do token).
4. Criar `docs/STYLE_GUIDE.md` curto (1 página): paleta, fontes, quando usar cada primitivo,
   regra de acento por elemento/raridade. É a referência que os prompts seguintes citam.

**Critério de aceite:** tokens definidos; primitivos renderizam; STYLE_GUIDE escrito; nenhuma tela
existente quebra (as classes globais antigas continuam válidas, só repaginadas).
**Verificação:** `npx ng build` limpo. Suba `ng serve` e confirme via preview que Home/Recruit
ainda renderizam (mesmo que "meio repaginadas") sem erro de console.

---

# PROMPT 2 — `KaeliArtService` + componente `<app-kaeli-idle>`  ⭐ seam de arte

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** `frontend-design` · **Depende de:** 1
- **[x] Concluído** — `core/kaeli-art.service.ts` (lê `manifest.json`; getters idles/wallpaper/
  bgLandscape/bgPortrait/banner/thumb → URL|null; `elementGradient`); `core/ui/kaeli-idle.ts`
  `<app-kaeli-idle>` com crossfade sem flash (base+fade, pré-carrega a próxima pose), fallback p/
  `app-outfit-preview` via catálogo, respeita `prefers-reduced-motion`, limpa timers no destroy.
  Validado no preview: Velvet roda idle-2→idle-3 com crossfade a 1.5s de teste; build limpo.

**Objetivo.** Criar a camada que serve a arte nova de personagem e o componente de **idle rotativo
com crossfade** (3 poses, troca a cada 7s, transição suave), com fallback pro sprite Tibia.

**Contexto técnico.**
- `WaifuDef` (`core/types.ts:66`) **não tem** campos de arte — não altere a interface do backend;
  o mapeamento arte↔id vive no frontend via `manifest.json` (Prompt 0) + service.
- Sprite fallback existente: `app-outfit-preview` (`core/outfit-preview.ts`), usado hoje na Home.
- Use signals. `use context7` se precisar confirmar API de signals/`@if`/`@for` do Angular 21.

**Tarefas:**
1. `frontend/src/app/core/kaeli-art.service.ts`:
   - Carrega `assets/kaelis/manifest.json`.
   - API: `idles(waifuId): string[]` (URLs; `[]` se sem arte), `wallpaper(id)`, `bgLandscape(id)`,
     `bgPortrait(id)`, `banner(id)`, `thumb(id)` — cada um devolve URL ou `null`.
   - `elementGradient(element): string` — devolve a URL do placeholder SVG por elemento (fallback).
2. `frontend/src/app/core/ui/kaeli-idle.ts` — componente standalone `<app-kaeli-idle>`:
   - Inputs: `waifuId`, `intervalMs` (default **7000**), `size`/fit.
   - Se `idles().length > 0`: duas `<img>` empilhadas, alterna opacity com crossfade
     (~800ms `ease`), `setInterval(intervalMs)`. A imagem anterior só some **depois** que a
     próxima entrou (sem flash branco). Loop circular 1→2→3→1.
   - Se vazio: renderiza `app-outfit-preview` (sprite) como fallback — passa o lookType/skin.
   - Respeitar `prefers-reduced-motion`: sem rotação, mostra `idle-1` estático.
   - `clearInterval` no destroy. Pré-carregar a próxima imagem antes do fade.
3. Não integrar nas páginas ainda (isso é Prompt 3 e 6) — só entregar service + componente +
   um uso de teste removível, se precisar validar.

**Critério de aceite:** service tipado; `<app-kaeli-idle waifuId="waifu:velvet">` faz a rotação 1→2→3
com crossfade suave a cada 7s; uma Kaeli sem arte mostra o sprite sem erro.
**Verificação:** `npx ng build` limpo. Validar a rotação via preview (`preview_screenshot` em 2
momentos diferentes deve mostrar poses diferentes) e console sem erros.

---

# PROMPT 3 — Home Hub redesign  ⭐ tela-vitrine

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** `frontend-design` · **Depende de:** 1, 2
- **[x] Concluído** — Home full-bleed: wallpaper da Kaeli fixada (`KaeliArtService.wallpaper` →
  fallback `bgLandscape`/gradiente do elemento + sprite quando sem arte) com scrim; identidade no
  canto inferior esquerdo (tag de elemento, `rarity-stars`, nome em Fraunces, título, desc);
  pin-picker com `thumb`/sprite; CTA do banner ativo ("DROP RATE UP" → /recruit); rail vertical de
  vidro à direita (Caçada/Kaelis/Recrutar/Mochila/Bestiário com subtítulos derivados do catálogo);
  contratos diários movidos p/ drawer glass com badge. Responsivo <900px: rail vira barra inferior,
  wallpaper `object-fit:cover`. Validado no preview (desktop 1366px: wallpaper Velvet + rail + drawer;
  fallback Ember/fogo; mobile bottom-bar). Build limpo.

**Objetivo.** Transformar a Home num hub full-bleed estilo gacha premium. Referência visual: o
mockup que o usuário esboçou (wallpaper da Kaeli fixada ocupando a tela, rail de navegação vertical
à direita com "Missões/Caçada/Kaelis/Recrutar...", cluster de moedas no topo, CTA do banner em
destaque embaixo à esquerda, nome da Kaeli em display grande).

**Contexto técnico.**
- Arquivo: `frontend/src/app/pages/home/home.ts` (217 linhas — leia inteiro antes).
- Dados já disponíveis no componente: `pinnedWaifu()`, `pinnedSkin()`, `owned()`, `dailies()`,
  `account()`, e ações `pick(id)` / `claim(id)`. **Reutilize a lógica; reescreva só o template+estilo.**
- `wallpaper` da Kaeli fixada vem de `KaeliArtService.wallpaper(id)` (Prompt 2); se `null`, usar
  `bgLandscape` ou o gradiente do elemento. A vitrine usa o **wallpaper** (não o idle) na Home.
- Use os primitivos do Prompt 1 (`ui-button`, `currency-pill`, `rarity-stars`) e tokens.

**Tarefas:**
1. Layout full-bleed: wallpaper da Kaeli fixada como fundo (com vinheta/scrim pra legibilidade).
2. Rail de navegação vertical à direita (glass), itens com subtítulo (ex: "Caçada · 3 masmorras").
   Os destinos reais hoje: Caçada=`/hunt`, Kaelis=`/kaelis`, Recrutar=`/recruit`, Mochila=`/backpack`,
   Bestiário=`/bestiary`. (Itens "Missões/Simulacro/Kaezan City" do mockup são aspiracionais — só
   inclua se houver rota; senão deixe fora ou desabilitado com tooltip "em breve".)
3. Nome da Kaeli em display grande embaixo à esquerda + estrelas + título + tag de elemento.
   Manter o `pin-picker` (trocar Kaeli destaque) num formato mais elegante (thumbs via `thumb`/sprite).
4. CTA do banner ativo em destaque (card "DROP RATE UP") linkando `/recruit`.
5. Contratos diários: mover pra um painel glass acessível (drawer/canto) — não pode poluir a vitrine.
6. Responsivo: <900px colapsa rail pra barra inferior; wallpaper recorta com `object-fit: cover`.

**Critério de aceite:** Home parece um hub de gacha premium, não um PowerPoint; wallpaper da Velvet
preenchendo, nav legível, performático. Trocar a Kaeli destaque atualiza o wallpaper.
**Verificação:** `npx ng build` limpo + `preview_screenshot` da Home com a Velvet fixada (compare
com o mockup do usuário) + console limpo + `preview_resize` mobile não quebra.

---

# PROMPT 4 — Recrutar / Banner redesign

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma (segue `STYLE_GUIDE.md` + padrão da Home) · **Depende de:** 1, 2, 3

**Objetivo.** Repaginar a tela de convocação no idioma premium. Referência: o mockup do banner
"Catedral de Cristal" (arte da Kaeli full-bleed à direita, rail de seleção de banner à esquerda,
título + descrição + pity, botões Convocar ×1 / ×10 no canto inferior direito, barra de garantia 5★).

**Contexto técnico.**
- Arquivo: `frontend/src/app/pages/recruit/recruit.ts` (168 linhas — leia inteiro).
- Lógica a reutilizar: `banners()`, `pullCost()`, `kaeros()`, `pity(id)`, `featuredWaifu(id)`,
  `pull(bannerId, count)`. **Não mexa na lógica de pull nem no service.** Só template+estilo.
- Arte do banner: `KaeliArtService.banner(featuredWaifuId)` (Prompt 2). Fallback: sprite + gradiente.
- **Siga o padrão visual já estabelecido nos Prompts 1 e 3** (tokens, glass, primitivos). Cite
  `docs/STYLE_GUIDE.md`. Esta tela deve parecer irmã da Home.
- O reveal de pull (overlay de cartas) **NÃO** é escopo deste prompt — é o Prompt 5. Mantenha o
  reveal atual funcionando; só não o redesenhe aqui.

**Tarefas:**
1. Banner em destaque full-bleed: arte da Kaeli à direita, área esquerda "respirável" pra texto/UI.
2. Rail de seleção de banners à esquerda (event/arma/padrão) — hoje só há os banners de `banners()`;
   renderize os que existem, com thumb.
3. Bloco de info: nome do banner, descrição, contador de pity (5★/80, 4★/10, garantido), barra de
   progresso "5★ garantido em N".
4. Botões Convocar ×1 / ×10 com custo em Kaeros, desabilitados sem saldo (use `ui-button`).
5. Painel "Taxas" + "4★ em destaque" reaproveitando dados existentes.
6. Responsivo.

**Critério de aceite:** tela de banner premium consistente com a Home; pulls continuam funcionando
(×1 e ×10), pity exibido corretamente.
**Verificação:** `npx ng build` limpo + `preview_screenshot` + testar um pull via preview (sem gastar
lógica real além do necessário) + console limpo.

---

# PROMPT 5 — Reveal de convocação (juice)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** `frontend-design` · **Depende de:** 4
- **[x] Concluído** — overlay de reveal reescrito em 2 fases: `charge` (orbe + raios girando cuja cor
  antecipa a maior raridade do lote — dourado p/ 5★, classe `.intense`) → `reveal` (cartas). x1 = carta
  grande única (nome em Fraunces, thumb da Kaeli ou sprite fallback); x10 = grid em cascata (1 carta/200ms)
  com a maior raridade ganhando aura/borda dourada (`.top`). Tags NOVA!/+shards/DESTAQUE. Botões "Pular
  animacao"/"Fechar" + clique no fundo (pula enquanto revela, fecha quando concluído). `prefers-reduced-motion`
  reforçado no JS (vai direto pro reveal completo) e no CSS. Timers limpos no `ngOnDestroy`. Validado no
  preview (orbe 5★ dourado, reveal x1 da Velvet, cascata x10 com Velvet em destaque); build limpo (CSS no
  budget após enxugar).

**Objetivo.** Elevar o momento de revelação do pull (hoje: cartas simples com fade). Adicionar
suspense e recompensa visual sem virar vídeo — animação CSS/Angular determinística.

**Contexto técnico.**
- Overlay de reveal vive em `recruit.ts` (a `@if (results())`). Lógica: `results()`, `revealed()`,
  `pull()` já incrementa `revealed` em intervalos. **Reutilize; reescreva a apresentação.**
- `RARITY_COLORS` por raridade. Arte: use `thumb` da Kaeli revelada quando houver (`KaeliArtService`),
  senão sprite.

**Tarefas:**
1. Sequência: tela escurece → feixe/orbe de luz com cor que **antecipa a raridade** (dourado = 5★)
   antes de revelar a carta → flip/burst da carta → tag NOVA!/shards/DESTAQUE.
2. ×10: revelar em cascata, com a maior raridade do lote ganhando destaque (skip com clique).
3. Respeitar `prefers-reduced-motion` (revelar direto, sem flashes).
4. Botão "pular animação" e "fechar".

**Critério de aceite:** reveal dramático e legível; ×1 e ×10 funcionam; skip e reduced-motion ok.
**Verificação:** `npx ng build` limpo + screenshots de um 5★ revelado + console limpo.

**Nota:** cutscene de invocação 5★ **em vídeo** (Remotion) é o Prompt 10, opcional e posterior.

---

# PROMPT 6 — Página Kaelis redesign + idle 7s  ⭐ onde o idle vive

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** `frontend-design` · **Depende de:** 1, 2
- **[x] Concluído** — Kaelis repaginada como "ateliê": grid de 3 colunas (rail de roster com
  `thumb`/sprite + `rarity-stars` · alcova de arte · dossiê de vidro). `<app-kaeli-idle [waifuId]
  [intervalMs]="7000">` integrado sobre `bgPortrait` (fallback: skin sprite via `app-outfit-preview`
  quando a Kaeli não tem arte autoral, preservando preview de skin) + floor-glow do elemento e
  drop-shadow p/ destacar a figura. Identidade (tag de elemento, estrelas, nome Fraunces, título,
  chips classe/arma) sobreposta na base. Dossiê com stat-ribbon (ATK/HP/Afinidade/Ascensão) +
  tabs sticky no idioma íris/aurum; as 5 abas (Perfil/Maestria/Equipamento/Informação/Skins)
  repaginadas com tokens preservando 100% da lógica (presentear, trocar skin, maestria, equipar,
  ascender). Responsivo <920px: roster vira faixa horizontal, alcova colapsa acima do dossiê.
  Build limpo (CSS desta tela é a maior do app → budget `anyComponentStyle` de erro elevado p/ 14kB
  no `angular.json`, warning mantido em 8kB). Validado no preview (desktop 3-col com idle da Velvet
  sobre a catedral; mobile).

**Objetivo.** Repaginar a página Kaelis e **integrar o `<app-kaeli-idle>`** — esta é a tela onde o
jogador passa tempo (ajustando set), então é o lugar certo do idle rotativo (7s, 3 poses, crossfade).

**Contexto técnico.**
- Arquivo: `frontend/src/app/pages/kaelis/kaelis.ts` (**998 linhas** — é grande; tem abas
  Perfil/Skins/Maestria/Equipamento). **Leia inteiro antes de tocar.** Preserve TODA a lógica de
  presentes, troca de skin, maestria e paperdoll de equipamento — só repagine apresentação e
  encaixe o idle.
- Fundo da Kaeli selecionada: `KaeliArtService.bgPortrait(id)` (`bg-portrait.jpg` da Velvet);
  fallback gradiente do elemento. A Velvet (`<app-kaeli-idle>`) fica **sobre** esse fundo.
- Use tokens/primitivos (Prompts 1) e cite `STYLE_GUIDE.md`. Consistente com Home/Recruit.

**Tarefas:**
1. Layout: coluna de arte (idle rotativo sobre `bg-portrait`) + painel de abas glass ao lado.
2. Integrar `<app-kaeli-idle [waifuId]="selected" [intervalMs]="7000">`.
3. Repaginar as 4 abas no novo idioma sem perder funcionalidade.
4. Roster/seleção de Kaeli usando `thumb`/sprite + `rarity-stars`.
5. Responsivo (arte colapsa acima do painel no mobile).

**Critério de aceite:** página premium; idle da Velvet roda 1→2→3 a cada 7s com crossfade;
todas as 4 abas funcionam (presentear, trocar skin, gastar maestria, equipar) exatamente como antes.
**Verificação:** `npx ng build` limpo + screenshots das abas + confirmar rotação do idle + testar
um fluxo de cada aba via preview + console limpo.

---

# PROMPT 7 — Shell / navegação + admin discreto

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma (segue `STYLE_GUIDE.md` + padrão da Home) · **Depende de:** 1, 3

**Objetivo.** Alinhar a top bar/navegação ao novo idioma e **tirar o Admin da nav principal**.

**Contexto técnico.**
- `frontend/src/app/shell/shell.ts` (83 linhas) — top bar com logo, nav, moedas, botão dev.
- `frontend/src/app/app.routes.ts` — rota `/admin` permanece existindo e funcional; só não fica
  na nav principal. **Admin não é redesenhado** (é ferramental).
- Se a Home (Prompt 3) já tem rail de navegação próprio, decida: ou a top bar global some na Home
  e aparece nas internas, ou vira uma barra fina consistente. Documente a escolha.

**Tarefas:**
1. Repaginar top bar (logo, moedas via `currency-pill`, nível) no novo idioma.
2. Mover `/admin` pra acesso discreto (ícone de engrenagem no canto, ou menu "⋯"), fora dos itens
   principais. Manter o botão dev de Kaeros como dev-only discreto.
3. Garantir consistência de nav entre telas (active state, etc.).

**Critério de aceite:** nav premium e coerente; admin acessível mas fora do fluxo principal.
**Verificação:** `npx ng build` limpo + navegar entre todas as telas via preview sem erro.

---

# PROMPT 8 — Telas secundárias (pass de tokens)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium · **Skill:** nenhuma (segue `STYLE_GUIDE.md`) · **Depende de:** 1

**Objetivo.** Aplicar o design system às telas restantes que ninguém vê "primeiro" mas que ainda
parecem PowerPoint: **hunt, mode, prerun, backpack, bestiary**.

**Contexto técnico.**
- Arquivos: `pages/hunt/*`, `pages/mode/*`, `pages/prerun/*`, `pages/backpack/*`, `pages/bestiary/*`.
- **Tarefa bounded:** trocar cores/painéis chapados pelos tokens e primitivos dos Prompts 1.
  **Não** redesenhar layout do zero — repaginar. Preserve toda a lógica.
- Cite `STYLE_GUIDE.md`. Faça uma tela, valide, repita (pode rodar uma tela por sub-execução).

**Critério de aceite:** as 5 telas usam tokens/primitivos; nenhuma lógica quebrada.
**Verificação:** `npx ng build` limpo + screenshot de cada tela + console limpo.

---

# PROMPT 9 — Polish, responsivo & verificação final

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium · **Skill:** `frontend-design` · **Depende de:** 3–8

**Objetivo.** Passada final de qualidade no conjunto.

**Tarefas:**
1. Auditar `prefers-reduced-motion` em todas as animações (idle, reveal, hovers).
2. Responsivo de ponta a ponta (mobile/tablet/desktop) — `preview_resize`.
3. Performance: imagens grandes (wallpaper/banner) com `loading`/decoding adequados; sem layout
   shift; idle não vaza `setInterval`.
4. Consistência de tokens (sem cor hardcoded sobrando das telas antigas — buscar `#2dd4bf` etc.).
5. Atualizar `README.md` (seção visível mudou) e marcar este remap como concluído.

**Critério de aceite:** experiência coesa, responsiva, sem regressão.
**Verificação:** `npx ng build` limpo + varredura de screenshots de todas as telas + grep por cores
hardcoded legadas retornando vazio nas telas migradas.

---

# PROMPT 10 — (OPCIONAL, posterior) Cutscene de invocação 5★ em vídeo

- **Modelo:** Claude Code Opus 4.8 · **Effort:** high · **Skill:** `remotion-best-practices` · **Depende de:** 5

**Objetivo.** Produzir um clipe cinematográfico (webm) de invocação 5★ da Velvet, tocado quando o
jogador puxa um 5★ destaque. **Só fazer depois que o reveal CSS (Prompt 5) estiver bom** — isto é
um upgrade de produção, não bloqueante.

**Contexto técnico.**
- Requer **FFmpeg** instalado (`ffmpeg -version`). Remotion = vídeo programático em React, **não**
  IA de vídeo. Matéria-prima: assets da Velvet (`idle-*`, `bg-*`, `thumb`).
- Projeto isolado em `tools/cinematics/` (Node/React) — **não** entra no bundle do Angular; a saída
  é um `.webm` copiado pra `frontend/public/assets/cinematics/velvet-5star.webm`.

**Tarefas:**
1. Scaffold do projeto Remotion em `tools/cinematics/`.
2. Composição de ~12–18s: build-up de luz roxa → reveal da Velvet (idle-1 sobre `bg-landscape`) →
   partículas → logo/nome. Áudio opcional.
3. Render → `.webm` → copiar pra `public/assets/cinematics/`.
4. (Integração no `recruit.ts` para tocar o clipe em 5★ destaque pode ser um sub-prompt à parte.)

**Critério de aceite:** `velvet-5star.webm` renderizado e reproduzível.
**Verificação:** abrir o webm; tamanho/duração razoáveis; estilo coerente com o jogo.

---

## Ordem recomendada de execução

```
0 → 1 → 2  (fundação: assets, design system, seam de arte)
3          (Home — define o teto de qualidade)
4 → 5      (Recrutar + reveal)
6          (Kaelis + idle)
7 → 8      (shell + telas secundárias)
9          (polish final)
10         (opcional, quando quiser produção de vídeo)
```

Pratos cheios (1, 3, 6) no **Opus 4.8 high**; conversões bounded (4, 7, 8) no **GPT-5.5 medium**.
