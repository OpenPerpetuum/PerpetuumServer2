using System.Collections.Concurrent;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyConcurrencyLimiter
{
    private readonly ConcurrentDictionary<SparkyApiProfile, Counter> _profiles = new();
    private int _globalActive;

    public bool TryAcquire(SparkyApiProfile profile, int profileLimit, int globalLimit)
    {
        if (!TryIncrement(ref _globalActive, globalLimit))
            return false;

        var counter = _profiles.GetOrAdd(profile, static _ => new Counter());
        if (TryIncrement(ref counter.Active, profileLimit))
            return true;

        Interlocked.Decrement(ref _globalActive);
        return false;
    }

    public void Release(SparkyApiProfile profile)
    {
        if (!_profiles.TryGetValue(profile, out var counter) || Interlocked.Decrement(ref counter.Active) < 0)
            throw new InvalidOperationException("A Sparky profile concurrency slot was released more than once.");
        if (Interlocked.Decrement(ref _globalActive) < 0)
            throw new InvalidOperationException("A Sparky global concurrency slot was released more than once.");
    }

    private static bool TryIncrement(ref int value, int limit)
    {
        if (limit < 1)
            return false;
        while (true)
        {
            int current = Volatile.Read(ref value);
            if (current >= limit)
                return false;
            if (Interlocked.CompareExchange(ref value, current + 1, current) == current)
                return true;
        }
    }

    private sealed class Counter
    {
        public int Active;
    }
}
