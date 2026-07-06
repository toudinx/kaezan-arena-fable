# Onda 3 — Orquestração Idle: Runs Encadeadas Infinitas (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** O jogador define a estratégia (tier, rotação de Kaelis, regras de parada), aperta play e o backend encadeia runs sozinho — aba fechada não interrompe; o cliente vira espectador com journal.

**Architecture:** A orquestração fica **fora do engine** — `GameWorld` não muda em nenhuma task (replay-check verde trivialmente). Três peças novas em `Meta/`: `RunFactory` (construção de run extraída do `GameHub`), `SessionDecider` (lógica de decisão pura, TDD), `SessionOrchestrator` (estado da sessão + journal). O `RunManager` ganha o conceito de *session run*: uma run que tica sem conexão dona; um watcher SignalR pode se anexar/desanexar. O ciclo DI é evitado por mediação: endpoints/hub têm ambos os serviços e fazem a ponte; o `RunManager` só conhece a interface `ISessionCoordinator`.

**Tech Stack:** ASP.NET Core 8 (SignalR, minimal API), xUnit, Angular 21 (signals), Vitest.

**Pré-requisitos:** Onda 1 concluída (projeto de testes + `tools/run-backend.ps1`). Independe da Onda 2.

## Global Constraints

- **`GameWorld` intocado.** Nenhuma task deste plano edita `GameWorld.cs`/`GameWorld.Replay.cs`. Gate por task: `git diff --stat` não pode listar esses arquivos.
- **Determinismo preservado por construção:** o orquestrador roda fora do tick; seeds das runs encadeadas continuam vindo de `Random.Shared.NextInt64` no momento da criação (como o `JoinRun` faz hoje) — a run em si permanece determinística pela seed.
- **Constantes novas em `Domain/GameConfig.cs`.**
- **DTOs novos → espelhar em `frontend/src/app/core/types.ts` na mesma task.**
- **Uma sessão ativa por vez** (jogo é single-account, `AccountState.Id = "local"`). Multi-sessão é YAGNI.
- **Idioma:** código/comentários/UI em inglês; docs em PT.
- Ao final de cada task: `dotnet build` limpo; tasks de frontend: `npx ng build` limpo.

## Modelos & quando usar

> Rubrica completa no plano da Onda 1 (`2026-07-06-wave1-performance-reliability.md`).
> A Task 4 é a task de maior risco das Ondas 1-3 (concorrência no tick loop + watcher +
> mediação de DI + merge com a instrumentação da Onda 1) — é Fable por isso.

| Task | Modelo | Effort |
|---|---|---|
| 1 — Extrair RunFactory | Codex | medium |
| 2 — SessionDecider (TDD puro) | Codex | low |
| 3 — RunJournal | Codex | low |
| 4 — Orquestrador + session runs | **Fable 5** | high |
| 5 — API/Hub + sync do cliente | Opus 4.8 | medium |
| 6 — UI espectador | Opus 4.8 | medium |
| 7 — EnergyLedger | Codex | medium |

---

### Task 1: Extrair `RunFactory` do `GameHub` (behavior-preserving)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium — código movido verbatim

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Meta/RunFactory.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Hubs/GameHub.cs` (linhas 21-84)
- Modify: `backend/src/KaezanArenaFable.Api/Program.cs` (registro DI)

**Interfaces:**
- Consumes: `AccountStore`, `GameData`, `MonsterRegistry`, `KaeliRegistry`, `ItemRegistry`, `ContentStore` (mesmas dependências que o `GameHub` já injeta).
- Produces: `RunFactory.Create(int tier, string? waifuId, long? seed, GameMode mode): GameWorld` — lança `HubException` nos mesmos casos do código atual ("unknown tier", "Kaeli not recruited", "requires account level N"). O `GameHub.JoinRun` delega; a Task 4 (orquestrador) consome.

- [x] **Step 1: Criar `RunFactory` (código movido, não reescrito)**

`backend/src/KaezanArenaFable.Api/Meta/RunFactory.cs`:

```csharp
using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;
using Microsoft.AspNetCore.SignalR;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Builds a ready-to-tick <see cref="GameWorld"/> from account state + content. Extracted verbatim
/// from GameHub.JoinRun (Wave 3) so the idle-session orchestrator can chain runs through the exact
/// same path a manual join uses — loadout, ascension, biome and validation stay in one place.
/// </summary>
public sealed class RunFactory(
    AccountStore store,
    GameData data,
    MonsterRegistry monsters,
    KaeliRegistry kaelis,
    ItemRegistry items,
    ContentStore content)
{
    public GameWorld Create(int tier, string? waifuId, long? seed, GameMode mode)
    {
        var tierDef = content.Tier(tier)
                      ?? throw new HubException("unknown tier");

        var (accountLevel, runWaifuId, ascension, bestiary, equipment, affinityXp, masteryNodes, skinId, helperProfile) =
            store.Read(s =>
            {
                var target = waifuId is { Length: > 0 } && s.OwnedWaifus.Contains(waifuId)
                    ? waifuId
                    : s.OwnedWaifus.Contains(s.ActiveWaifuId) ? s.ActiveWaifuId : Waifus.StarterWaifuId;
                if (waifuId is { Length: > 0 } && !s.OwnedWaifus.Contains(waifuId))
                    throw new HubException("Kaeli not recruited");
                s.Equipment.TryGetValue(AccountState.EquipKey(target, tierDef.Tier), out var loadout);
                return (
                    s.AccountLevel,
                    target,
                    s.Ascension.GetValueOrDefault(target),
                    new Dictionary<string, long>(s.BestiaryKills),
                    loadout?.ToDictionary() ?? [],
                    s.AffinityXp.GetValueOrDefault(target),
                    s.Mastery.TryGetValue(target, out var mastery) ? mastery.Nodes.ToList() : [],
                    s.SelectedSkins.GetValueOrDefault(target),
                    s.HelperProfiles.GetValueOrDefault(target, ""));
            });

        if (accountLevel < tierDef.RequiredAccountLevel)
            throw new HubException($"requires account level {tierDef.RequiredAccountLevel}");

        var waifu = kaelis.Find(runWaifuId) ?? kaelis.ById[Waifus.StarterWaifuId];
        var runSeed = seed ?? Random.Shared.NextInt64(1, long.MaxValue);

        var skin = skinId is not null
            ? waifu.Skins.FirstOrDefault(s => s.Id == skinId) ?? waifu.DefaultSkin
            : waifu.DefaultSkin;
        var kaeliLoadout = new KaeliLoadout(
            KaeliService.AffinityLevelFor(affinityXp),
            Mastery.Aggregate(waifu.Id, masteryNodes),
            skin);

        var equipmentStats = EquipmentStatAggregator.Aggregate(equipment, items.All);
        var biome = content.Biome(tierDef.Tier) ?? Biomes.ForTier(tierDef.Tier);
        return new GameWorld(
            runSeed, tierDef, waifu, ascension, data, monsters, bestiary, equipmentStats, kaeliLoadout, items,
            helperProfile, content.RoleTunings, mode, biome);
    }
}
```

(Isto é o corpo de `GameHub.JoinRun` linhas 24-81 movido; a única mudança é retornar o `GameWorld` em vez de chamar `runs.StartRun`.)

- [x] **Step 2: `GameHub` delega**

`GameHub` passa a injetar `RunFactory factory` (substituindo `GameData data, MonsterRegistry monsters, KaeliRegistry kaelis, ItemRegistry items, AccountStore store, ContentStore content` — que só eram usados pelo JoinRun) e o `JoinRun` vira:

```csharp
public sealed class GameHub(RunManager runs, RunFactory factory) : Hub
{
    public object JoinRun(int tier, string? waifuId = null, long? seed = null, bool resume = false,
        GameMode mode = GameMode.Dungeon)
    {
        if (resume && runs.TryResumeRun(Context.ConnectionId, out var resumedWorld) && resumedWorld is not null)
        {
            return new
            {
                seed = resumedWorld.Seed,
                tier = resumedWorld.Tier.Tier,
                tierName = resumedWorld.Tier.Name,
                waifuId = resumedWorld.Waifu.Id,
                resumed = true
            };
        }

        var world = factory.Create(tier, waifuId, seed, mode);
        runs.StartRun(Context.ConnectionId, world);
        return new
        {
            seed = world.Seed, tier = world.Tier.Tier, tierName = world.Tier.Name,
            waifuId = world.Waifu.Id, mode, resumed = false
        };
    }
    // ... demais métodos inalterados
}
```

Atenção: se o timing de criação de run da Onda 1 (Task 2) tiver adicionado `ILogger<GameHub>` + Stopwatch no JoinRun, preservar — o cronômetro passa a envolver `factory.Create(...)`.

- [x] **Step 3: Registrar no DI**

Em `Program.cs`, junto aos singletons existentes: `builder.Services.AddSingleton<RunFactory>();`

- [x] **Step 4: Build + smoke manual**

Run: `dotnet build backend/src/KaezanArenaFable.Api`
Expected: limpo. Depois `tools/run-backend.ps1` + `npm start`: entrar numa run T1 normalmente (join manual intacto).

(Smoke feito via `run-backend.ps1` + chamada direta `JoinRun` pelo hub SignalR com `@microsoft/signalr`
do `frontend/node_modules` — sem subir o Angular dev server. Resultado: `{"seed":...,"tier":1,
"tierName":"Echoing Den","waifuId":"waifu:velvet","mode":0,"resumed":false}`, join manual intacto.)

- [x] **Step 5: Commit**

```bash
git add backend/src/KaezanArenaFable.Api
git commit -m "refactor(meta): extract RunFactory from GameHub.JoinRun"
```

---

### Task 2: `SessionPlan` + `SessionDecider` (lógica pura, TDD)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** low

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Meta/SessionPlan.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/SessionDeciderTests.cs` (criar)

