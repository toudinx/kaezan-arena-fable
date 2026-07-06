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
        Assert.Equal(24, stats.Max());
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
