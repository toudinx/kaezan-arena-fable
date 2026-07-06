namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// Fixed-size ring of duration samples with percentile readout.
/// Operational instrumentation only; never used by the simulation.
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
