namespace KaezanArenaFable.Api.Engine;

/// <summary>Deterministic xorshift128+ RNG so a seed reproduces a run exactly.</summary>
public sealed class Rng
{
    private ulong _s0, _s1;

    public Rng(ulong seed)
    {
        // splitmix64 to spread the seed
        _s0 = Mix(ref seed);
        _s1 = Mix(ref seed);
        if ((_s0 | _s1) == 0) _s1 = 0x9E3779B97F4A7C15UL;
    }

    private static ulong Mix(ref ulong z)
    {
        z += 0x9E3779B97F4A7C15UL;
        var x = z;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    public ulong NextULong()
    {
        var x = _s0;
        var y = _s1;
        _s0 = y;
        x ^= x << 23;
        _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
        return _s1 + y;
    }

    /// <summary>[0, max)</summary>
    public int Next(int max) => max <= 0 ? 0 : (int)(NextULong() % (uint)max);

    /// <summary>[min, max] inclusive</summary>
    public int Range(int min, int max) => max <= min ? min : min + Next(max - min + 1);

    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double probability) => NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> list) => list[Next(list.Count)];

    public void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
