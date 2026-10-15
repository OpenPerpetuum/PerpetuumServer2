using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyLiveKnowledgeCatalog
{
    private readonly SparkyLiveKnowledgeStore _store;
    private readonly Func<SparkyLiveKnowledgeProfileState, SparkyKnowledgeSupplement> _buildSupplement;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private ImmutableDictionary<SparkyApiProfile, CatalogEntry> _entries = ImmutableDictionary<SparkyApiProfile, CatalogEntry>.Empty;

    public SparkyLiveKnowledgeCatalog(
        SparkyLiveKnowledgeStore store,
        Func<SparkyLiveKnowledgeProfileState, SparkyKnowledgeSupplement> buildSupplement)
    {
        _store = store;
        _buildSupplement = buildSupplement;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var entries = ImmutableDictionary<SparkyApiProfile, CatalogEntry>.Empty;
            foreach (var profile in Enum.GetValues<SparkyApiProfile>())
            {
                var state = await _store.LoadProfileAsync(profile, cancellationToken).ConfigureAwait(false);
                entries = entries.Add(profile, CreateEntry(state, previous: null));
            }

            Interlocked.Exchange(ref _entries, entries);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<SparkyLiveKnowledgeApplyResult> ApplyAsync(
        SparkyApiProfile profile,
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = update.Operation switch
            {
                "replace-current" => await _store.ReplaceCurrentAsync(profile, update, cancellationToken).ConfigureAwait(false),
                "finalize-season" when update.Finalization is not null => await _store.FinalizeSeasonAsync(
                    profile, update.Finalization.SeasonId, update, cancellationToken).ConfigureAwait(false),
                _ => throw new InvalidDataException("The live knowledge update cannot be applied by this operation.")
            };

            if (result.Status is "applied" or "refreshed" or "unchanged" or "finalized" or "duplicate")
            {
                var state = await _store.LoadProfileAsync(profile, cancellationToken).ConfigureAwait(false);
                var currentEntries = Volatile.Read(ref _entries);
                currentEntries.TryGetValue(profile, out var previous);
                Publish(profile, CreateEntry(state, previous));
            }

            return result;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<SparkyLiveKnowledgeApplyResult> RefreshCurrentAsync(
        SparkyApiProfile profile,
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _store.RefreshCurrentAsync(profile, update, cancellationToken).ConfigureAwait(false);
            if (result.Status is "refreshed" or "unchanged")
            {
                var state = await _store.LoadProfileAsync(profile, cancellationToken).ConfigureAwait(false);
                var currentEntries = Volatile.Read(ref _entries);
                currentEntries.TryGetValue(profile, out var previous);
                Publish(profile, CreateEntry(state, previous));
            }

            return result;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public SparkyLiveKnowledgeReadView GetReadView(
        SparkyApiProfile profile,
        DateTimeOffset nowUtc,
        TimeSpan maximumAge)
    {
        var entries = Volatile.Read(ref _entries);
        if (!entries.TryGetValue(profile, out var entry))
        {
            var empty = new SparkyLiveKnowledgeProfileState(null, []);
            return new SparkyLiveKnowledgeReadView(
                _buildSupplement(empty),
                new HashSet<string>(StringComparer.Ordinal),
                new SparkyLiveKnowledgeDiagnostics(null, null, null, null, "missing", 0),
                empty);
        }

        var current = entry.State.Current;
        double? ageMinutes = current is null
            ? null
            : Math.Max(0, (nowUtc.ToUniversalTime() - current.CapturedAtUtc).TotalMinutes);
        string freshness = current is null
            ? "missing"
            : ageMinutes <= maximumAge.TotalMinutes ? "fresh" : "stale";
        var excluded = freshness == "stale"
            ? new HashSet<string>(["live-season-current"], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        string? provider = current?.Provider ?? (entry.State.FinalizedSeasons.Count > 0 ? "seasons" : null);
        int? schemaVersion = current?.SchemaVersion ?? (provider is null ? null : SparkyLiveKnowledgeValidator.SchemaVersion);
        return new SparkyLiveKnowledgeReadView(
            entry.Supplement,
            excluded,
            new SparkyLiveKnowledgeDiagnostics(
                provider,
                schemaVersion,
                current?.ContentHash,
                ageMinutes,
                freshness,
                entry.State.FinalizedSeasons.Count),
            entry.State);
    }

    private CatalogEntry CreateEntry(SparkyLiveKnowledgeProfileState state, CatalogEntry? previous)
    {
        string signature = ComputeSignature(state);
        if (previous is not null && string.Equals(previous.ContentSignature, signature, StringComparison.Ordinal))
        {
            return new CatalogEntry(state, signature, previous.Supplement);
        }

        return new CatalogEntry(state, signature, _buildSupplement(state));
    }

    private void Publish(SparkyApiProfile profile, CatalogEntry entry)
    {
        var entries = Volatile.Read(ref _entries);
        Interlocked.Exchange(ref _entries, entries.SetItem(profile, entry));
    }

    private static string ComputeSignature(SparkyLiveKnowledgeProfileState state)
    {
        var builder = new StringBuilder();
        if (state.Current is { } current)
        {
            builder.Append("current:").Append(current.ContentHash).Append(';');
        }

        foreach (var final in state.FinalizedSeasons.OrderBy(value => value.SeasonId))
        {
            builder.Append(final.SeasonId).Append(':').Append(final.Revision).Append(':').Append(final.ContentHash).Append(';');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private sealed record CatalogEntry(
        SparkyLiveKnowledgeProfileState State,
        string ContentSignature,
        SparkyKnowledgeSupplement Supplement);
}
