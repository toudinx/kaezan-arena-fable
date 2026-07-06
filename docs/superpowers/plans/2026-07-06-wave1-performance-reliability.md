# Wave 1 — Performance & FX Reliability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Spec:** `docs/superpowers/specs/2026-07-06-kaezan-idle-evolution-design.md` (Onda 1; Ondas 2-4 como milestones no fim deste arquivo).

**Goal:** Eliminar FX perdidos, stutter de primeiro-combate e medição inválida: eventos ganham seq + janela de reenvio, o cliente deduplica, o backend roda em Release com instrumentação permanente, e um baseline medido decide o próximo passo de render.

**Architecture:** Backend autoritativo intocado na simulação — o `EventLog` (seq monotônico + janela dos últimos N ticks) é *saída*, não estado, então o determinismo/replay não muda. No cliente, dedup por seq no `GameRenderer.setSnapshot` torna a entrega de FX imune à coalescência do `effect()` do Angular e a snapshots perdidos. Instrumentação (percentis de tick no backend, overlay F3 no cliente) vira ferramenta permanente e alimenta o gate de decisão final (otimizar Canvas vs. sub-plano PixiJS).

**Tech Stack:** ASP.NET Core 8 (net8.0, SignalR), Angular 21 (signals, canvas 2D), xUnit (novo projeto de teste backend), Vitest (frontend).

## Global Constraints

- **Determinismo do engine:** dentro do tick, apenas o `Rng` da run. Proibido `Random`, `DateTime.Now`, `Guid.NewGuid()`, iteração de coleção sem ordem estável. (`Stopwatch`/`ILogger` FORA do `GameWorld` são permitidos — medição não é simulação.)
- **Gate de engine:** qualquer task que toque `Engine/` termina com `dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays` verde.
- **Constantes de simulação em `Domain/GameConfig.cs`** — nunca hardcode.
- **Idioma:** todo código, comentário e string visível ao jogador em **inglês**.
- **DTO mudou no backend → `frontend/src/app/core/types.ts` atualizado no mesmo task.**
- **Build limpo ao concluir cada task:** `dotnet build` (em `backend/src/KaezanArenaFable.Api`) e, se tocou frontend, `npx ng build` (em `frontend/`).
- IDs estáveis (`waifu:*`, `card:*`, `banner:*`, `monster:*`) nunca renomeados.
- Frontend nunca simula gameplay; só interpola e renderiza snapshots.

## Modelos & quando usar

> Convenção das Ondas 1-4: cada task declara **Modelo · Effort** no topo. O Fable 5 é caro —
> só entra onde Opus/Codex não bastam; nunca para executar código que o plano já escreveu.

| Modelo | Papel | Quando usar |
|---|---|---|
| **GPT-5.5 (Codex)** | Executor | Task bem-especificada com código dado no plano, 1-3 arquivos, TDD mecânico, UI com template pronto |
| **Claude Code Opus 4.8** | Integrador | Toca `Engine/`/gerador com gate replay/golden, ou exige adaptar o plano aos nomes/padrões reais dos arquivos |
| **Claude Fable 5** | Risco cross-cutting | Concorrência + estado compartilhado + interação entre ondas; decisão arquitetural que trava/abre sub-projeto |

| Task | Modelo | Effort |
|---|---|---|
| 1 — Release run script | Codex | low |
| 2 — Backend perf instrumentation | Codex | medium |
| 3 — EventLog (engine) | Opus 4.8 | medium |
| 4 — Client event dedup | Codex | medium |
| 5 — Debug overlay F3 | Codex | medium |
| 6 — Await atlas preload | Codex | low |
| 7 — Baseline + decisão de render | **Fable 5** | high |

---

### Task 1: Release run script (fix the measurement environment)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** low

O backend costuma rodar como exe **Debug** — toda medição feita assim é inválida. Este task cria o fluxo canônico de build+run em Release e o documenta.

**Files:**
- Create: `tools/run-backend.ps1`
- Modify: `README.md` (seção "Como rodar")

**Interfaces:**
- Produces: script `tools/run-backend.ps1` (parâmetro `-NoBuild`); todo task seguinte que precise do backend vivo usa este script.

- [ ] **Step 1: Write the script**

```powershell
# tools/run-backend.ps1 - stop, build (Release) and run the API on :5210.
# Use -NoBuild to just restart the last Release build.
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$api = Join-Path $PSScriptRoot '..\backend\src\KaezanArenaFable.Api'

Get-Process -Name 'KaezanArenaFable.Api' -ErrorAction SilentlyContinue | Stop-Process -Force

if (-not $NoBuild) {
    dotnet build $api -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$env:ASPNETCORE_URLS = 'http://localhost:5210'
& (Join-Path $api 'bin\Release\net8.0\KaezanArenaFable.Api.exe')
```

