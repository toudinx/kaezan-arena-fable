# STYLE_GUIDE — Kaezan Arena Fable (frontend)

Contrato visual compartilhado entre os prompts do `FRONTEND_REMAP.md`. Toda tela
nova ou migrada segue isto. Fonte da verdade dos valores: `frontend/src/styles.css`.

Direção: **"Cathedral Ink + Aurum"** — gacha premium (alvo Wuthering Waves).
Superfícies de vidro com aresta de luz ("crystal edge"), tipografia editorial-fantasy,
e **cor com intenção** (não um único teal em tudo).

---

## Paleta

**Superfícies (ink em camadas — nunca preto puro):**
`--bg-0 #07070d` · `--bg-1 #0c0c15` (fundo do app) · `--bg-2 #13131f` · `--bg-3 #1b1b2a` (painel sólido) · `--bg-4 #24243a` (hover).

**Vidro:** `--glass-bg` / `--glass-bg-strong` + `backdrop-filter: blur(var(--glass-blur))`.
Sempre aplicar `--glass-edge` (hairline de luz no topo) — é a assinatura.

**Acento duplo — a regra mais importante:**
- **Íris `--accent #7b6bf2`** → toda a UI interativa (botões primários, links, foco, seleção, active state).
- **Aurum `--gold #e8a93c`** → reservado a **recompensa/premium**: 5★, moeda Kaeros, CTA de banner, "garantido".
  Não use ouro como cor de UI genérica. Cool = controle, quente = recompensa.

**Raridade** (espelha `RARITY_COLORS` em `core/types.ts` — não divergir):
`--rarity-3 #5ba8d4` · `--rarity-4 #a06bd6` · `--rarity-5 #e8a93c`.

**Elementos (7):** `--el-physical`, `--el-fire`, `--el-ice`, `--el-energy` (teal mora aqui agora),
`--el-earth`, `--el-death`, `--el-holy`. Use a cor do elemento da Kaeli/skill como acento contextual
(tag de elemento, gradiente de fallback), nunca como cor primária de ação.

**Texto:** `--text` / `--text-dim` / `--text-mute` / `--text-faint`.
**Linhas:** `--line` / `--line-strong`.

---

## Tipografia

- **Display — `Fraunces`** (`--font-display`): nomes de Kaeli, títulos de tela, números de herói.
  Com **restrição** — é o acento, não o corpo. Headings já usam por padrão (`h1..h4`).
- **UI — `Sora`** (`--font-ui`): corpo, labels, botões, dados. `tabular-nums` ligado no `body`
  (contadores de pity/moeda alinham).

Escala: `--fs-display` (clamp) › `--fs-h1` › `--fs-h2` › `--fs-h3` › `--fs-body` › `--fs-sm` › `--fs-xs`.
Overline/eyebrow: classe `.eyebrow` (uppercase, tracking `--tracking-eyebrow`) — use para rotular
seções com algo verdadeiro (ex: "BANNER ATIVO"), não como enfeite.

---

## Primitivos (use-os; não reinvente)

Componentes standalone em `frontend/src/app/core/ui/`:

| Tag | Uso | Inputs principais |
|---|---|---|
| `<ui-button>` | Ações | `variant="primary\|gold\|ghost"`, `[loading]`, `[disabled]`, `(act)` |
| `<ui-panel>` | Cartão de vidro | `header`, `eyebrow`, `[solid]`; slot `[actions]` |
| `<currency-pill>` | Moeda | `icon`, `[value]`, `tone="gold"`, `[plus]`, `(add)` |
| `<rarity-stars>` | Raridade | `[rarity]`, `[size]` |

**Variante de botão:** `primary` (íris) = ação principal de UI; `gold` = ação de recompensa
(Convocar, Resgatar); `ghost` = ação secundária/cancelar.

**Classes globais** (telas ainda não migradas continuam válidas, só repaginadas):
`.btn` / `.btn.secondary` / `.btn.gold` · `.panel` · utilitários `.glass` / `.glass-strong` /
`.pill` / `.stars` / `.scrim` / `.eyebrow` / `.pixel`.

---

## Espaço, raio, sombra, motion

- **Espaço:** `--sp-1..8` (base 4px). **Raio:** `--r-sm..xl` + `--r-full`.
- **Elevação:** `--sh-1..3` (tintada) + `--sh-accent` / `--sh-gold` para hover de ação.
- **Motion:** durações `--dur-fast/--dur/--dur-slow`; easings `--ease-out` (padrão),
  `--ease-in-out`, `--ease-spring` (pop/reveal). Todo `transition`/`animation` referencia tokens.
  `prefers-reduced-motion` já é cortado globalmente — não confie só nisso em animações de destaque,
  reforce no componente.

---

## Regras de acento (resumo)

1. Ação de UI → íris. Recompensa/premium → aurum. Nunca troque os papéis.
2. Raridade sempre pelos tokens `--rarity-N`; 5★ pode ganhar glow dourado.
3. Elemento da Kaeli colore tags/fundos de fallback (`assets/kaelis/_placeholder/<el>.svg`),
   nunca a cor primária de botão.
4. Superfície de destaque = vidro + crystal edge. Fundo chapado só em telas utilitárias (admin).
