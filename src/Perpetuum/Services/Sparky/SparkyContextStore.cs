namespace Perpetuum.Services.Sparky;

internal sealed class SparkyContextStore
{
    public const int DefaultMaximumTurns = 8;
    public const int HardMaximumTurns = 16;
    public const int DefaultMaximumCharacters = 6000;
    public const int HardMaximumCharacters = 8000;
    public const int MinimumMaximumCharacters = 4096;
    public const int MaximumTurnCharacters = 2000;
    public const int MaximumSummaryCharacters = 700;
    public const int MaximumCandidates = 8;
    public const int MaximumCandidateCharacters = 120;
    private const int RememberedRequestIds = 32;

    private readonly object _lock = new();
    private readonly Dictionary<SparkyRequestIdentity, ContextEntry> _entries = new();
    private readonly int _capacity;
    private readonly TimeSpan _lifetime;
    private readonly int _maxTurns;
    private readonly int _maxCharacters;

    public SparkyContextStore(
        int capacity,
        TimeSpan lifetime,
        int maxTurns = DefaultMaximumTurns,
        int maxCharacters = DefaultMaximumCharacters)
    {
        _capacity = Math.Clamp(capacity, 1, 100000);
        _lifetime = TimeSpan.FromMinutes(Math.Clamp(lifetime.TotalMinutes, 1, 1440));
        _maxTurns = Math.Clamp(maxTurns - (maxTurns % 2), 2, HardMaximumTurns);
        _maxCharacters = Math.Clamp(maxCharacters, MinimumMaximumCharacters, HardMaximumCharacters);
    }

    public SparkyContextEntry? GetAndTouch(SparkyRequestIdentity identity, DateTimeOffset now)
    {
        lock (_lock)
        {
            RemoveExpired(now);
            if (!_entries.TryGetValue(identity, out var entry))
            {
                return null;
            }

            _entries[identity] = entry with { LastActivity = now };
            return Snapshot(entry);
        }
    }

    public void Set(SparkyRequestIdentity identity, string? itemDisplayName, int? languageId, DateTimeOffset now)
    {
        itemDisplayName = LimitOptional(itemDisplayName, MaximumCandidateCharacters);
        if (itemDisplayName is null && languageId is null)
        {
            return;
        }

        lock (_lock)
        {
            RemoveExpired(now);
            var entry = GetOrCreate(identity, now);
            _entries[identity] = entry with
            {
                ItemDisplayName = itemDisplayName ?? entry.ItemDisplayName,
                LanguageId = languageId ?? entry.LanguageId,
                LastActivity = now
            };
        }
    }

