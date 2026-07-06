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