Nota: **não** setar `ASPNETCORE_ENVIRONMENT=Production` — o ambiente continua Development (seed de conta local, erros detalhados); só a compilação muda para Release.

- [ ] **Step 2: Verify it works**

Run: `powershell -File tools/run-backend.ps1` (deixe rodando) e, em outro shell:
`curl.exe -s -o NUL -w "%{http_code}" http://localhost:5210/api/v1/catalog`
Expected: `200` (ou outro endpoint existente de `MetaEndpoints` retornando 2xx). Pare o processo depois.

- [ ] **Step 3: Document in README**

Na seção "Como rodar" do `README.md`, substituir o passo do backend por:

```markdown
# 1. Backend (porta 5210) — sempre em Release; medição em Debug é inválida
powershell -File tools/run-backend.ps1        # build + run
powershell -File tools/run-backend.ps1 -NoBuild   # só restart
```

(mantendo o `dotnet run` documentado como alternativa de debug).

- [ ] **Step 4: Commit**

```bash
git add tools/run-backend.ps1 README.md
git commit -m "chore: canonical Release build+run script for the backend"
```

---

### Task 2: Backend perf instrumentation (tick percentiles + run-creation time)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium

**Files:**
- Create: `backend/tests/KaezanArenaFable.Api.Tests/KaezanArenaFable.Api.Tests.csproj` (via `dotnet new xunit`)
- Create: `backend/src/KaezanArenaFable.Api/Engine/PerfStats.cs`
- Create: `backend/tests/KaezanArenaFable.Api.Tests/PerfStatsTests.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Engine/RunManager.cs` (medir `World.Tick()`, log periódico)
- Modify: `backend/src/KaezanArenaFable.Api/Hubs/GameHub.cs:79-82` (cronometrar criação do `GameWorld`)

**Interfaces:**
- Produces: `PerfStats` — `void Add(double ms)`, `double Percentile(double p)`, `double Max()`, `int Count`. Logs `tick perf: p50=... p95=... max=...` (a cada ~30s) e `run created in ...ms` — o Task 7 lê esses logs para o baseline.

- [ ] **Step 1: Create the test project**

```bash
cd backend
dotnet new xunit -o tests/KaezanArenaFable.Api.Tests
dotnet add tests/KaezanArenaFable.Api.Tests reference src/KaezanArenaFable.Api/KaezanArenaFable.Api.csproj
```

- [ ] **Step 2: Write the failing tests**

`backend/tests/KaezanArenaFable.Api.Tests/PerfStatsTests.cs`:

```csharp
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

public class PerfStatsTests
{
    [Fact]
    public void PercentileOfKnownSamples()
    {
        var stats = new PerfStats();
        for (var i = 1; i <= 100; i++) stats.Add(i);
        Assert.Equal(50, stats.Percentile(50));
        Assert.Equal(95, stats.Percentile(95));
        Assert.Equal(100, stats.Max());
        Assert.Equal(100, stats.Count);
    }

    [Fact]
    public void RingWrapsAtCapacity()
    {
        var stats = new PerfStats(capacity: 10);
        for (var i = 0; i < 25; i++) stats.Add(i);
        Assert.Equal(10, stats.Count);
        Assert.Equal(24, stats.Max()); // only the last 10 samples (15..24) remain
        Assert.True(stats.Percentile(50) >= 15);
    }

    [Fact]
    public void EmptyStatsReadZero()
    {
        var stats = new PerfStats();
        Assert.Equal(0, stats.Percentile(95));
        Assert.Equal(0, stats.Max());
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests`
Expected: FAIL — `PerfStats` does not exist.

- [ ] **Step 4: Implement PerfStats**

`backend/src/KaezanArenaFable.Api/Engine/PerfStats.cs`:

```csharp
namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Fixed-size ring of duration samples with percentile readout.
/// Operational instrumentation only — never used by the simulation.
/// </summary>
public sealed class PerfStats(int capacity = 600)
{
    private readonly double[] _samples = new double[capacity];
    private int _count;
    private int _next;

    public int Count => _count;

    public void Add(double ms)
    {
        _samples[_next] = ms;
        _next = (_next + 1) % _samples.Length;
        if (_count < _samples.Length) _count++;
    }

    public double Percentile(double p)
    {
        if (_count == 0) return 0;
        var sorted = _samples.Take(_count).OrderBy(x => x).ToArray();
        var rank = (int)Math.Ceiling(p / 100.0 * _count) - 1;
        return sorted[Math.Clamp(rank, 0, _count - 1)];
    }

    public double Max() => _count == 0 ? 0 : _samples.Take(_count).Max();
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests`
Expected: PASS (3 tests).