**Interfaces:**
- Produces (consumidas pela Task 4):
  - `record SessionPlan(int Tier, IReadOnlyList<string> WaifuRotation, int MaxRuns, int StopAfterConsecutiveLosses, int TierUpWins, int MaxTier, bool StopWhenOutOfEnergy)`
  - `record SessionStats(int RunsCompleted, int Wins, int ConsecutiveLosses, int WinsAtCurrentTier, int CurrentTier, int RotationIndex)`
  - `abstract record SessionDecision` com `record StartNextRun(int Tier, string WaifuId) : SessionDecision` e `record StopSession(string Reason) : SessionDecision`
  - `SessionDecider.Decide(SessionPlan, SessionStats, int energyAvailable, int energyPerRun): SessionDecision`

- [x] **Step 1: Testes que falham**

Criar `backend/tests/KaezanArenaFable.Api.Tests/SessionDeciderTests.cs`:

```csharp
using KaezanArenaFable.Api.Meta;

namespace KaezanArenaFable.Api.Tests;

public class SessionDeciderTests
{
    private static SessionPlan Plan(
        int tier = 1, string[]? rotation = null, int maxRuns = 0, int stopLosses = 0,
        int tierUpWins = 0, int maxTier = 5, bool stopNoEnergy = false) =>
        new(tier, rotation ?? ["waifu:eloa"], maxRuns, stopLosses, tierUpWins, maxTier, stopNoEnergy);

    private static SessionStats Stats(
        int runs = 0, int wins = 0, int losses = 0, int winsAtTier = 0, int tier = 1, int rot = 0) =>
        new(runs, wins, losses, winsAtTier, tier, rot);

    [Fact]
    public void starts_next_run_at_plan_tier_with_first_rotation_kaeli()
    {
        var d = SessionDecider.Decide(Plan(tier: 2, rotation: ["waifu:eloa", "waifu:seren"]), Stats(tier: 2), 300, 60);
        var start = Assert.IsType<StartNextRun>(d);
        Assert.Equal(2, start.Tier);
        Assert.Equal("waifu:eloa", start.WaifuId);
    }

    [Fact]
    public void rotation_cycles_by_rotation_index()
    {
        var plan = Plan(rotation: ["waifu:eloa", "waifu:seren"]);
        var d = SessionDecider.Decide(plan, Stats(runs: 3, rot: 3), 300, 60);
        Assert.Equal("waifu:seren", Assert.IsType<StartNextRun>(d).WaifuId);
    }

    [Fact]
    public void stops_when_run_budget_reached()
    {
        var d = SessionDecider.Decide(Plan(maxRuns: 10), Stats(runs: 10), 300, 60);
        Assert.Contains("budget", Assert.IsType<StopSession>(d).Reason);
    }

    [Fact]
    public void stops_after_consecutive_losses()
    {
        var d = SessionDecider.Decide(Plan(stopLosses: 3), Stats(runs: 5, losses: 3), 300, 60);
        Assert.Contains("losses", Assert.IsType<StopSession>(d).Reason);
    }

    [Fact]
    public void stops_when_out_of_energy_if_enabled()
    {
        var d = SessionDecider.Decide(Plan(stopNoEnergy: true), Stats(), 40, 60);
        Assert.Contains("energy", Assert.IsType<StopSession>(d).Reason);
        // disabled: keeps going
        Assert.IsType<StartNextRun>(SessionDecider.Decide(Plan(stopNoEnergy: false), Stats(), 40, 60));
    }

    [Fact]
    public void tiers_up_after_enough_wins_but_respects_ceiling()
    {
        var plan = Plan(tier: 2, tierUpWins: 3, maxTier: 3);
        var up = SessionDecider.Decide(plan, Stats(runs: 4, winsAtTier: 3, tier: 2), 300, 60);
        Assert.Equal(3, Assert.IsType<StartNextRun>(up).Tier);
        var capped = SessionDecider.Decide(plan, Stats(runs: 9, winsAtTier: 5, tier: 3), 300, 60);
        Assert.Equal(3, Assert.IsType<StartNextRun>(capped).Tier);
    }

    [Fact]
    public void zero_valued_rules_mean_disabled()
    {
        var d = SessionDecider.Decide(Plan(maxRuns: 0, stopLosses: 0), Stats(runs: 500, losses: 50), 0, 60);
        Assert.IsType<StartNextRun>(d);
    }
}
```

- [x] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter SessionDeciderTests`
Expected: FAIL (tipos não existem).

- [x] **Step 3: Implementar**

Criar `backend/src/KaezanArenaFable.Api/Meta/SessionPlan.cs`:

```csharp
namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Player-authored strategy for an idle session: which tier to farm, which Kaelis rotate run over
/// run, and when to stop. Zero-valued stop rules mean "disabled" (an infinite session stops only
/// by explicit player action). Pure data — the decision logic lives in <see cref="SessionDecider"/>.
/// </summary>
public sealed record SessionPlan(
    int Tier,
    IReadOnlyList<string> WaifuRotation,
    int MaxRuns,
    int StopAfterConsecutiveLosses,
    int TierUpWins,
    int MaxTier,
    bool StopWhenOutOfEnergy);

/// <summary>Running counters the decider reads. CurrentTier can exceed Plan.Tier via TierUpWins.</summary>
public sealed record SessionStats(
    int RunsCompleted, int Wins, int ConsecutiveLosses, int WinsAtCurrentTier, int CurrentTier, int RotationIndex);

public abstract record SessionDecision;
public sealed record StartNextRun(int Tier, string WaifuId) : SessionDecision;
public sealed record StopSession(string Reason) : SessionDecision;

