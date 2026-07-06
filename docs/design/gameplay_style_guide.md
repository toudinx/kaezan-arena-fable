# Gameplay Style Guide — "Reliquary Combat"

Guia de referência da identidade visual da **tela de gameplay** (`frontend/src/app/pages/game/game.ts`).
É a extensão do design system global ("Cathedral Ink + Aurum", ver `docs/STYLE_GUIDE.md` e
`frontend/src/styles.css`) para o contexto de combate. As outras telas devem propagar estes
princípios quando tiverem elementos de HUD/overlay sobre conteúdo vivo.

> Idioma: este doc é material de design (PT ok). Toda string visível ao jogador é em inglês.

---

## 1. Tese

O HUD não é um chrome genérico por cima da arena — é **o relicário trazido para o combate**.
A mesma linguagem da tela de Kaelis (vidro consagrado, luz de elemento, rosácea) vira instrumento
de jogo: janelas de catedral como slots de skill, a rosácea como ultimate, penumbra de catedral
emoldurando a arena. O que era "clone do Tibia" (slots quadrados cinza, HP verde, minimap preto,
emoji) foi substituído por peças que pertencem ao mundo das artes aprovadas das Kaelis
(catedral gótica, violeta profundo, filigrana dourada, luz de vela).

## 2. Assinatura

**A action bar é uma fileira de janelas de catedral.**

- **Skills 1–4**: tablets com topo em arco ogival
  (`border-radius: 46px 46px 12px 12px / 60px 60px 12px 12px`), vidro escuro
  (`--glass-bg-strong` + blur), tecla em numeral Fraunces na base.
- **Ultimate (R)**: a **rosácea** — botão circular com anel cônico dourado que enche com o
  gauge (`conic-gradient` + `mask` radial), miolo com raios de `repeating-conic-gradient`
  tingidos pelo elemento. Pronta = bloom dourado pulsante (`ultBloom`).
- **Potion (T) e Dash (Spc)**: "moedas votivas" — a mesma silhueta de arco, mais estreitas.
- **Echo cards** (card offer) repetem o arco no topo — a oferta é feita do mesmo material
  que a action bar.

Esse é o único lugar onde o design "gasta ousadia". Todo o resto é vidro quieto e disciplinado.

## 3. Paleta

Nenhuma cor nova foi inventada — tudo vem dos tokens de `styles.css`:

| Papel | Token | Uso no gameplay |
|---|---|---|
| Superfície | `--glass-bg-strong` + `--glass-blur` + `--glass-edge` | plaque, arcos, rosácea, cartouche, painéis |
| Fundo | `--bg-0` | letterbox/root, minimap |
| UI interativa | `--accent` (íris) | pills de sistema, helper panel, XP, reroll, foco |
| Recompensa | `--gold` / `--gold-bright` | **só**: gauge/bloom do ultimate, posture/Echo Break, gold, passiva carregada, save do helper, VICTORY, raridade echo |
| Perigo | `--danger` | HP baixo, boss bar, eyebrow BOSS, ban, DEFEAT |
| Elemento ativo | `--accent-el` (runtime) | ver §4 |
| Raridade | `--rarity-3/4/5` | cards common/rare/echo (echo usa `--gold`) |

**HP é luz de marfim, não verde-Tibia**: `linear-gradient(90deg, #f4eedb, #cbbd97)`.
Abaixo de 35% vira sangue (`--danger`) com pulso (`lowPulse`). Esses dois hex de marfim são a
única cor local da tela (vitalidade = luz de vela; não existe token pra isso no sistema global).

**Regra de ouro preservada**: frio (íris) = controle; quente (aurum) = recompensa/clímax.

## 4. Tinta de elemento (`--accent-el`)

O HUD inteiro se tinge com o elemento da stance ativa da Kaeli — mesmo padrão da página Kaelis:

- `game-root` recebe `[style.--accent-el]="accentEl()"` (signal computado da stance;
  fallback `--accent` para `support`/desconhecido).
- Derivados por `color-mix`: `--el-bright` (64% + white), `--el-glow` (40% alpha),
  `--el-haze` (16% alpha).
- Onde aparece: borda esquerda do plaque, nome da classe (Fraunces itálico em `--el-bright`),
  chip de stance, passiva assinatura, glow "ready" dos arcos (cada arco usa `--sk-el` do
  **próprio elemento da skill**), miolo da rosácea, e o **haze de altar** — brilho radial
  do elemento sob a skill bar no veil.
- Trocar de stance (Tab) re-tinge o HUD em tempo real. É feedback de gameplay, não decoração.

## 5. Tipografia

- **Fraunces (display)** — usada com restrição, só em "números/nomes de consequência":
  número de HP, teclas dos slots (1–4, R), nome da classe (itálico), nome do boss (itálico),
  título de overlay, numerais dos stats de run-end, VICTORY/DEFEAT (peso **900**).
  Pesos carregados: 400/600/900 — não usar 700 em Fraunces.
