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