/// <summary>
/// Pure decision function for run chaining. Stop rules are checked first (budget, loss streak,
/// energy), then tier progression, then Kaeli rotation. No I/O, no clock, no rng — fully unit-tested.
/// </summary>
public static class SessionDecider
{
    public static SessionDecision Decide(SessionPlan plan, SessionStats stats, int energyAvailable, int energyPerRun)
    {
        if (plan.MaxRuns > 0 && stats.RunsCompleted >= plan.MaxRuns)
            return new StopSession($"run budget reached ({plan.MaxRuns})");
        if (plan.StopAfterConsecutiveLosses > 0 && stats.ConsecutiveLosses >= plan.StopAfterConsecutiveLosses)
            return new StopSession($"{stats.ConsecutiveLosses} losses in a row");
        if (plan.StopWhenOutOfEnergy && energyAvailable < energyPerRun)
            return new StopSession("out of energy");

        var tier = stats.CurrentTier;
        if (plan.TierUpWins > 0 && stats.WinsAtCurrentTier >= plan.TierUpWins && tier < plan.MaxTier)
            tier++;

        var waifu = plan.WaifuRotation[stats.RotationIndex % plan.WaifuRotation.Count];
        return new StartNextRun(tier, waifu);
    }
}
```

- [x] **Step 4: Rodar e ver passar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter SessionDeciderTests`
Expected: PASS (7 testes).

- [x] **Step 5: Commit**

```bash
git add backend/src/KaezanArenaFable.Api/Meta/SessionPlan.cs backend/tests
git commit -m "feat(meta): session plan and pure chaining decider"
```

---

### Task 3: `RunJournal` (ring buffer + agregados, TDD)

- **Modelo:** GPT-5.5 (Codex) · **Effort:** low

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Meta/RunJournal.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/RunJournalTests.cs` (criar)

**Interfaces:**
- Consumes: `RunEndDto` (`Engine/GameDtos.cs:140` — `Victory, Reason, GoldEarned, AccountXpEarned, KaerosEarned, Kills, RunLevel, DurationMs, Items, DailyProgressNotes`).
- Produces:
  - `record RunJournalEntry(int RunNumber, long Seed, int Tier, string WaifuId, bool Victory, string Reason, long DurationMs, long Gold, long AccountXp, int Kills, DateTimeOffset EndedAtUtc)`
  - `class RunJournal(int capacity)` com `Add(entry)`, `IReadOnlyList<RunJournalEntry> Entries`, `Aggregate(TimeSpan window, DateTimeOffset nowUtc): SessionAggregates`
  - `record SessionAggregates(int Runs, int Wins, long Gold, long AccountXp, int Kills)`

- [x] **Step 1: Testes que falham**

Criar `backend/tests/KaezanArenaFable.Api.Tests/RunJournalTests.cs`:

```csharp
using KaezanArenaFable.Api.Meta;

namespace KaezanArenaFable.Api.Tests;

public class RunJournalTests
{
    private static RunJournalEntry Entry(int n, bool win = true, long gold = 100, DateTimeOffset? at = null) =>
        new(n, Seed: n, Tier: 1, WaifuId: "waifu:eloa", Victory: win, Reason: win ? "boss defeated" : "died",
            DurationMs: 60_000, Gold: gold, AccountXp: 50, Kills: 30,
            EndedAtUtc: at ?? DateTimeOffset.UtcNow);

    [Fact]
    public void keeps_only_the_newest_capacity_entries()
    {
        var journal = new RunJournal(capacity: 3);
        for (var i = 1; i <= 5; i++) journal.Add(Entry(i));
        Assert.Equal(3, journal.Entries.Count);
        Assert.Equal(3, journal.Entries[0].RunNumber);
        Assert.Equal(5, journal.Entries[^1].RunNumber);
    }

    [Fact]
    public void aggregate_only_counts_entries_inside_the_window()
    {
        var now = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        var journal = new RunJournal(capacity: 10);
        journal.Add(Entry(1, win: true, gold: 100, at: now.AddHours(-3)));  // outside 2h window
        journal.Add(Entry(2, win: true, gold: 200, at: now.AddMinutes(-30)));
        journal.Add(Entry(3, win: false, gold: 50, at: now.AddMinutes(-5)));
        var agg = journal.Aggregate(TimeSpan.FromHours(2), now);
        Assert.Equal(2, agg.Runs);
        Assert.Equal(1, agg.Wins);
        Assert.Equal(250, agg.Gold);
        Assert.Equal(100, agg.AccountXp);
        Assert.Equal(60, agg.Kills);
    }
}
```

- [x] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter RunJournalTests`
Expected: FAIL.

- [x] **Step 3: Implementar**

Criar `backend/src/KaezanArenaFable.Api/Meta/RunJournal.cs`:

```csharp
namespace KaezanArenaFable.Api.Meta;

/// <summary>One line of the idle-session journal: the summary of a finished run.</summary>
public sealed record RunJournalEntry(
    int RunNumber, long Seed, int Tier, string WaifuId, bool Victory, string Reason,
    long DurationMs, long Gold, long AccountXp, int Kills, DateTimeOffset EndedAtUtc);

/// <summary>Rolling window totals shown as "last 2h: 34 runs, 31 wins, +12k gold".</summary>
public sealed record SessionAggregates(int Runs, int Wins, long Gold, long AccountXp, int Kills);

/// <summary>
/// Session-scoped run journal: a bounded list (oldest entries dropped past capacity) plus
/// time-windowed aggregates. In-memory only — a session's history dies with the session by design
/// (account-level totals already live in AccountState.RunsPlayed/RunsWon).
/// </summary>
public sealed class RunJournal(int capacity = 200)
{
    private readonly List<RunJournalEntry> _entries = [];

    public IReadOnlyList<RunJournalEntry> Entries => _entries;

    public void Add(RunJournalEntry entry)
    {
        _entries.Add(entry);
        if (_entries.Count > capacity) _entries.RemoveAt(0);
    }

    public SessionAggregates Aggregate(TimeSpan window, DateTimeOffset nowUtc)
    {
        var cutoff = nowUtc - window;
        int runs = 0, wins = 0, kills = 0;
        long gold = 0, xp = 0;
        foreach (var e in _entries)
        {
            if (e.EndedAtUtc < cutoff) continue;
            runs++;
            if (e.Victory) wins++;
            gold += e.Gold;
            xp += e.AccountXp;
            kills += e.Kills;
        }
        return new SessionAggregates(runs, wins, gold, xp, kills);
    }
}
```

- [x] **Step 4: Rodar e ver passar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter RunJournalTests`
Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add backend/src/KaezanArenaFable.Api/Meta/RunJournal.cs backend/tests
git commit -m "feat(meta): session run journal with windowed aggregates"
```

---

### Task 4: `SessionOrchestrator` + session runs no `RunManager`

- **Modelo:** Claude Fable 5 · **Effort:** high — concorrência no tick loop (lock por run + ConcurrentDictionary), watcher attach/detach, mediação de DI e merge com a instrumentação da Onda 1; errado aqui = corrupção de sessão difícil de reproduzir. É uma das 2 tasks Fable das Ondas 1-3

**Files:**
- Create: `backend/src/KaezanArenaFable.Api/Meta/SessionOrchestrator.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Engine/RunManager.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Program.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`
- Test: `backend/tests/KaezanArenaFable.Api.Tests/SessionOrchestratorTests.cs` (criar)

