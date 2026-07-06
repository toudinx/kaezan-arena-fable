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
