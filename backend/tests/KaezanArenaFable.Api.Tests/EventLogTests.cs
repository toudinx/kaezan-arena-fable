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