- [ ] **Step 6: Wire into RunManager**

Em `RunManager` (campos da classe):

```csharp
private readonly PerfStats _tickPerf = new();
private long _perfLogCounter;
```

No `ExecuteAsync`, envolver o tick (linha ~162, dentro do `lock (run)`):

```csharp
var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
(snapshot, map) = run.World.Tick();
_tickPerf.Add(System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
```

E logo após o `ExpireOrphans()` no topo do loop do timer:

```csharp
if (++_perfLogCounter % 300 == 0 && _tickPerf.Count > 0)
    logger.LogInformation(
        "tick perf: p50={P50:F2}ms p95={P95:F2}ms max={Max:F2}ms ({Count} samples)",
        _tickPerf.Percentile(50), _tickPerf.Percentile(95), _tickPerf.Max(), _tickPerf.Count);
```

- [ ] **Step 7: Time run creation in GameHub**

Adicionar `ILogger<GameHub> logger` ao primary constructor do `GameHub` e envolver a criação (linhas 79-82):

```csharp
var creationStart = System.Diagnostics.Stopwatch.GetTimestamp();
var world = new GameWorld(
    runSeed, tierDef, waifu, ascension, data, monsters, bestiary, equipmentStats, kaeliLoadout, items,
    helperProfile, content.RoleTunings, mode, biome);
logger.LogInformation("run created in {Ms:F0}ms (tier {Tier}, seed {Seed})",
    System.Diagnostics.Stopwatch.GetElapsedTime(creationStart).TotalMilliseconds, tierDef.Tier, runSeed);
runs.StartRun(Context.ConnectionId, world);
```

- [ ] **Step 8: Build + verify logs appear**

Run: `dotnet build backend/src/KaezanArenaFable.Api` → sem erros.
Run: `powershell -File tools/run-backend.ps1`, iniciar uma run T1 pelo frontend, aguardar ~30s.
Expected: log `run created in ...ms` na entrada e `tick perf: ...` periódico.

- [ ] **Step 9: Commit**

```bash
git add backend/tests backend/src/KaezanArenaFable.Api/Engine/PerfStats.cs backend/src/KaezanArenaFable.Api/Engine/RunManager.cs backend/src/KaezanArenaFable.Api/Hubs/GameHub.cs
git commit -m "feat(perf): tick percentile logging and run-creation timing"
```

---

### Task 3: EventLog — seq monotônico + janela de reenvio (backend)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium — toca `GameWorld` (5,1k linhas) e termina no gate `--replay-check`

Hoje `_events` é limpo a cada tick e viaja uma única vez no snapshot ([GameWorld.cs:786](../../backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs), 5442, 5644); snapshot coalescido no cliente = FX perdido. O `EventLog` carimba cada evento com `Seq` e mantém a janela dos últimos `GameConfig.EventReplayTicks` ticks no snapshot. **Neste task a janela fica em 1 tick** (comportamento idêntico ao atual) — o Task 4 liga a janela real junto com o dedup do cliente, senão o cliente atual re-ingere FX duplicado.

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Engine/EventLog.cs`
- Create: `backend/tests/KaezanArenaFable.Api.Tests/EventLogTests.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Engine/GameDtos.cs:111-113` (campo `Seq`)
- Modify: `backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs:252,786,5442,5644`
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (nova constante)

**Interfaces:**
- Produces: `EventDto.Seq` (`long`, monotônico por run, ordem de emissão); `EventLog` — `EventDto Add(long tick, EventDto ev)`, `void Trim(long tick)`, `List<EventDto> Snapshot()`; `GameConfig.EventReplayTicks` (`int`, = 1 neste task). O Task 4 consome `Seq` no cliente.

- [ ] **Step 1: Write the failing tests**

`backend/tests/KaezanArenaFable.Api.Tests/EventLogTests.cs`:

```csharp
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

public class EventLogTests
{
    private static EventDto Ev(string kind) => new(kind, 0, 0, 0, 0, 0, "", 0, false);

    [Fact]
    public void StampsMonotonicSeqInEmissionOrder()
    {
        var log = new EventLog(replayTicks: 10);
        var a = log.Add(1, Ev("hit"));
        var b = log.Add(1, Ev("death"));
        var c = log.Add(2, Ev("hit"));
        Assert.Equal(0, a.Seq);
        Assert.Equal(1, b.Seq);
        Assert.Equal(2, c.Seq);
    }

    [Fact]
    public void ResendsEventsInsideTheWindow()
    {
        var log = new EventLog(replayTicks: 3);
        log.Add(10, Ev("hit"));
        log.Trim(12); // tick 10 still inside a 3-tick window at tick 12
        Assert.Single(log.Snapshot());
    }