**Interfaces:**
- Consumes: `RunFactory` (Task 1), `SessionDecider`/`SessionPlan` (Task 2), `RunJournal` (Task 3), `RunEndDto`.
- Produces:
  - `interface ISessionCoordinator { GameWorld? OnRunCompleted(GameWorld world, RunEndDto end); void OnManualInput(); }` (em `Meta/SessionOrchestrator.cs`)
  - `SessionOrchestrator : ISessionCoordinator` com `StartSession(SessionPlan): GameWorld`, `Resume(): GameWorld?`, `Stop(string reason)`, `SessionSnapshot(): SessionStateDto?`
  - `RunManager.StartSessionRun(GameWorld world)`, `RunManager.AttachWatcher(string connectionId): GameWorld?`, `RunManager.DetachWatcher(string connectionId)`; `ActiveRun` ganha `bool IsSession` e `string? WatcherConnectionId`
  - `GameConfig.SessionRunChainDelayTicks`, `GameConfig.SessionJournalCapacity`
  - `record SessionStateDto(string Status, int RunNumber, int CurrentTier, string CurrentWaifuId, string? StopReason, SessionAggregates Last2h, List<RunJournalEntry> Journal)` (em `SessionOrchestrator.cs`; `Status` ∈ `"running" | "paused" | "stopped"`)

- [ ] **Step 1: Testes do orquestrador (a parte testável sem SignalR)**

O orquestrador é testável com um `RunFactory` real? Não — registries pesados. Testar a máquina de estados isolando a criação de mundo: o orquestrador recebe `Func<int, string, GameWorld?> createWorld` no construtor de teste? Não — mantemos produção simples: o orquestrador expõe a transição de estado pura via método interno `internal (SessionStats stats, SessionDecision decision) Advance(SessionStats stats, SessionPlan plan, bool victory, int energy, int energyPerRun)` e os testes cobrem ele. Criar `backend/tests/KaezanArenaFable.Api.Tests/SessionOrchestratorTests.cs`:

```csharp
using KaezanArenaFable.Api.Meta;

namespace KaezanArenaFable.Api.Tests;

public class SessionOrchestratorTests
{
    private static readonly SessionPlan Plan =
        new(1, ["waifu:eloa", "waifu:seren"], MaxRuns: 0, StopAfterConsecutiveLosses: 3,
            TierUpWins: 2, MaxTier: 3, StopWhenOutOfEnergy: false);

    [Fact]
    public void victory_resets_loss_streak_and_advances_rotation()
    {
        var stats = new SessionStats(RunsCompleted: 4, Wins: 2, ConsecutiveLosses: 2,
            WinsAtCurrentTier: 1, CurrentTier: 1, RotationIndex: 4);
        var (next, decision) = SessionOrchestrator.Advance(stats, Plan, victory: true, energy: 300, energyPerRun: 60);
        Assert.Equal(5, next.RunsCompleted);
        Assert.Equal(0, next.ConsecutiveLosses);
        Assert.Equal(2, next.WinsAtCurrentTier);
        var start = Assert.IsType<StartNextRun>(decision);
        Assert.Equal(2, start.Tier);              // TierUpWins=2 reached
        Assert.Equal("waifu:seren", start.WaifuId); // rotation index 5 -> second Kaeli
    }

    [Fact]
    public void tier_up_resets_wins_at_tier_counter()
    {
        var stats = new SessionStats(4, 2, 0, 1, CurrentTier: 1, RotationIndex: 0);
        var (next, decision) = SessionOrchestrator.Advance(stats, Plan, victory: true, 300, 60);
        Assert.Equal(2, Assert.IsType<StartNextRun>(decision).Tier);
        Assert.Equal(2, next.CurrentTier);
        Assert.Equal(0, next.WinsAtCurrentTier);
    }

    [Fact]
    public void third_straight_loss_stops_the_session()
    {
        var stats = new SessionStats(9, 5, 2, 0, 1, 9);
        var (_, decision) = SessionOrchestrator.Advance(stats, Plan, victory: false, 300, 60);
        Assert.IsType<StopSession>(decision);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter SessionOrchestratorTests`
Expected: FAIL.

- [ ] **Step 3: Implementar o orquestrador**

Constantes em `GameConfig.cs` (junto ao bloco farm/energia, ~linha 845):

```csharp
    /// <summary>Wave 3: ticks the ended run keeps being shown before the orchestrator swaps in the
    /// next chained run (the "Victory/Defeat" beat the spectator sees between runs).</summary>
    public const int SessionRunChainDelayTicks = 50;
    /// <summary>Wave 3: journal entries kept per idle session.</summary>
    public const int SessionJournalCapacity = 200;
```

Criar `backend/src/KaezanArenaFable.Api/Meta/SessionOrchestrator.cs`:

```csharp
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Meta;

/// <summary>Seam the RunManager uses to chain session runs without a DI cycle: the RunManager only
/// knows this interface; endpoints/hub mediate everything else.</summary>
public interface ISessionCoordinator
{
    /// <summary>Called by the RunManager when a session run finished and its end-beat elapsed.
    /// Returns the next chained world, or null when the session stops/pauses.</summary>
    GameWorld? OnRunCompleted(GameWorld world, RunEndDto end);

    /// <summary>Manual player input on a watched session run pauses chaining until resumed.</summary>
    void OnManualInput();
}

public sealed record SessionStateDto(
    string Status, int RunNumber, int CurrentTier, string CurrentWaifuId, string? StopReason,
    SessionAggregates Last2h, List<RunJournalEntry> Journal);

/// <summary>
/// Owns the single idle session (single-account game): plan, stats, journal and status. Lives
/// entirely OUTSIDE the engine — GameWorld is never touched; chaining happens between runs.
/// Thread-safety: all mutations under one lock (called from the RunManager tick loop and from
/// API endpoints).
/// </summary>
public sealed class SessionOrchestrator(RunFactory factory, AccountStore store, ILogger<SessionOrchestrator> logger)
    : ISessionCoordinator
{
    private readonly object _lock = new();
    private SessionPlan? _plan;
    private SessionStats _stats = new(0, 0, 0, 0, 1, 0);
    private RunJournal _journal = new(GameConfig.SessionJournalCapacity);
    private string _status = "stopped"; // running | paused | stopped
    private string? _stopReason;
    private string _currentWaifuId = "";
    private bool _pendingChain; // a run ended while paused; Resume() must kick the next one

    public GameWorld StartSession(SessionPlan plan)
    {
        lock (_lock)
        {
            if (_status == "running") throw new InvalidOperationException("a session is already running");
            _plan = plan;
            _stats = new SessionStats(0, 0, 0, 0, plan.Tier, 0);
            _journal = new RunJournal(GameConfig.SessionJournalCapacity);
            _status = "running";
            _stopReason = null;
            _pendingChain = false;
            var decision = SessionDecider.Decide(plan, _stats, EnergyAvailable(), GameConfig.DungeonEnergyPerRun);
            if (decision is not StartNextRun start)
                throw new InvalidOperationException(((StopSession)decision).Reason);
            return CreateWorld(start);
        }
    }

    public void Stop(string reason)
    {
        lock (_lock)
        {
            if (_status == "stopped") return;
            _status = "stopped";
            _stopReason = reason;
        }
    }

    public void OnManualInput()
    {
        lock (_lock)
        {
            if (_status == "running")
            {
                _status = "paused";
                logger.LogInformation("session paused by manual input");
            }
        }
    }

    /// <summary>Clears the pause; when a run already ended while paused, returns the next world
    /// to hand to the RunManager (mediated by the caller), else null (current run continues).</summary>
    public GameWorld? Resume()
    {
        lock (_lock)
        {
            if (_status != "paused") return null;
            _status = "running";
            if (!_pendingChain) return null;
            _pendingChain = false;
            var decision = SessionDecider.Decide(_plan!, _stats, EnergyAvailable(), GameConfig.DungeonEnergyPerRun);
            if (decision is StartNextRun start) return CreateWorld(start);
            Stop(((StopSession)decision).Reason);
            return null;
        }
    }

    public GameWorld? OnRunCompleted(GameWorld world, RunEndDto end)
    {
        lock (_lock)
        {
            if (_plan is null || _status == "stopped") return null;

            SessionDecision decision;
            (_stats, decision) = Advance(_stats, _plan, end.Victory, EnergyAvailable(), GameConfig.DungeonEnergyPerRun);
            _journal.Add(new RunJournalEntry(
                _stats.RunsCompleted, world.Seed, world.Tier.Tier, world.Waifu.Id, end.Victory, end.Reason,
                end.DurationMs, end.GoldEarned, end.AccountXpEarned, end.Kills, DateTimeOffset.UtcNow));

            if (_status == "paused")
            {
                _pendingChain = true;
                return null;
            }
            if (decision is StopSession stop)
            {
                _status = "stopped";
                _stopReason = stop.Reason;
                return null;
            }
            return CreateWorld((StartNextRun)decision);
        }
    }

    /// <summary>Pure stats transition + next decision. Internal for unit tests.</summary>
    internal static (SessionStats Stats, SessionDecision Decision) Advance(
        SessionStats stats, SessionPlan plan, bool victory, int energy, int energyPerRun)
    {
        var next = stats with
        {
            RunsCompleted = stats.RunsCompleted + 1,
            Wins = stats.Wins + (victory ? 1 : 0),
            ConsecutiveLosses = victory ? 0 : stats.ConsecutiveLosses + 1,
            WinsAtCurrentTier = stats.WinsAtCurrentTier + (victory ? 1 : 0),
            RotationIndex = stats.RotationIndex + 1,
        };
        var decision = SessionDecider.Decide(plan, next, energy, energyPerRun);
        if (decision is StartNextRun start && start.Tier > next.CurrentTier)
            next = next with { CurrentTier = start.Tier, WinsAtCurrentTier = 0 };
        return (next, decision);
    }

    public SessionStateDto? Snapshot()
    {
        lock (_lock)
        {
            if (_plan is null) return null;
            return new SessionStateDto(
                _status, _stats.RunsCompleted, _stats.CurrentTier, _currentWaifuId, _stopReason,
                _journal.Aggregate(TimeSpan.FromHours(2), DateTimeOffset.UtcNow),
                _journal.Entries.Reverse().Take(30).ToList());
        }
    }

    private GameWorld CreateWorld(StartNextRun start)
    {
        _currentWaifuId = start.WaifuId;
        return factory.Create(start.Tier, start.WaifuId, seed: null, Engine.GameMode.Dungeon);
    }

    // Energy is wired for real in Task 7; until then sessions see "always full".
    private int EnergyAvailable() => GameConfig.DungeonEnergyCap;
}
```