- **Sora (UI)** — todo o resto. Peso máximo **700** (800/900 não são carregados).
- **Eyebrows** (Sora 700, uppercase, tracking 0.14–0.24em, 8.5–10px) rotulam com verdade:
  `BOSS`, `LV 4`, labels dos stats, "THE ARENA FALLS SILENT". Nunca decoração.

## 6. Chrome de câmera (arena-veil)

Camada `pointer-events: none` (z-index 5, entre canvas e HUD) com 4 gradientes:

1. Vinheta radial (penumbra nas bordas — a arena vira diorama, não janela de client);
2. Faixa superior escura (assenta o HUD);
3. Faixa inferior escura (assenta a action bar);
4. **Altar glow**: radial `--el-haze` subindo por trás da skill bar.

## 7. Layout

```
┌────────────────────────────────────────────────────────┐
│ [plaque Kaeli]        ⌄[cartouche boss]⌄   [sys pills] │
│ [chips/passiva]                              (minimap)  │
│                                                          │
│                        ARENA                             │
│ [helper panel]                                           │
│            [1][2][3][4] (❀ rosácea) [T][»]               │
└──────────────── vinheta em toda a volta ────────────────┘
```

- **Plaque** (top-left): identidade + vitals. Classe em Fraunces itálico + chip de stance;
  HP grande; hairline de XP; sub-linha kills · gold · tier; equip stats.
- **Cartouche do boss** (top-center): pendão pendurado na borda superior
  (`border-radius: 0 0 18px 18px`, sem borda no topo). Eyebrow BOSS em carmim, nome em
  Fraunces itálico, HP carmim, posture em hairline dourada.
- **Sys cluster** (top-right): pills de texto uppercase — `LEAVE · BAG · AUTO · SOUND`.
  **Nunca emoji como iconografia.**
- **Minimap** (right): "espelho de obsidiana" — cantos `--r-lg`, ring `--line-strong`,
  halo escuro de 4px. Não é o quadrado cru do automap.
- **Cooldown**: varredura cônica escura que recua no sentido horário
  (`conic-gradient` com `--cd` = fração restante, bind `[style.--cd]`) — nunca a
  "cortina subindo" de MMO.

## 8. Linguagem de motion

Tokens globais (`--dur-fast` 120ms · `--dur` 220ms · `--ease-out`). Regras:

- **Um clímax só**: o bloom dourado da rosácea pronta (`ultBloom`, 1.6s alternate).
  Nada mais compete com ele.
- Pulsos de estado (HP baixo, posture alta, Echo Break) são funcionais e locais.
- Barras: `width` com transição 120–160ms — dados primeiro, suavidade depois.
- Hovers: translateY(-2px/-4px) + troca de borda; sem scale.
- `prefers-reduced-motion` já é respeitado globalmente por `styles.css`.

## 9. Copy do HUD

- Inglês, sentence case fora dos eyebrows; verbos ativos ("Play again", "Back to Hunt").
- Momentos têm voz de mundo, uma linha, sem melodrama: "The arena falls silent" /
  "The echo fades" / "The dungeon offers" / "Shaping the dungeon…".
- Condições usam códigos curtos (PSN, BRN…) em chips tingidos pelo elemento da condição.

## 10. O que NÃO fazer (foi o que lia como Tibia)

- Slots quadrados cinza com borda 2px e cooldown-cortina.
- HP verde→verde-escuro; XP ciano; boss laranja em caixa cinza.
- Teal `#2dd4bf` como acento universal (teal agora mora só em `--el-energy`).
- Minimap quadrado preto cru.
- Emoji como ícone de botão/moeda (🎒🔊🤖🪙👑📜⚡).
- Font única em 10–13px bold pra tudo, sem display face.
- Painéis `#15151f`/`#2c2c3e` opacos sem vidro nem crystal edge.

## 11. Propagação para outras telas

Ao migrar outra tela para esta linguagem:

1. Tokens e primitivos de `styles.css` primeiro; cor local só com justificativa (como o marfim do HP).
2. Se a tela tem uma Kaeli/elemento em contexto, tinja com `--accent-el` (padrão §4).
3. O arco de catedral é a forma de "slot/carta de ação"; a rosácea é reservada a clímax
   (ultimate, summon, reveal).
4. Emoji → texto eyebrow ou SVG próprio.
5. Fraunces só em nomes/números de consequência; Sora pro resto; pesos disponíveis (ver §5).
6. Overlay = `rgba(7,7,13,0.74)` + blur 10px + eyebrow + título Fraunces.

Fora do escopo desta fase (follow-ups conhecidos): nameplates/damage numbers do canvas
(`core/renderer.ts`) ainda usam a linguagem antiga; um passe futuro deve alinhá-los
(nameplates em Sora, números de dano tingidos por elemento).
