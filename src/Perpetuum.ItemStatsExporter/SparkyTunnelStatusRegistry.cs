namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyTunnelStatusRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, SparkyTunnelStatus> _statuses = new(StringComparer.Ordinal);

    public void Register(string name, bool enabled)
    {
        lock (_sync)
        {
            _statuses[name] = new SparkyTunnelStatus(name, enabled, enabled ? "starting" : "disabled");
        }
    }

    public void Set(string name, string state)
    {
        lock (_sync)
        {
            if (!_statuses.TryGetValue(name, out var current) || !current.Enabled)
            {
                return;
            }

            _statuses[name] = current with { State = state };
        }
    }

    public IReadOnlyList<SparkyTunnelStatus> GetSnapshot()
    {
        lock (_sync)
        {
            return _statuses.Values.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        }
    }
}

internal sealed record SparkyTunnelStatus(string Name, bool Enabled, string State);