(Se `GameMode` não estiver acessível sem qualificação, usar `KaezanArenaFable.Api.Engine.GameMode.Dungeon`.)

- [ ] **Step 4: Session runs no `RunManager`**

Em `RunManager.cs`:

1. `ActiveRun` ganha campos:

```csharp
public sealed class ActiveRun
{
    public required GameWorld World;
    public required string ConnectionId;   // session runs: the fixed key "session:local"
    public bool RewardsApplied;
    public int TicksAfterEnd;
    public bool IsSession;
    public string? WatcherConnectionId;    // SignalR client currently spectating (nullable)
}
```

2. Construtor ganha o coordinator: `RunManager(IHubContext<GameHub> hub, RewardService rewards, ReplayStore replays, ISessionCoordinator sessions, ILogger<RunManager> logger)` (+ `using KaezanArenaFable.Api.Meta;`).

3. Novos métodos:

```csharp
    private const string SessionKey = "session:local";

    /// <summary>Starts (or replaces) the single idle-session run. Session runs tick with no owning
    /// connection; a watcher may attach for snapshots.</summary>
    public void StartSessionRun(GameWorld world)
    {
        string? watcher = null;
        if (_runs.TryRemove(SessionKey, out var previous))
        {
            watcher = previous.WatcherConnectionId;
            FinalizeAbandon(previous);
        }
        _runs[SessionKey] = new ActiveRun
        {
            World = world, ConnectionId = SessionKey, IsSession = true, WatcherConnectionId = watcher
        };
        if (watcher is not null) world.RequestMapRefresh();
    }

    /// <summary>Attaches a spectator to the session run (returns its world, or null when no session).</summary>
    public GameWorld? AttachWatcher(string connectionId)
    {
        if (!_runs.TryGetValue(SessionKey, out var run)) return null;
        lock (run)
        {
            run.WatcherConnectionId = connectionId;
            run.World.RequestMapRefresh();
            return run.World;
        }
    }

    public void DetachWatcher(string connectionId)
    {
        if (_runs.TryGetValue(SessionKey, out var run))
            lock (run)
                if (run.WatcherConnectionId == connectionId) run.WatcherConnectionId = null;
    }

    public void StopSessionRun()
    {
        if (_runs.TryRemove(SessionKey, out var run)) FinalizeAbandon(run);
    }
```

4. `GetRun` resolve watcher também (para os comandos manuais do espectador funcionarem — e pausarem o chaining):

```csharp
    public GameWorld? GetRun(string connectionId)
    {
        if (_runs.TryGetValue(connectionId, out var run)) return run.World;
        if (_runs.TryGetValue(SessionKey, out var session) && session.WatcherConnectionId == connectionId)
        {
            sessions.OnManualInput(); // manual interference pauses chaining until resumed
            return session.World;
        }
        return null;
    }
```

5. `DropRun` (disconnect): antes do `TryRemove` atual, adicionar no topo: `DetachWatcher(connectionId);` (watcher some, sessão continua).

6. No loop `ExecuteAsync`, o corpo do `foreach` muda em dois pontos:

Envio — o destino agora é o watcher para session runs; sem watcher, não envia (mas tica):

```csharp
                    var target = run.IsSession ? run.WatcherConnectionId : connectionId;
                    if (target is not null)
                    {
                        if (map is not null)
                            await hub.Clients.Client(target).SendAsync("map", map, stoppingToken);
                        await hub.Clients.Client(target).SendAsync("snapshot", snapshot, stoppingToken);
                    }
```

Fim de run — session runs encadeiam em vez de morrer (dentro do `lock (run)`, substituindo o bloco `if (run.RewardsApplied && ++run.TicksAfterEnd > 50)`):

```csharp
                        if (run.RewardsApplied && ++run.TicksAfterEnd > GameConfig.SessionRunChainDelayTicks)
                        {
                            if (run.IsSession && snapshot.Run.Ended is { } ended)
                            {
                                var next = sessions.OnRunCompleted(run.World, ended);
                                if (next is not null)
                                {
                                    run.World = next;
                                    run.RewardsApplied = false;
                                    run.TicksAfterEnd = 0;
                                    if (run.WatcherConnectionId is not null) next.RequestMapRefresh();
                                }
                                else _runs.TryRemove(connectionId, out _);
                            }
                            else _runs.TryRemove(connectionId, out _);
                        }
```

Atenção Onda 1: se a Task 3/2 da Onda 1 tiver adicionado `PerfStats`/log em volta de `run.World.Tick()`, preservar intacto. O `50` hardcoded vira `GameConfig.SessionRunChainDelayTicks` (mesmo valor). Adicionar `using KaezanArenaFable.Api.Domain;` se faltar. Nota: `snapshot.Run.Ended` aqui é o DTO enriquecido pelo `rewards.Apply` do tick em que a run terminou? Não — após aquele tick, `Ended` volta do `World`. Para o journal ter gold/xp corretos, capture o enriquecido: adicionar campo `public RunEndDto? EnrichedEnd;` em `ActiveRun`, setado no bloco existente `if (snapshot.Run.Ended is not null && !run.RewardsApplied)` (`run.EnrichedEnd = enriched;`), e passar `run.EnrichedEnd ?? ended` para `sessions.OnRunCompleted`.

