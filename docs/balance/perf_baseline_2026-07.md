# Perf baseline — Onda 1 (2026-07-06)

Fecha a Onda 1 do plano `docs/superpowers/plans/2026-07-06-wave1-performance-reliability.md`
(Task 7). Mede o estado pós Tasks 1–6 e aplica a régua de decisão de render (Canvas vs PixiJS).

## Ambiente

- **Backend:** build **Release** via `tools/run-backend.ps1` (`:5210`, ambiente Development —
  só a compilação muda). Instrumentação do Task 2 (`tick perf` a cada ~30s, `run created` por join).
- **Frontend:** `ng serve` (dev build, sem otimizações de prod — números de frame são um
  **teto**, não um piso). Overlay F3 do Task 5 lido via DOM a cada 2s.
- **Browser:** Chromium embutido (Claude Preview), display high-refresh (p50 de frame alterna
  entre os buckets de 4.2ms/240Hz e 8.3ms/120Hz — o orçamento de referência do spec é 16ms/60Hz).
- **Conta local:** Velvet (Necromancer) asc 6, autopilot ligado; tier máximo desbloqueado = **T5**.
- Runs de autopilot se auto-resolvem em **~40–55s** (T1 e T5) — relevante para o cenário 4.

## Números por cenário

| Métrica | 1 · T1 completa | 2 · T5 (pilha máxima) | 3 · 10s pós-join | 4 · background 12s* | Régua |
|---|---|---|---|---|---|
| frame p50 | 4.2–8.3 ms | 4.2–8.3 ms | 4.2–8.3 ms | 8.2 ms | — |
| frame p95 | 8.3–12.5 ms | 8.4–**12.6 ms** | 8.4 ms | 12.1 ms | ≤ 16 ms ✔ |
| draw p95 | 0.5–1.6 ms | 0.6–**2.7 ms** | 1.2–2.0 ms | 1.3 ms | ≤ 12 ms ✔ |
| long frames (>33ms) | 9 (5 no join) | 5 (3 no join) | 3–5 (todos no join) | +1 (frame de resume) | — |
| snapshot age em combate | 0–106 ms | 0–103 ms | 1–89 ms | 5 ms ao voltar | — |
| events ingeridos (+deduped) | 2 523 (+22 800) | 5 464 (+49 223) | — | 3 570→5 851 (+22 421) | 0 duplicado ✔ |
| tick perf p95 (backend) | 0.48–3.54 ms | 0.36–0.49 ms | — | — | ≤ 30 ms ✔ |
| tick perf max (backend) | 34.33 ms (1º tick, JIT) | ≤ 3.43 ms | — | — | — |
| run created | 13 ms (1ª run) | 1–3 ms | — | — | ≤ 300 ms ✔ |

\* Cenário 4 = suspensão do rAF mid-combate (ver método abaixo); valores em "resume+3s".

Notas de leitura:
- O max de tick de 34.33ms é o primeiro tick do processo (JIT warmup); depois que o ring
  descarta o warmup, o max observado fica em 0.90–3.43ms.
- `deduped >> ingested` é o esperado: com `EventReplayTicks = 10`, cada snapshot reenvia ~9 ticks
  de eventos já vistos e o cursor de seq os descarta — o contador mede o custo do seguro, não perda.

### Cenário 4 — método e caveat

A extensão de browser real não estava disponível na sessão, então o "tab em background" foi
emulado **suspendendo o rAF** (callbacks retidos e liberados depois — exatamente o que um tab
oculto faz com o render loop; a ingestão via SignalR + `effect()` continua nos dois casos).
Três suspensões mid-combate T5: **53s**, **45s** e **12s**.

- Durante a suspensão os eventos continuam sendo ingeridos (4 133→4 551 na de 53s;
  3 570→5 851 na de 12s) e a janela de reenvio é toda absorvida pelo dedup (zero FX duplicado).
- O retorno custa **1 long frame** (o frame gigante do resume) e o snapshot age volta a <10ms
  imediatamente; sem erros novos no console.
- **Caveat:** o pedido original de 60s de background com retorno em combate é impossível hoje —
  runs de autopilot acabam em <55s, então nas suspensões de 45–53s a run terminou em background
  (o cliente mostrou a tela de fim normalmente ao voltar). A validação "voltar com combate vivo"
  vem da suspensão de 12s.

## Veredito por sintoma original

| Sintoma | Estado | Evidência |
|---|---|---|
| Stutter de primeiro combate | **Resolvido** | Long frames concentrados no join (3–5); nenhum salto no primeiro FX de skill (T1: 5 long frames parados de t=0 a t=26); draw p95 ≤ 2.7ms mesmo no pico do T5. Preload dos atlases antes do join (Task 6) confirmado. |
| FX perdidos (snapshot coalescido/perdido) | **Resolvido** | Seq + janela de 10 ticks + dedup do cliente: ingestão contínua mesmo com rAF suspenso 53s; contador `deduped` absorve 100% dos reenvios; zero duplicata visual/sonora observável e zero erro de ingestão. |
| Medição inválida (backend Debug) | **Resolvido** | `tools/run-backend.ps1` (Release) + instrumentação permanente (`tick perf`, `run created`, overlay F3). |
| Hitch de geração de run | **Resolvido** | `run created` = 1–13ms (régua: 300ms). |

**Achado novo (fora do escopo da Onda 1):** `GameRenderer.drawShockwaves` lança
`IndexSizeError` (raio negativo, ~-0.04 a -0.92) quando o primeiro frame de um shockwave chega
com timestamp de rAF anterior ao `performance.now()` do ingest — o catch do loop segura, mas o
resto do draw daquele frame é abortado (~8 frames afetados por sessão). Fix pontual: clampar
`t = max(0, age)/SHOCKWAVE_MS`. Registrado como task separada; não muda a decisão abaixo.

## Decisão de render

Régua do plano: `draw p95 > 12ms` → sub-projeto de render; `≤ 12ms` com `frame p95 ≤ 16ms` →
renderer atual basta; `tick p95 > 30ms` ou `run created > 300ms` → investigação de engine antes.

- draw p95 **pico = 2.7ms** — 22% do limiar, com margem de ~4× até em dev build.
- frame p95 **máx = 12.6ms** ≤ 16ms.
- tick p95 **máx = 3.54ms** (warmup) / 0.49ms regime — 1–12% do limiar; run created ≤ 13ms.

**Decisão: o renderer Canvas 2D atual basta. Não abre sub-projeto de render (nem otimização
Canvas dirigida, nem migração PixiJS). Nenhuma investigação de engine necessária.**

Condição de reabertura: se a Onda 2 (mapas v2 — autotiling 47 casos, profundidade visual) ou
hordas mais densas empurrarem `draw p95` acima de **12ms** em cenário típico, reabrir com os
números do overlay F3 (permanente) anexados. Re-medir sempre com backend Release
(`tools/run-backend.ps1`).
