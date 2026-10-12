namespace Perpetuum.Services.Sparky;

internal sealed class SparkyContextStore
{
    private readonly object _lock = new();
    private readonly Dictionary<SparkyRequestIdentity, ContextEntry> _entries = new();
    private readonly int _capacity;
    private readonly TimeSpan _lifetime;

    public SparkyContextStore(int capacity, TimeSpan lifetime)
    {
        _capacity = capacity;
        _lifetime = lifetime;
    }

    public string? GetAndTouch(SparkyRequestIdentity identity, DateTimeOffset now)
    {
        lock (_lock)
        {
            RemoveExpired(now);
            if (!_entries.TryGetValue(identity, out var entry))
            {
                return null;
            }

            _entries[identity] = entry with { LastActivity = now };
            return entry.ItemDisplayName;
        }
    }

    public void Set(SparkyRequestIdentity identity, string itemDisplayName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(itemDisplayName))
        {
            return;
        }

        lock (_lock)
        {
            RemoveExpired(now);
            if (!_entries.ContainsKey(identity) && _entries.Count >= _capacity)
            {
                var oldest = _entries.MinBy(pair => pair.Value.LastActivity).Key;
                _entries.Remove(oldest);
            }

            _entries[identity] = new ContextEntry(itemDisplayName, now);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var identity in _entries
                     .Where(pair => now - pair.Value.LastActivity >= _lifetime)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _entries.Remove(identity);
        }
    }

    private sealed record ContextEntry(string ItemDisplayName, DateTimeOffset LastActivity);
}