7. `Program.cs`:

```csharp
builder.Services.AddSingleton<SessionOrchestrator>();
builder.Services.AddSingleton<ISessionCoordinator>(sp => sp.GetRequiredService<SessionOrchestrator>());
```

(antes do registro do `RunManager`/hosted service, que agora depende de `ISessionCoordinator`).

- [ ] **Step 5: Rodar testes + build**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests` e `dotnet build backend/src/KaezanArenaFable.Api`
Expected: PASS / limpo.

- [ ] **Step 6: Verificar invariante e commitar**

```bash
git diff --stat | grep -i gameworld   # deve retornar vazio
git add backend/src/KaezanArenaFable.Api backend/tests
git commit -m "feat(engine,meta): session orchestrator chains detached session runs"
```

---

### Task 5: Superfície API/Hub + sync do cliente (`types.ts`, `game-client`, `api.service`)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium — exige adaptar aos helpers reais de `api.service`/`game-client`

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Api/MetaEndpoints.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Hubs/GameHub.cs`
- Modify: `frontend/src/app/core/types.ts`
- Modify: `frontend/src/app/core/api.service.ts`
- Modify: `frontend/src/app/core/game-client.service.ts`

**Interfaces:**
- Produces (backend): `POST /api/v1/session/start` (body `SessionStartRequest`), `POST /api/v1/session/stop`, `POST /api/v1/session/resume`, `GET /api/v1/session` → `SessionStateDto | 204`; hub `WatchSession(): object` (payload igual ao do JoinRun) e `StopWatching()`.
- Produces (frontend): `SessionPlanRequest`, `SessionStateDto`, `RunJournalEntryDto`, `SessionAggregatesDto` em `types.ts`; `ApiService.startSession/stopSession/resumeSession/getSession`; `GameClientService.watchSession()`.

- [ ] **Step 1: Endpoints**

Em `MetaEndpoints.cs`, adicionar após o bloco do catalog (usar o `api` group existente):

```csharp
        // ---- idle session (Wave 3): server-side run chaining ----
        api.MapPost("/session/start", (SessionStartRequest req, SessionOrchestrator sessions, RunManager runs) =>
        {
            var plan = new SessionPlan(
                req.Tier,
                req.WaifuRotation is { Count: > 0 } ? req.WaifuRotation : throw new BadHttpRequestException("empty rotation"),
                req.MaxRuns, req.StopAfterConsecutiveLosses, req.TierUpWins,
                Math.Max(req.Tier, req.MaxTier), req.StopWhenOutOfEnergy);
            var world = sessions.StartSession(plan);
            runs.StartSessionRun(world);
            return Results.Ok(sessions.Snapshot());
        });

        api.MapPost("/session/stop", (SessionOrchestrator sessions, RunManager runs) =>
        {
            sessions.Stop("stopped by player");
            runs.StopSessionRun();
            return Results.Ok(sessions.Snapshot());
        });

        api.MapPost("/session/resume", (SessionOrchestrator sessions, RunManager runs) =>
        {
            var world = sessions.Resume();
            if (world is not null) runs.StartSessionRun(world);
            return Results.Ok(sessions.Snapshot());
        });

        api.MapGet("/session", (SessionOrchestrator sessions) =>
            sessions.Snapshot() is { } s ? Results.Ok(s) : Results.NoContent());
```

E o request record no fim do arquivo (ou junto aos outros request records, se houver):

```csharp
public sealed record SessionStartRequest(
    int Tier, List<string> WaifuRotation, int MaxRuns, int StopAfterConsecutiveLosses,
    int TierUpWins, int MaxTier, bool StopWhenOutOfEnergy);
```

Imports necessários: `using KaezanArenaFable.Api.Engine;` (RunManager).

- [ ] **Step 2: Hub**

Em `GameHub.cs`:

```csharp
    /// <summary>Attaches this connection as the spectator of the running idle session.</summary>
    public object WatchSession()
    {
        var world = runs.AttachWatcher(Context.ConnectionId)
                    ?? throw new HubException("no active session");
        return new
        {
            seed = world.Seed, tier = world.Tier.Tier, tierName = world.Tier.Name,
            waifuId = world.Waifu.Id, resumed = false, watching = true
        };
    }

    public void StopWatching() => runs.DetachWatcher(Context.ConnectionId);
```

- [ ] **Step 3: `types.ts`**

Adicionar (nomes camelCase — serialização padrão do ASP.NET):

```typescript
export interface SessionPlanRequest {
  tier: number;
  waifuRotation: string[];
  maxRuns: number;
  stopAfterConsecutiveLosses: number;
  tierUpWins: number;
  maxTier: number;
  stopWhenOutOfEnergy: boolean;
}

export interface RunJournalEntryDto {
  runNumber: number;
  seed: number;
  tier: number;
  waifuId: string;
  victory: boolean;
  reason: string;
  durationMs: number;
  gold: number;
  accountXp: number;
  kills: number;
  endedAtUtc: string;
}

export interface SessionAggregatesDto {
  runs: number;
  wins: number;
  gold: number;
  accountXp: number;
  kills: number;
}

export interface SessionStateDto {
  status: 'running' | 'paused' | 'stopped';
  runNumber: number;
  currentTier: number;
  currentWaifuId: string;
  stopReason: string | null;
  last2h: SessionAggregatesDto;
  journal: RunJournalEntryDto[];
}
```

- [ ] **Step 4: `api.service.ts` + `game-client.service.ts`**

`api.service.ts` — seguir o padrão dos métodos POST/GET existentes do arquivo (mesmo helper de fetch):

```typescript
  startSession(plan: SessionPlanRequest): Promise<SessionStateDto> {
    return this.post<SessionStateDto>('/api/v1/session/start', plan);
  }
  stopSession(): Promise<SessionStateDto> {
    return this.post<SessionStateDto>('/api/v1/session/stop', {});
  }
  resumeSession(): Promise<SessionStateDto> {
    return this.post<SessionStateDto>('/api/v1/session/resume', {});
  }
  getSession(): Promise<SessionStateDto | null> {
    return this.get<SessionStateDto | null>('/api/v1/session');
  }
```

(Adapte `post`/`get` para os nomes reais dos helpers privados do serviço — abrir o arquivo e seguir o padrão local; um GET 204 deve resolver como `null`.)

`game-client.service.ts` — espelhar o `joinRun` (linha 44) para o watch (mesma conexão/handlers de `snapshot`/`map`):

```typescript
  async watchSession(): Promise<{ seed: number; tier: number; waifuId: string }> {
    await this.ensureConnection(); // the same connect path joinRun uses before invoking
    return await this.connection!.invoke('WatchSession');
  }

  stopWatching(): void {
    void this.connection?.invoke('StopWatching').catch(() => undefined);
  }
```

(`ensureConnection` = o trecho de setup de conexão que `joinRun` já executa; se `joinRun` inline-a, extrair para um método privado nesta task e fazer os dois usarem.)

- [ ] **Step 5: Builds + smoke**