    [Fact]
    public void DropsEventsOlderThanTheWindow()
    {
        var log = new EventLog(replayTicks: 3);
        log.Add(10, Ev("hit"));
        log.Add(12, Ev("death"));
        log.Trim(13); // tick 10 falls out (13 - 3 = 10 → tick <= 10 dropped)
        var window = log.Snapshot();
        Assert.Single(window);
        Assert.Equal("death", window[0].Kind);
    }

    [Fact]
    public void WindowOfOneTickBehavesLikeThePreTaskClearPerTick()
    {
        var log = new EventLog(replayTicks: 1);
        log.Add(5, Ev("hit"));
        log.Trim(6);
        Assert.Empty(log.Snapshot());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter EventLogTests`
Expected: FAIL — `EventLog` does not exist / `EventDto` has no `Seq`.

- [ ] **Step 3: Implement**

Em `GameDtos.cs`, `EventDto` ganha `Seq` com default (mantém call sites e BalanceSim compilando):

```csharp
public sealed record EventDto(
    string Kind, int X, int Y, int ToX, int ToY, int Value,
    string Text, int ActorId, bool Crit, long Seq = 0);
```

`backend/src/KaezanArenaFable.Api/Engine/EventLog.cs`:

```csharp
namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Monotonic event log with a replay window: snapshots re-send the last
/// <c>replayTicks</c> ticks of events so a dropped/coalesced snapshot never
/// loses FX. The client dedups by <see cref="EventDto.Seq"/>. Events are
/// engine *output*, not state — this never affects determinism.
/// </summary>
public sealed class EventLog(int replayTicks)
{
    private readonly Queue<(long Tick, EventDto Ev)> _window = new();
    private long _nextSeq;

    public EventDto Add(long tick, EventDto ev)
    {
        var stamped = ev with { Seq = _nextSeq++ };
        _window.Enqueue((tick, stamped));
        return stamped;
    }

    /// <summary>Drop events that fell out of the replay window at the given tick.</summary>
    public void Trim(long tick)
    {
        while (_window.Count > 0 && _window.Peek().Tick <= tick - replayTicks)
            _window.Dequeue();
    }

    public List<EventDto> Snapshot() => _window.Select(e => e.Ev).ToList();
}
```

Em `GameConfig.cs` (perto das constantes de tick/snapshot):

```csharp
/// <summary>How many ticks of already-sent events each snapshot re-sends so a
/// dropped/coalesced snapshot never loses FX (client dedups by EventDto.Seq).</summary>
public const int EventReplayTicks = 1; // raised to 10 together with the client dedup
```

Em `GameWorld.cs`:
- Linha 252: `private readonly List<EventDto> _events = [];` → `private readonly EventLog _events = new(GameConfig.EventReplayTicks);`
- Linha 786: `_events.Clear();` → `_events.Trim(TickCount);`
- Linha 5442: `_events.Add(new EventDto(kind, x, y, toX, toY, value, text, actorId, crit));` → `_events.Add(TickCount, new EventDto(kind, x, y, toX, toY, value, text, actorId, crit));`
- Linha 5644: `_events.ToList()` → `_events.Snapshot()`

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests`
Expected: PASS (todos, incluindo PerfStats do Task 2).

- [ ] **Step 5: Build + engine gate (replay-check)**

Run: `dotnet build backend/src/KaezanArenaFable.Api` → sem erros.
Run: `dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays`
Expected: todos os replays existentes verdes (eventos são saída, não estado — hash não muda). Se divergir, PARE: o hash de replay está incluindo eventos e o design precisa ser revisto antes de seguir.

- [ ] **Step 6: Commit**

```bash
git add backend/src/KaezanArenaFable.Api backend/tests
git commit -m "feat(engine): stamp events with monotonic seq and replay window (window=1, no behavior change)"
```

---

### Task 4: Client event dedup + ligar a janela de reenvio

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium

Duas causas de FX perdido no cliente: o `effect()` do Angular coalesce snapshots ([game.ts:885](../../frontend/src/app/pages/game/game.ts)) e mensagens podem se perder. Com o dedup por `seq`, ligar a janela de 10 ticks (1s) do backend fecha as duas.

**Files:**
- Create: `frontend/src/app/core/event-seq.ts`
- Create: `frontend/src/app/core/event-seq.spec.ts`
- Modify: `frontend/src/app/core/types.ts:719-729` (`seq` no `EventDto`)
- Modify: `frontend/src/app/core/renderer.ts:252-281` (`setSnapshot` deduplica; contadores p/ overlay)
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs` (`EventReplayTicks` 1 → 10)

**Interfaces:**
- Consumes: `EventDto.Seq` do Task 3 (`seq: number` no JSON camelCase do SignalR).
- Produces: `takeNewEvents(events: EventDto[], lastSeq: number): { fresh: EventDto[]; lastSeq: number }`; contadores públicos no `GameRenderer`: `eventsIngested: number`, `eventsDeduped: number` (Task 5 lê).

- [ ] **Step 1: Write the failing test**

`frontend/src/app/core/event-seq.spec.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { takeNewEvents } from './event-seq';
import type { EventDto } from './types';

const ev = (seq: number): EventDto =>
  ({ kind: 'hit', x: 0, y: 0, toX: 0, toY: 0, value: 0, text: '', actorId: 0, crit: false, seq });

describe('takeNewEvents', () => {
  it('passes everything through on first snapshot', () => {
    const r = takeNewEvents([ev(0), ev(1), ev(2)], -1);
    expect(r.fresh.map((e) => e.seq)).toEqual([0, 1, 2]);
    expect(r.lastSeq).toBe(2);
  });

  it('drops events already ingested (replay window overlap)', () => {
    const r = takeNewEvents([ev(1), ev(2), ev(3)], 2);
    expect(r.fresh.map((e) => e.seq)).toEqual([3]);
    expect(r.lastSeq).toBe(3);
  });

  it('keeps the cursor when nothing is new', () => {
    const r = takeNewEvents([ev(1), ev(2)], 5);
    expect(r.fresh).toEqual([]);
    expect(r.lastSeq).toBe(5);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx vitest run src/app/core/event-seq.spec.ts`
Expected: FAIL — `./event-seq` not found.

- [ ] **Step 3: Implement helper + types**

Em `types.ts`, adicionar ao `EventDto`: `seq: number;`

`frontend/src/app/core/event-seq.ts`:

```typescript
import type { EventDto } from './types';

/**
 * Filter out events the renderer already ingested. Snapshots re-send a short
 * replay window of events (dedup by monotonic seq), so a dropped or coalesced
 * snapshot no longer loses FX.
 */
export function takeNewEvents(
  events: EventDto[],
  lastSeq: number,
): { fresh: EventDto[]; lastSeq: number } {
  let cursor = lastSeq;
  const fresh: EventDto[] = [];
  for (const ev of events) {
    if (ev.seq > cursor) {
      fresh.push(ev);
      cursor = ev.seq;
    }
  }
  return { fresh, lastSeq: cursor };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx vitest run src/app/core/event-seq.spec.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Wire into the renderer**

Em `renderer.ts`, campos novos na classe `GameRenderer`:

```typescript
private lastEventSeq = -1;
eventsIngested = 0; // debug overlay counters (Task 5)
eventsDeduped = 0;
```

No `setSnapshot` (linha ~280), substituir `for (const ev of snap.events) this.ingest(ev, nowPerf);` por:

```typescript
// A new run restarts seq at 0 (its tick also restarts): drop the stale cursor.
if (previous && snap.tick < previous.tick) this.lastEventSeq = -1;
const { fresh, lastSeq } = takeNewEvents(snap.events, this.lastEventSeq);
this.lastEventSeq = lastSeq;
this.eventsIngested += fresh.length;
this.eventsDeduped += snap.events.length - fresh.length;
for (const ev of fresh) this.ingest(ev, nowPerf);
```

(com `import { takeNewEvents } from './event-seq';` no topo). **Não** resetar `lastEventSeq` no `setMap` — troca de andar mantém a run e o seq.

- [ ] **Step 6: Turn the replay window on (backend)**

Em `GameConfig.cs`: `EventReplayTicks = 1` → `EventReplayTicks = 10` (1s de janela) e remover o sufixo do comentário "raised to 10 together with the client dedup".

- [ ] **Step 7: Build both sides**

Run: `dotnet build backend/src/KaezanArenaFable.Api` e `cd frontend && npx ng build`
Expected: ambos sem erros.

- [ ] **Step 8: Manual FX verification**

Com backend (script do Task 1) + `npm start`: jogar uma run T1 e confirmar (a) FX de skill aparecem consistentemente, (b) nenhum FX duplicado (dano dobrado na tela, som repetido). Depois: mudar de aba por ~10s no meio do combate, voltar, e confirmar que os FX continuam saindo normalmente.
Expected: FX íntegros nos dois cenários.

- [ ] **Step 9: Commit**

```bash
git add frontend/src/app/core backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs
git commit -m "feat(fx): client event dedup by seq + enable 10-tick replay window"
```

---

### Task 5: Client debug overlay (F3)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium

**Files:**
- Create: `frontend/src/app/core/perf-ring.ts`
- Create: `frontend/src/app/core/perf-ring.spec.ts`
- Modify: `frontend/src/app/pages/game/game.ts` (medição no rAF loop ~linhas 945-957, toggle F3 no `onKeyDown`, overlay no template)
- Modify: `frontend/src/app/core/renderer.ts` (expor idade do snapshot)

**Interfaces:**
- Consumes: `eventsIngested` / `eventsDeduped` do Task 4.
- Produces: `PerfRing` — `add(ms: number)`, `percentile(p: number): number`; `GameRenderer.snapshotAgeMs(now: number): number`. O Task 7 lê o overlay para o baseline.

- [ ] **Step 1: Write the failing test**

`frontend/src/app/core/perf-ring.spec.ts`:

```typescript
import { describe, expect, it } from 'vitest';
import { PerfRing } from './perf-ring';

describe('PerfRing', () => {
  it('reads percentiles from known samples', () => {
    const ring = new PerfRing(300);
    for (let i = 1; i <= 100; i++) ring.add(i);
    expect(ring.percentile(50)).toBe(50);
    expect(ring.percentile(95)).toBe(95);
  });

  it('wraps at capacity keeping the latest samples', () => {
    const ring = new PerfRing(10);
    for (let i = 0; i < 25; i++) ring.add(i);
    expect(ring.percentile(100)).toBe(24);
    expect(ring.percentile(0)).toBeGreaterThanOrEqual(15);
  });

  it('reads 0 when empty', () => {
    expect(new PerfRing(10).percentile(95)).toBe(0);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npx vitest run src/app/core/perf-ring.spec.ts`
Expected: FAIL — `./perf-ring` not found.

- [ ] **Step 3: Implement PerfRing**

`frontend/src/app/core/perf-ring.ts`:

```typescript
/** Fixed-size ring of duration samples with percentile readout (debug overlay only). */
export class PerfRing {
  private samples: number[] = [];
  private next = 0;

  constructor(private readonly capacity: number) {}

  add(ms: number): void {
    if (this.samples.length < this.capacity) {
      this.samples.push(ms);
      return;
    }
    this.samples[this.next] = ms;
    this.next = (this.next + 1) % this.capacity;
  }

  percentile(p: number): number {
    if (!this.samples.length) return 0;
    const sorted = [...this.samples].sort((a, b) => a - b);
    const rank = Math.ceil((p / 100) * sorted.length) - 1;
    return sorted[Math.max(0, Math.min(rank, sorted.length - 1))];
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd frontend && npx vitest run src/app/core/perf-ring.spec.ts`
Expected: PASS (3 tests).

- [ ] **Step 5: Expose snapshot age on the renderer**

Em `renderer.ts` (a classe já guarda `snapArrival` em `setSnapshot`):

```typescript
/** Milliseconds since the last snapshot arrived (staleness readout for the debug overlay). */
snapshotAgeMs(now: number): number {
  return this.snapArrival >= 0 ? now - this.snapArrival : 0;
}
```

(Se `snapArrival` inicializar como `undefined`/outro sentinela, alinhar o guard ao valor inicial real do campo.)

- [ ] **Step 6: Measure + overlay in game.ts**

Campos novos no componente:

```typescript
readonly showPerf = signal(false);
readonly perfReadout = signal<{
  frameP50: number; frameP95: number; drawP95: number;
  snapAgeMs: number; eventsIngested: number; eventsDeduped: number; longFrames: number;
} | null>(null);
private readonly framePerf = new PerfRing(300);
private readonly drawPerf = new PerfRing(300);
private lastFrameAt = -1;
private longFrames = 0;
private perfFrameCount = 0;
```

No rAF loop (linhas ~945-957), envolver o draw:

```typescript
const loop = (now: number) => {
  if (this.lastFrameAt >= 0) {
    const frameMs = now - this.lastFrameAt;
    this.framePerf.add(frameMs);
    if (frameMs > 33) this.longFrames++;
  }
  this.lastFrameAt = now;
  try {
    const drawStart = performance.now();
    this.renderer?.draw(now);
    if (this.mini?.nativeElement) this.renderer?.drawMinimap(this.mini.nativeElement);
    this.drawPerf.add(performance.now() - drawStart);
  } catch (err) {
    this.onRenderError(err);
  }
  if (this.showPerf() && ++this.perfFrameCount % 30 === 0) {
    this.perfReadout.set({
      frameP50: this.framePerf.percentile(50),
      frameP95: this.framePerf.percentile(95),
      drawP95: this.drawPerf.percentile(95),
      snapAgeMs: this.renderer?.snapshotAgeMs(now) ?? 0,
      eventsIngested: this.renderer?.eventsIngested ?? 0,
      eventsDeduped: this.renderer?.eventsDeduped ?? 0,
      longFrames: this.longFrames,
    });
  }
  this.raf = requestAnimationFrame(loop);
};
```

No `onKeyDown`, junto dos outros atalhos: `if (k === 'f3') { e.preventDefault(); this.showPerf.update((v) => !v); return; }` (alinhar `k`/`e` aos nomes reais usados no handler).

No template (irmão dos overlays existentes; strings em inglês):

```html
@if (showPerf() && perfReadout(); as perf) {
  <div class="perf-overlay">
    <div>frame p50 {{ perf.frameP50.toFixed(1) }}ms · p95 {{ perf.frameP95.toFixed(1) }}ms</div>
    <div>draw p95 {{ perf.drawP95.toFixed(1) }}ms · long frames {{ perf.longFrames }}</div>
    <div>snapshot age {{ perf.snapAgeMs.toFixed(0) }}ms</div>
    <div>events {{ perf.eventsIngested }} (+{{ perf.eventsDeduped }} deduped)</div>
  </div>
}
```

CSS (junto dos estilos do HUD): `.perf-overlay { position: absolute; top: 8px; right: 8px; z-index: 40; font: 11px/1.5 monospace; color: #9fe8a0; background: rgba(0,0,0,.55); padding: 6px 9px; border-radius: 6px; pointer-events: none; }`

- [ ] **Step 7: Build + manual check**

Run: `cd frontend && npx ng build` → sem erros. Rodar o jogo, apertar F3 numa run.
Expected: overlay aparece com números vivos; F3 de novo esconde.

- [ ] **Step 8: Commit**

```bash
git add frontend/src/app/core/perf-ring.ts frontend/src/app/core/perf-ring.spec.ts frontend/src/app/core/renderer.ts frontend/src/app/pages/game/game.ts
git commit -m "feat(perf): F3 debug overlay with frame/draw percentiles and snapshot age"
```

---

### Task 6: Await atlas preload before joining (kill first-combat stutter)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** low

Hoje o preload de atlases é fire-and-forget ([game.ts:915](../../frontend/src/app/pages/game/game.ts)): `void this.assets.preload([...])`. Os primeiros combates decodificam PNG no meio do frame — stutter garantido. O join passa a esperar o preload, com estado de loading explícito.

**Files:**
- Modify: `frontend/src/app/pages/game/game.ts:913-934` (ngAfterViewInit) e template (estado de loading)

**Interfaces:**
- Consumes: `AssetsService.preload(categories: string[]): Promise<unknown>` (já existe).
- Produces: signal `joining` (o template já tem um branch `@if (!snapshot())` — ele ganha o texto de estado).

- [ ] **Step 1: Await the preload and flag the join state**

Campo novo: `readonly joining = signal(true);`

No `ngAfterViewInit`, substituir a linha `void this.assets.preload([...]).catch(() => undefined);` e o bloco do join por:

```typescript
// Decode the atlases BEFORE joining: lazy-decoding a sheet mid-combat is a guaranteed stutter.
await this.assets.preload(['outfits', 'objects', 'effects', 'missiles']).catch(() => undefined);
```

e após o `joinRun` resolver (dentro do `try`, depois do tratamento de `joined.resumed`): `this.joining.set(false);` (também no `catch`, antes de navegar para fora).

- [ ] **Step 2: Loading copy in the template**

No branch existente `@if (!snapshot())` (linha ~364), exibir o estado: `{{ joining() ? 'Preparing the hunt…' : 'Entering the dungeon…' }}` (integrar ao markup que já existe ali — não criar overlay paralelo).

- [ ] **Step 3: Build + manual check**

Run: `cd frontend && npx ng build` → sem erros. Iniciar uma run com o cache do navegador limpo (DevTools → Disable cache) e o overlay F3 ligado.
Expected: tela de loading breve antes do join; nos primeiros 10s de combate, `long frames` não cresce em salto no primeiro FX de skill (comparar com o comportamento anterior).

- [ ] **Step 4: Commit**

```bash
git add frontend/src/app/pages/game/game.ts
git commit -m "fix(perf): await atlas preload before joining a run"
```

---

### Task 7: Perf baseline + decision gate (Canvas vs PixiJS)

- **Modelo:** Claude Fable 5 · **Effort:** high — interpretar o profiling e decidir se abre o sub-projeto de render contamina todo o trabalho seguinte; é uma das 2 tasks Fable das Ondas 1-3

Task de medição e decisão — produz o documento que fecha a Onda 1 e decide o próximo passo de render. Sem código novo.

**Files:**
- Create: `docs/balance/perf_baseline_2026-07.md`

**Interfaces:**
- Consumes: overlay F3 (Task 5), logs `tick perf`/`run created` (Task 2), backend Release (Task 1), FX confiável (Tasks 3-4).

- [ ] **Step 1: Run the measurement session**

Backend via `tools/run-backend.ps1` (Release), frontend `npm start`, overlay F3 ligado. Cenários, anotando frame p50/p95, draw p95, long frames, snapshot age, tick perf (log) e run-created ms:
1. Run T1 completa (horda floor 1 + boss floor 2).
2. Run no tier mais alto desbloqueado (pilha de mobs máxima + campos de Contágio da Rin se disponível).
3. Primeiros 10s pós-join (validar Task 6).
4. Tab em background por 60s no meio do combate → voltar (validar Tasks 3-4: `deduped` cresceu, FX íntegros, snapshot age recuperou).

- [ ] **Step 2: Write the baseline doc**

`docs/balance/perf_baseline_2026-07.md` com: tabela de números por cenário; veredito por sintoma original (stutter / hitch de geração / FX perdidos — resolvido ou não, com número); e a **decisão de render** pela régua:
- `draw p95 > 12ms` em cenário típico → abrir sub-projeto de render (brainstorm próprio: otimização Canvas dirigida — camada estática em offscreen, cull — vs migração PixiJS), com os números anexados.
- `draw p95 ≤ 12ms` e frame p95 ≤ 16ms → renderer atual basta; registrar e encerrar.
- `tick perf p95 > 30ms` ou `run created > 300ms` → abrir investigação de engine correspondente antes de qualquer trabalho de render.

- [ ] **Step 3: Wave 1 exit gate**

Run: `dotnet build backend/src/KaezanArenaFable.Api`, `dotnet test backend/tests/KaezanArenaFable.Api.Tests`, `cd frontend && npx ng build && npx vitest run`, e `dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays`.
Expected: tudo verde. Critérios do spec: p95 de frame < 16ms na horda típica (senão a decisão do Step 2 já encaminhou o sub-projeto); zero FX perdido no teste de tab em background.

- [ ] **Step 4: Update README + commit**

Atualizar `README.md` (seção "Fluidez e segurança da run"): 1 parágrafo sobre FX com seq/janela de reenvio e overlay F3.

```bash
git add docs/balance/perf_baseline_2026-07.md README.md
git commit -m "docs(perf): wave 1 baseline and render decision"
```

---

## Milestones — Ondas 2-4 (planejar quando chegar a vez)

Cada onda abaixo vira um plano próprio (brainstorm rápido de refinamento se necessário + `superpowers:writing-plans`, ou `roadmap-from-plan` para o formato de roadmap do repo). **Não implementar a partir desta seção** — ela só registra escopo e gates acordados no spec.

### Onda 2 — Geração de mapas v2
Silhueta orgânica multi-lóbulo antes da erosão (`DungeonGenerator.cs`, `ErodeArena`); features intencionais (pilares/cover, câmaras laterais via `RoomsFloorN > 1`, arena de boss anfiteatro); autotiling blob/Wang 47 casos (substitui vizinhança-8, mata "dentes"/buracos); profundidade visual client-side (sombra de borda, variação de chão por ruído determinístico); check de conectividade BFS que falha ruidosamente. **Gate:** replay-check + golden rebaselinado explicitamente por último + screenshots por bioma + sweep BalanceSim sem run inacabável. Depende da Onda 1 (medição).

### Onda 3 — Orquestração idle (runs encadeadas infinitas)
Session Plan server-side no `RunManager` (estratégia: tier, rotação de Kaeli, regras de parada, orçamento de energia; substitui o seletor client-side de Tentativas); Run Journal (resumo por run + agregados de sessão); UI espectador por default (feed de journal, próxima ação visível; intervenção manual pausa a orquestração); economia idle (energia regenerativa, caps offline revisados — constantes em `GameConfig`, tuning via BalanceSim). **Restrição-chave:** orquestração fica FORA do engine — `GameWorld` não muda. **Gate:** sessão 1h+ sem interação; reconexão retoma espectador; replay-check verde. Depende da Onda 2.

### Onda 4 — Migração de assets (packs CC0 + ComfyUI + Codex imagegen) — trilha paralela
Plano completo em `2026-07-06-wave4-asset-migration.md`. Style guide de sprite ANTES de qualquer asset (gate do usuário); auditoria via `tools/AssetAudit`; commodity via packs CC0/CC-BY e via Codex imagegen (plugin game-studio: gpt-image-2 + sprite-pipeline, validado instalado em 2026-07-06); identidade via ComfyUI (`pack_kaeli_outfits.py`) para bosses/assinaturas. Ordem: FX/mísseis → tiles dos 5 biomas (depende da Onda 2 Task 4 — slot `WallSet`) → monstros comuns → itens. Licenças só CC0/CC-BY com `CREDITS.md`. **Gate por categoria:** 100% servida por manifest autoral + screenshot de regressão por bioma. Auditoria, style guide e infra de packs podem começar já, em paralelo às Ondas 2-3.