    public bool AppendExchange(
        SparkyRequestIdentity identity,
        Guid requestId,
        string question,
        string answer,
        SparkyConversationUpdate? update,
        int? languageId,
        DateTimeOffset now)
    {
        if (requestId == Guid.Empty || string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        lock (_lock)
        {
            RemoveExpired(now);
            var entry = GetOrCreate(identity, now);
            if (entry.AppliedRequestIds.Contains(requestId))
            {
                _entries[identity] = entry with { LastActivity = now };
                return false;
            }

            var turns = entry.Turns.ToList();
            turns.Add(new SparkyConversationTurn("user", Limit(question, MaximumTurnCharacters)));
            turns.Add(new SparkyConversationTurn("assistant", Limit(answer, MaximumTurnCharacters)));
            TrimTurns(turns);

            string? summary = entry.Summary;
            string? itemDisplayName = entry.ItemDisplayName;
            SparkyPendingClarification? pendingClarification = entry.PendingClarification;
            if (update is not null)
            {
                summary = update.Summary is null
                    ? entry.Summary
                    : LimitOptional(update.Summary, MaximumSummaryCharacters);
                itemDisplayName = LimitOptional(update.ResolvedEntity, MaximumCandidateCharacters);
                pendingClarification = SanitizePendingClarification(update.PendingClarification);
            }
            else
            {
                pendingClarification = null;
            }

            var appliedRequestIds = new Queue<Guid>(entry.AppliedRequestIds);
            appliedRequestIds.Enqueue(requestId);
            while (appliedRequestIds.Count > RememberedRequestIds)
            {
                appliedRequestIds.Dequeue();
            }

            _entries[identity] = entry with
            {
                Turns = turns.ToArray(),
                Summary = summary,
                ItemDisplayName = itemDisplayName,
                PendingClarification = pendingClarification,
                LanguageId = languageId ?? entry.LanguageId,
                LastActivity = now,
                AppliedRequestIds = appliedRequestIds.ToArray()
            };
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }

    private void TrimTurns(List<SparkyConversationTurn> turns)
    {
        while (turns.Count > _maxTurns)
        {
            turns.RemoveRange(0, Math.Min(2, turns.Count));
        }

        while (turns.Count > 2 && turns.Sum(turn => turn.Content.Length) > _maxCharacters)
        {
            turns.RemoveRange(0, 2);
        }

        if (turns.Count == 2 && turns.Sum(turn => turn.Content.Length) > _maxCharacters)
        {
            int excess = turns.Sum(turn => turn.Content.Length) - _maxCharacters;
            var first = turns[0];
            int keepLength = Math.Max(1, first.Content.Length - excess);
            turns[0] = first with { Content = first.Content[^keepLength..] };
        }
    }

    private ContextEntry GetOrCreate(SparkyRequestIdentity identity, DateTimeOffset now)
    {
        if (_entries.TryGetValue(identity, out var existing))
        {
            return existing;
        }

        if (_entries.Count >= _capacity)
        {
            var oldest = _entries.MinBy(pair => pair.Value.LastActivity).Key;
            _entries.Remove(oldest);
        }

        return new ContextEntry(null, null, [], null, null, now, []);
    }

    private static SparkyContextEntry Snapshot(ContextEntry entry) => new(
        entry.ItemDisplayName,
        entry.LanguageId,
        entry.Turns.ToArray(),
        entry.Summary,
        Clone(entry.PendingClarification));

    private static SparkyPendingClarification? SanitizePendingClarification(SparkyPendingClarification? pending)
    {
        if (pending is null)
        {
            return null;
        }

        string originalQuestion = Limit(pending.OriginalQuestion, MaximumTurnCharacters);
        string? requestedAnswerType = LimitOptional(pending.RequestedAnswerType, 80);
        var candidates = (pending.CandidateNames ?? [])
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => Limit(candidate.Trim(), MaximumCandidateCharacters))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumCandidates)
            .ToArray();
        return originalQuestion.Length == 0 || candidates.Length == 0
            ? null
            : new SparkyPendingClarification(originalQuestion, requestedAnswerType, candidates);
    }

    private static SparkyPendingClarification? Clone(SparkyPendingClarification? pending) => pending is null
        ? null
        : pending with { CandidateNames = pending.CandidateNames.ToArray() };

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[^maxLength..];

    private static string? LimitOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
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

    private sealed record ContextEntry(
        string? ItemDisplayName,
        int? LanguageId,
        IReadOnlyList<SparkyConversationTurn> Turns,
        string? Summary,
        SparkyPendingClarification? PendingClarification,
        DateTimeOffset LastActivity,
        IReadOnlyList<Guid> AppliedRequestIds);
}

internal sealed record SparkyContextEntry(
    string? ItemDisplayName,
    int? LanguageId,
    IReadOnlyList<SparkyConversationTurn> Turns,
    string? Summary,
    SparkyPendingClarification? PendingClarification);

internal sealed record SparkyConversationTurn(string Role, string Content);

internal sealed record SparkyPendingClarification(
    string OriginalQuestion,
    string? RequestedAnswerType,
    IReadOnlyList<string> CandidateNames);

internal sealed record SparkyConversationUpdate(
    string? Summary,
    string? ResolvedEntity,
    SparkyPendingClarification? PendingClarification);

internal enum SparkyCallerRank
{
    Technician,
    Agent,
    Officer,
    CEO,
    CommandDirector
}