```bash
dotnet build backend/src/KaezanArenaFable.Api
cd frontend && npx ng build
```
Expected: limpos. Smoke com backend rodando:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5210/api/v1/session/start -ContentType 'application/json' -Body '{"tier":1,"waifuRotation":["waifu:eloa"],"maxRuns":3,"stopAfterConsecutiveLosses":0,"tierUpWins":0,"maxTier":1,"stopWhenOutOfEnergy":false}'
Start-Sleep 30; Invoke-RestMethod http://localhost:5210/api/v1/session
```
Expected: primeira chamada retorna `status: running`; a segunda mostra `runNumber` avançando com journal populado (runs T1 duram <60s) — **runs encadeando sem nenhum cliente conectado**. (Se a Kaeli inicial não for `waifu:eloa`, use o id real do starter em `Waifus.StarterWaifuId`.)

- [ ] **Step 6: Commit**

```bash
git add backend/src/KaezanArenaFable.Api frontend/src/app/core
git commit -m "feat(api,hub): idle session endpoints and spectator attach"
```

---

### Task 6: UI espectador (config na página mode + painel de sessão no game)

- **Modelo:** Claude Code Opus 4.8 · **Effort:** medium — `game.ts` é grande e a integração (query params, timers, overlay de fim de run) é sensível

**Files:**
- Modify: `frontend/src/app/pages/mode/mode.ts` (painel "Idle Session" ao lado do farm-plan, ~linha 97)
- Modify: `frontend/src/app/pages/game/game.ts` (modo espectador)

**Interfaces:**
- Consumes: `ApiService.startSession/getSession/stopSession/resumeSession`, `GameClientService.watchSession/stopWatching`, `SessionStateDto`.
- Produces: rota `game` com query param `session=1` entra como espectador; painel de sessão com journal + status + botões Resume/Stop.

- [ ] **Step 1: Painel de config na página mode**

Em `mode.ts`, adicionar ao card do tier (abaixo do `.farm-plan`) o bloco de sessão. Sinais no componente:

```typescript
  readonly sessionMaxRuns = signal(0);          // 0 = infinite
  readonly sessionStopLosses = signal(3);
  readonly sessionTierUp = signal(0);           // 0 = stay on tier
  readonly sessionStarting = signal(false);
```

Template (dentro do `<aside>` do tier, após `.farm-plan`):

```html
                <div class="idle-session">
                  <div class="farm-head"><span>Idle Session</span></div>
                  <label>Stop after losses in a row
                    <input type="number" min="0" max="20" [value]="sessionStopLosses()"
                           (input)="sessionStopLosses.set(+$any($event.target).value)" />
                  </label>
                  <label>Max runs (0 = endless)
                    <input type="number" min="0" max="999" [value]="sessionMaxRuns()"
                           (input)="sessionMaxRuns.set(+$any($event.target).value)" />
                  </label>
                  <label>Tier up after wins (0 = never)
                    <input type="number" min="0" max="20" [value]="sessionTierUp()"
                           (input)="sessionTierUp.set(+$any($event.target).value)" />
                  </label>
                  <button class="pill-btn" [disabled]="sessionStarting() || locked(t.requiredAccountLevel)"
                          (click)="startIdleSession(t.tier)">Start idle session</button>
                </div>
```

Método (rotação = Kaeli ativa; multi-select de rotação fica para iteração futura — YAGNI):

```typescript
  async startIdleSession(tier: number): Promise<void> {
    this.sessionStarting.set(true);
    try {
      const waifuId = this.api.account()?.activeWaifuId;
      if (!waifuId) return;
      await this.api.startSession({
        tier,
        waifuRotation: [waifuId],
        maxRuns: this.sessionMaxRuns(),
        stopAfterConsecutiveLosses: this.sessionStopLosses(),
        tierUpWins: this.sessionTierUp(),
        maxTier: 5,
        stopWhenOutOfEnergy: false,
      });
      void this.router.navigate(['/game'], { queryParams: { session: 1 } });
    } finally {
      this.sessionStarting.set(false);
    }
  }
```

(Ajustar aos nomes reais: leitor de conta do `ApiService` (`account()` signal ou equivalente) e o `Router` já injetado na página — conferir no arquivo e seguir o padrão local; estilos `.idle-session` copiam o visual do `.farm-plan`.)

- [ ] **Step 2: Espectador na página game**

Em `game.ts`:

1. Detectar o modo: onde a página lê os query params atuais (tier/waifu), ler também `session`; se `session === '1'`, no init chamar `await this.client.watchSession()` em vez de `joinRun`.
2. Sinais + polling:

```typescript
  readonly sessionState = signal<SessionStateDto | null>(null);
  private sessionPollTimer = 0;

  private startSessionPolling(): void {
    const poll = async () => this.sessionState.set(await this.api.getSession());
    void poll();
    this.sessionPollTimer = window.setInterval(() => void poll(), 5000);
  }
```

(Limpar o interval no `ngOnDestroy` junto aos timers existentes; chamar `startSessionPolling()` só no modo sessão.)

3. Painel no template (junto ao HUD, escondido fora do modo sessão):

```html
      @if (sessionState(); as s) {
        <aside class="session-panel">
          <header>
            <b>Idle session — run {{ s.runNumber + 1 }} · T{{ s.currentTier }}</b>
            <span class="status" [class.paused]="s.status === 'paused'">{{ s.status }}</span>
          </header>
          <p class="agg">Last 2h: {{ s.last2h.runs }} runs · {{ s.last2h.wins }} wins ·
            +{{ s.last2h.gold }} gold · +{{ s.last2h.accountXp }} xp</p>
          @if (s.status === 'paused') {
            <p class="note">Paused by manual input.</p>
            <button class="btn" (click)="resumeSession()">Resume chaining</button>
          }
          @if (s.status === 'stopped') {
            <p class="note">Stopped: {{ s.stopReason }}</p>
          }
          <button class="btn secondary" (click)="stopSession()">Stop session</button>
          <ul class="journal">
            @for (e of s.journal; track e.runNumber) {
              <li [class.loss]="!e.victory">
                #{{ e.runNumber }} T{{ e.tier }} — {{ e.victory ? 'W' : 'L' }} ·
                {{ (e.durationMs / 1000).toFixed(0) }}s · +{{ e.gold }}g
              </li>
            }
          </ul>
        </aside>
      }
```

4. Ações:

```typescript
  async resumeSession(): Promise<void> {
    this.sessionState.set(await this.api.resumeSession());
  }

  async stopSession(): Promise<void> {
    this.sessionState.set(await this.api.stopSession());
    this.leave(); // back to Hunt, same as the existing button
  }
```

5. No modo sessão: suprimir `maybeScheduleAutoRepeat` (o encadeamento agora é do servidor) — guard no topo: `if (this.sessionMode) return;`. O overlay de fim de run continua aparecendo durante o beat entre runs (o `SessionRunChainDelayTicks` do servidor) e some sozinho quando o snapshot da run nova chega — comportamento desejado.

- [ ] **Step 3: Build + verificação no preview**

`npx ng build` limpo. Com backend + frontend rodando: iniciar sessão idle T1 pela página mode → assistir 2-3 runs encadearem sem input; apertar uma tecla de movimento → painel mostra `paused`; Resume → encadeia de novo; fechar a aba no meio de uma run, reabrir `/game?session=1` → espectador retoma a run em andamento.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/app/pages
git commit -m "feat(ui): idle session config and spectator panel with run journal"
```

---

### Task 7: Energia real (regen server-side) + tuning

- **Modelo:** GPT-5.5 (Codex) · **Effort:** medium — escale para Opus 4.8 se a persistência de conta for EF colunar (migration) em vez de blob JSON

**Files:**
- Modify: `backend/src/KaezanArenaFable.Api/Meta/AccountState.cs`
- Create: `backend/src/KaezanArenaFable.Api/Meta/EnergyLedger.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Meta/SessionOrchestrator.cs` (usar energia real)
- Modify: `backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`
- Modify: `backend/src/KaezanArenaFable.Api/Api/MetaEndpoints.cs` (expor regen no catalog farm)
- Test: `backend/tests/KaezanArenaFable.Api.Tests/EnergyLedgerTests.cs` (criar)

**Interfaces:**
- Produces: `EnergyLedger.Current(AccountState, DateTimeOffset): int`; `EnergyLedger.TrySpend(AccountState, int amount, DateTimeOffset): bool`; `AccountState.Energy` (int, default = cap) e `AccountState.EnergyUpdatedUtc` (string ISO-8601, `""` = nunca); `GameConfig.EnergyRegenPerMinute`.
- **Decisão de design:** energia limita apenas o ENCADEAMENTO da sessão idle (regra de parada); runs manuais nunca são bloqueadas (idle-friendly: sem punição por jogar). Documentar no README.

- [ ] **Step 1: Testes que falham**

Criar `backend/tests/KaezanArenaFable.Api.Tests/EnergyLedgerTests.cs`:

```csharp
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Meta;

namespace KaezanArenaFable.Api.Tests;

public class EnergyLedgerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void fresh_account_is_at_cap()
    {
        var s = new AccountState();
        Assert.Equal(GameConfig.DungeonEnergyCap, EnergyLedger.Current(s, T0));
    }

    [Fact]
    public void regenerates_over_elapsed_time_and_clamps_at_cap()
    {
        var s = new AccountState { Energy = 100, EnergyUpdatedUtc = T0.ToString("O") };
        var after10 = EnergyLedger.Current(s, T0.AddMinutes(10));
        Assert.Equal(100 + 10 * GameConfig.EnergyRegenPerMinute, after10);
        Assert.Equal(GameConfig.DungeonEnergyCap, EnergyLedger.Current(s, T0.AddHours(48)));
    }

    [Fact]
    public void try_spend_settles_regen_then_deducts()
    {
        var s = new AccountState { Energy = 50, EnergyUpdatedUtc = T0.ToString("O") };
        var now = T0.AddMinutes(10); // 50 + 10*regen available
        Assert.True(EnergyLedger.TrySpend(s, 60, now));
        Assert.Equal(50 + 10 * GameConfig.EnergyRegenPerMinute - 60, s.Energy);
        Assert.Equal(now.ToString("O"), s.EnergyUpdatedUtc);
    }

    [Fact]
    public void try_spend_fails_without_enough_energy_and_mutates_nothing_but_settlement()
    {
        var s = new AccountState { Energy = 10, EnergyUpdatedUtc = T0.ToString("O") };
        Assert.False(EnergyLedger.TrySpend(s, 60, T0.AddMinutes(1)));
        Assert.Equal(10 + GameConfig.EnergyRegenPerMinute, s.Energy); // settled, not spent
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests --filter EnergyLedgerTests`
Expected: FAIL.

- [ ] **Step 3: Implementar**

`GameConfig.cs`, junto a `DungeonEnergyPerRun` (~linha 847):

```csharp
    /// <summary>Wave 3: energy regenerated per real-time minute. 3/min = a 60-energy run every
    /// 20 minutes once the 300 bank runs dry — the pace limiter of endless idle sessions.
    /// Starting value; tune by feel + BalanceSim once sessions are live.</summary>
    public const int EnergyRegenPerMinute = 3;
```

`AccountState.cs`, junto aos campos de conta:

```csharp
    /// <summary>Wave 3: idle-session energy bank. Settled lazily by EnergyLedger; "" = never settled
    /// (treated as full). Manual runs never spend energy — it only paces session chaining.</summary>
    public int Energy { get; set; } = Domain.GameConfig.DungeonEnergyCap;
    public string EnergyUpdatedUtc { get; set; } = "";
```

Criar `backend/src/KaezanArenaFable.Api/Meta/EnergyLedger.cs`:

```csharp
using System.Globalization;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

/// <summary>
/// Lazy-settled energy bank: energy regenerates at EnergyRegenPerMinute up to DungeonEnergyCap,
/// settled on read/spend from the wall-clock delta since the last settlement. Lives in Meta (not
/// the engine): the run itself never reads energy, so determinism is untouched.
/// </summary>
public static class EnergyLedger
{
    public static int Current(AccountState s, DateTimeOffset nowUtc)
    {
        if (s.EnergyUpdatedUtc.Length == 0) return GameConfig.DungeonEnergyCap;
        var last = DateTimeOffset.Parse(s.EnergyUpdatedUtc, CultureInfo.InvariantCulture);
        var minutes = Math.Max(0, (nowUtc - last).TotalMinutes);
        var regen = (long)(minutes * GameConfig.EnergyRegenPerMinute);
        return (int)Math.Min(GameConfig.DungeonEnergyCap, s.Energy + regen);
    }

    /// <summary>Settles regen into the state, then deducts when affordable. Always advances the
    /// settlement timestamp (so repeated failed spends don't double-count regen).</summary>
    public static bool TrySpend(AccountState s, int amount, DateTimeOffset nowUtc)
    {
        var current = Current(s, nowUtc);
        s.EnergyUpdatedUtc = nowUtc.ToString("O");
        if (current < amount)
        {
            s.Energy = current;
            return false;
        }
        s.Energy = current - amount;
        return true;
    }
}
```

- [ ] **Step 4: Ligar no orquestrador**

Em `SessionOrchestrator.cs`, substituir o stub `EnergyAvailable`:

```csharp
    private int EnergyAvailable() =>
        store.Read(s => EnergyLedger.Current(s, DateTimeOffset.UtcNow));
```

E em `CreateWorld`, antes do `factory.Create`, debitar (best-effort — a sessão continua se o plano não usa a regra de energia):

```csharp
    private GameWorld CreateWorld(StartNextRun start)
    {
        _currentWaifuId = start.WaifuId;
        store.Mutate(s => EnergyLedger.TrySpend(s, GameConfig.DungeonEnergyPerRun, DateTimeOffset.UtcNow));
        return factory.Create(start.Tier, start.WaifuId, seed: null, Engine.GameMode.Dungeon);
    }
```

No catalog (`MetaEndpoints.cs`, bloco `farm`), adicionar: `energyRegenPerMinute = GameConfig.EnergyRegenPerMinute,`.

- [ ] **Step 5: Rodar tudo**

Run: `dotnet test backend/tests/KaezanArenaFable.Api.Tests` e `dotnet build backend/src/KaezanArenaFable.Api`
Expected: PASS / limpo. (AccountState novo campo: o JSON repo desserializa contas antigas com default = cap — sem migração; se o repositório MySQL mapear colunas explícitas para AccountState, adicionar a migration EF `dotnet ef migrations add SessionEnergy` a partir de `backend/src/KaezanArenaFable.Api` — verificar `Meta/Persistence/AccountEntities.cs`: se a conta é persistida como blob JSON, nada a fazer.)

- [ ] **Step 6: Commit**

```bash
git add backend/src/KaezanArenaFable.Api backend/tests
git commit -m "feat(meta): regenerating energy bank paces idle session chaining"
```

---

## Gate de saída da Onda 3

- [ ] `git log --stat` da onda: nenhum commit toca `GameWorld.cs` / `GameWorld.Replay.cs`.
- [ ] `dotnet run --project tools/BalanceSim -- --replay-check backend/src/KaezanArenaFable.Api/.data/replays` GREEN (engine intocado).
- [ ] Sessão de 1h+ sem interação: iniciar sessão T1 com stop-loss 0/maxRuns 0, deixar rodando 1h com a aba FECHADA; `GET /api/v1/session` mostra dezenas de runs no journal, memória do processo estável (ver working set no Task Manager antes/depois).
- [ ] Reconexão retoma espectador no meio da run (fechar/reabrir aba durante uma run).
- [ ] Input manual pausa o encadeamento; Resume retoma; Stop encerra e aplica a última recompensa.
- [ ] `dotnet test` + `npx ng build` + `npm test` verdes; README atualizado (seção "Idle sessions": estratégia, regras de parada, energia).
