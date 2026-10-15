using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Perpetuum.Services.Sparky;

namespace Perpetuum.ItemStatsExporter;

internal interface ISparkyProfileRepository
{
    Task<IReadOnlyList<SparkyProfileRevisionRecord>> GetProfileRevisionsAsync(SparkyApiProfile profile, CancellationToken cancellationToken = default);
    Task SaveProfileRevisionAsync(SparkyProfileRevisionRecord revision, string actor, string? reason, CancellationToken cancellationToken = default);
    Task<bool> ActivateProfileRevisionAsync(SparkyApiProfile profile, string revisionId, string actor, string action, string? sourceRevisionId, string? reason, IReadOnlyList<string> changedFields, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SparkyProfileAuditRecord>> GetProfileAuditAsync(SparkyApiProfile profile, int limit = 100, CancellationToken cancellationToken = default);
}

internal sealed record SparkyProfileRevisionSummary(
    string RevisionId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ActivatedAtUtc,
    string BundleVersion,
    bool IsActive,
    bool IsDraft);

internal sealed record SparkyProfileDetails(
    SparkyProfileSummary Summary,
    SparkyProfileDraft? ActiveConfiguration,
    string? DraftRevisionId,
    SparkyProfileDraft? DraftConfiguration,
    IReadOnlyList<SparkyProfileRevisionSummary> Revisions,
    IReadOnlyList<SparkyProfileAuditRecord> Audit);

internal sealed record SparkyProfileOperationResult(
    bool Success,
    string? RevisionId = null,
    IReadOnlyList<string>? Errors = null,
    string? ErrorCode = null);

internal sealed record SparkyTraceRetentionPolicy(int ContentRetentionDays, int MetadataRetentionDays);

internal sealed class SparkyProfileManager
{
    private const string AdminActor = "local-admin";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    static SparkyProfileManager() => JsonOptions.Converters.Add(new JsonStringEnumConverter());

    private readonly SparkyTraceStore? _store;
    private readonly ISparkyBundleCatalog _bundles;
    private readonly SparkyServiceOptions _defaults;
    private readonly SparkyKnowledgeBundle _legacyBundle;
    private readonly IReadOnlySet<int> _languageIds;
    private readonly ConcurrentDictionary<SparkyApiProfile, SparkyProfileSnapshot> _active = new();
    private readonly ConcurrentDictionary<SparkyApiProfile, SparkyProfileRevisionRecord> _drafts = new();
    private readonly Dictionary<SparkyApiProfile, List<SparkyProfileRevisionRecord>> _memoryRevisions = new();
    private readonly Dictionary<SparkyApiProfile, List<SparkyProfileAuditRecord>> _memoryAudit = new();
    private readonly SemaphoreSlim _mutation = new(1, 1);

    public SparkyProfileManager(
        SparkyTraceStore? store,
        ISparkyBundleCatalog bundles,
        SparkyServiceOptions defaults,
        SparkyKnowledgeBundle legacyBundle)
    {
        _store = store;
        _bundles = bundles;
        _defaults = defaults;
        _legacyBundle = legacyBundle;
        _languageIds = legacyBundle.Languages.Select(language => language.LanguageId).ToHashSet();
        _memoryRevisions[SparkyApiProfile.Dev] = [];
        _memoryRevisions[SparkyApiProfile.Live] = [];
        _memoryAudit[SparkyApiProfile.Dev] = [];
        _memoryAudit[SparkyApiProfile.Live] = [];
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _bundles.RegisterExisting(_legacyBundle.Manifest.BundleVersion, _defaults.ExportPath, _legacyBundle);
        foreach (SparkyApiProfile profile in Enum.GetValues<SparkyApiProfile>())
        {
            if (profile == SparkyApiProfile.Live && string.IsNullOrWhiteSpace(_defaults.LiveApiToken))
                continue;

            IReadOnlyList<SparkyProfileRevisionRecord> revisions = _store is null
                ? _memoryRevisions[profile]
                : await _store.GetProfileRevisionsAsync(profile, cancellationToken).ConfigureAwait(false);
            _memoryRevisions[profile].Clear();
            _memoryRevisions[profile].AddRange(revisions);

            SparkyProfileRevisionRecord? active = revisions
                .Where(revision => revision.IsActive)
                .OrderByDescending(revision => revision.ActivatedAtUtc)
                .FirstOrDefault();
            SparkyProfileRevisionRecord? draft = revisions
                .Where(revision => revision.IsDraft)
                .OrderByDescending(revision => revision.CreatedAtUtc)
                .FirstOrDefault();
            if (draft is not null)
                _drafts[profile] = draft;

            if (active is not null && TryCreateSnapshot(active, out var snapshot))
            {
                _active[profile] = snapshot;
                continue;
            }

            // Do not replace a persisted but unreadable active revision with a new default.
            // Keeping the profile unavailable is safer than silently discarding its configuration.
            if (active is not null)
                continue;

            if (profile == SparkyApiProfile.Live)
                continue;

            SparkyProfileDraft configuration = SparkyProfileDraft.CreateDefault(
                _defaults.ModelId,
                _legacyBundle.Manifest.BundleVersion,
                _defaults.MaxAnswerCharacters,
                _defaults.MaxCompletionTokens,
                checked((int)_defaults.InferenceTimeout.TotalSeconds),
                _defaults.MaxConcurrentRequests,
                _defaults.DevTraceCaptureContent,
                _defaults.TraceContentRetentionDays,
                _defaults.TraceMetadataRetentionDays);
            var seed = NewRevision(profile, configuration, isDraft: true);
            await PersistDraftAsync(seed, "Initialize legacy Dev profile", cancellationToken).ConfigureAwait(false);
            _drafts[profile] = seed;
            _ = await ActivateRevisionCoreAsync(profile, seed, "initialize", null, "Initialize the existing single-profile configuration as Dev.", cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public bool TryGetSnapshot(SparkyApiProfile profile, out SparkyProfileSnapshot snapshot) =>
        _active.TryGetValue(profile, out snapshot!);

    public async Task<SparkyTraceRetentionPolicy> GetTraceRetentionPolicyAsync(
        SparkyApiProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (_active.TryGetValue(profile, out var active))
        {
            return new SparkyTraceRetentionPolicy(
                active.Configuration.ContentRetentionDays,
                active.Configuration.MetadataRetentionDays);
        }

        IReadOnlyList<SparkyProfileRevisionRecord> revisions = await GetRevisionsAsync(profile, cancellationToken).ConfigureAwait(false);
        SparkyProfileRevisionRecord? persistedActive = revisions
            .Where(revision => revision.IsActive)
            .OrderByDescending(revision => revision.ActivatedAtUtc)
            .FirstOrDefault();
        if (persistedActive is not null)
        {
            try
            {
                var configuration = DeserializeDraft(persistedActive.ConfigurationJson);
                if (IsValidRetention(configuration.ContentRetentionDays, configuration.MetadataRetentionDays))
                {
                    return new SparkyTraceRetentionPolicy(
                        configuration.ContentRetentionDays,
                        configuration.MetadataRetentionDays);
                }
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                // A corrupted or older profile must not disable cleanup; fall back to service defaults.
            }
        }

        return new SparkyTraceRetentionPolicy(_defaults.TraceContentRetentionDays, _defaults.TraceMetadataRetentionDays);
    }

    public async Task<SparkyProfileSnapshot?> GetDraftSnapshotAsync(
        SparkyApiProfile profile,
        string revisionId,
        CancellationToken cancellationToken)
    {
        var revisions = await GetRevisionsAsync(profile, cancellationToken).ConfigureAwait(false);
        var revision = revisions.FirstOrDefault(candidate => candidate.IsDraft &&
            string.Equals(candidate.RevisionId, revisionId, StringComparison.Ordinal));
        return revision is not null && TryCreateSnapshot(revision, out var resolved) ? resolved : null;
    }

    public SparkyProfileSummary[] GetSummaries() => Enum.GetValues<SparkyApiProfile>()
        .Select(profile =>
        {
            _active.TryGetValue(profile, out var active);
            _drafts.TryGetValue(profile, out var draft);
            bool enabled = profile == SparkyApiProfile.Dev || !string.IsNullOrWhiteSpace(_defaults.LiveApiToken);
            return new SparkyProfileSummary(
                profile.ToString(),
                enabled,
                active?.RevisionId,
                draft?.RevisionId,
                active?.BundleVersion,
                active?.Configuration.CaptureContent);
        })
        .ToArray();

    public async Task<SparkyProfileDetails> GetDetailsAsync(
        SparkyApiProfile profile,
        CancellationToken cancellationToken = default)
    {
        _active.TryGetValue(profile, out var active);
        _drafts.TryGetValue(profile, out var draft);
        IReadOnlyList<SparkyProfileRevisionRecord> revisions = await GetRevisionsAsync(profile, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SparkyProfileAuditRecord> audit = _store is null
            ? _memoryAudit[profile].OrderByDescending(entry => entry.CreatedAtUtc).Take(100).ToArray()
            : await _store.GetProfileAuditAsync(profile, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new SparkyProfileDetails(
            GetSummaries().Single(summary => summary.Profile == profile.ToString()),
            active?.Configuration,
            draft?.RevisionId,
            draft is null ? null : DeserializeDraft(draft.ConfigurationJson),
            revisions.Select(revision => new SparkyProfileRevisionSummary(
                revision.RevisionId, revision.CreatedAtUtc, revision.ActivatedAtUtc,
                revision.BundleVersion, revision.IsActive, revision.IsDraft)).Take(100).ToArray(),
            audit);
    }

    public IReadOnlyList<string> Validate(SparkyApiProfile profile, SparkyProfileDraft? configuration)
    {
        var errors = SparkyProfileValidator.Validate(configuration, _languageIds, _defaults.MaxConcurrentRequests).ToList();
        if (configuration is not null)
        {
            try
            {
                _ = _bundles.GetRequired(configuration.BundleVersion);
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                errors.Add("BundleVersion is not present and validated in the local bundle catalog.");
            }

            if (profile == SparkyApiProfile.Live && string.IsNullOrWhiteSpace(_defaults.LiveApiToken))
                errors.Add("Live is disabled until a separate Live API token is configured.");
        }

        return errors;
    }

    public async Task<SparkyProfileOperationResult> SaveDraftAsync(
        SparkyApiProfile profile,
        SparkyProfileDraft configuration,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!IsProfileEnabled(profile))
            return new(false, ErrorCode: "profile_disabled");
        if (profile == SparkyApiProfile.Live)
            return new(false, ErrorCode: "live_requires_promotion");
        if (reason?.Length > 500)
            return new(false, Errors: ["Reason may contain at most 500 characters."]);

        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var revision = NewRevision(profile, configuration, isDraft: true);
            await PersistDraftAsync(revision, reason, cancellationToken).ConfigureAwait(false);
            _drafts[profile] = revision;
            return new(true, revision.RevisionId);
        }
        finally
        {
            _mutation.Release();
        }
    }

    public async Task<SparkyProfileOperationResult> ActivateAsync(
        SparkyApiProfile profile,
        string revisionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!IsProfileEnabled(profile))
            return new(false, ErrorCode: "profile_disabled");
        if (profile == SparkyApiProfile.Live)
            return new(false, ErrorCode: "live_requires_promotion");
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var revisions = await GetRevisionsAsync(profile, cancellationToken).ConfigureAwait(false);
            SparkyProfileRevisionRecord? revision = revisions.FirstOrDefault(item => item.RevisionId == revisionId && item.IsDraft);
            if (revision is null)
                return new(false, ErrorCode: "draft_not_found");
            var configuration = DeserializeDraft(revision.ConfigurationJson);
            var errors = Validate(profile, configuration);
            if (errors.Count > 0)
                return new(false, revisionId, errors);
            if (!await ActivateRevisionCoreAsync(profile, revision, "activate", null, reason, cancellationToken).ConfigureAwait(false))
                return new(false, ErrorCode: "draft_not_found");
            return new(true, revision.RevisionId);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return new(false, ErrorCode: "invalid_profile");
        }
        finally
        {
            _mutation.Release();
        }
    }

    public async Task<SparkyProfileOperationResult> PromoteDevToLiveAsync(
        string sourceRevisionId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_defaults.LiveApiToken))
            return new(false, ErrorCode: "profile_disabled");
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var devRevisions = await GetRevisionsAsync(SparkyApiProfile.Dev, cancellationToken).ConfigureAwait(false);
            var source = devRevisions.FirstOrDefault(revision => revision.RevisionId == sourceRevisionId);
            if (source is null)
                return new(false, ErrorCode: "source_revision_not_found");
            var configuration = DeserializeDraft(source.ConfigurationJson);
            var errors = Validate(SparkyApiProfile.Live, configuration);
            if (errors.Count > 0)
                return new(false, sourceRevisionId, errors);

            var liveRevision = NewRevision(SparkyApiProfile.Live, configuration, isDraft: true);
            await PersistDraftAsync(liveRevision, reason, cancellationToken).ConfigureAwait(false);
            _drafts[SparkyApiProfile.Live] = liveRevision;
            if (!await ActivateRevisionCoreAsync(SparkyApiProfile.Live, liveRevision, "promote", sourceRevisionId, reason, cancellationToken).ConfigureAwait(false))
                return new(false, ErrorCode: "activation_failed");
            return new(true, liveRevision.RevisionId);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return new(false, ErrorCode: "invalid_profile");
        }
        finally
        {
            _mutation.Release();
        }
    }

    public async Task<SparkyProfileOperationResult> RollbackAsync(
        SparkyApiProfile profile,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!IsProfileEnabled(profile))
            return new(false, ErrorCode: "profile_disabled");
        await _mutation.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previous = (await GetRevisionsAsync(profile, cancellationToken).ConfigureAwait(false))
                .Where(revision => revision.ActivatedAtUtc is not null)
                .OrderByDescending(revision => revision.ActivatedAtUtc)
                .Skip(1)
                .FirstOrDefault();
            if (previous is null)
                return new(false, ErrorCode: "no_rollback_revision");
            var configuration = DeserializeDraft(previous.ConfigurationJson);
            var errors = Validate(profile, configuration);
            if (errors.Count > 0)
                return new(false, previous.RevisionId, errors);
            var rollback = NewRevision(profile, configuration, isDraft: true);
            await PersistDraftAsync(rollback, reason, cancellationToken).ConfigureAwait(false);
            _drafts[profile] = rollback;
            if (!await ActivateRevisionCoreAsync(profile, rollback, "rollback", previous.RevisionId, reason, cancellationToken).ConfigureAwait(false))
                return new(false, ErrorCode: "activation_failed");
            return new(true, rollback.RevisionId);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return new(false, ErrorCode: "invalid_profile");
        }
        finally
        {
            _mutation.Release();
        }
    }

    public async Task<SparkyBundleInfo> StageBundleAsync(Stream input, CancellationToken cancellationToken) =>
        await _bundles.StageAsync(input, cancellationToken).ConfigureAwait(false);

    public IReadOnlyList<SparkyBundleInfo> GetBundles() => _bundles.List();

    private bool IsProfileEnabled(SparkyApiProfile profile) =>
        profile == SparkyApiProfile.Dev || !string.IsNullOrWhiteSpace(_defaults.LiveApiToken);

    private static bool IsValidRetention(int contentDays, int metadataDays) =>
        contentDays is >= 1 and <= 3650 && metadataDays is >= 1 and <= 3650 && metadataDays >= contentDays;

    private async Task<IReadOnlyList<SparkyProfileRevisionRecord>> GetRevisionsAsync(
        SparkyApiProfile profile,
        CancellationToken cancellationToken)
    {
        if (_store is not null)
            return await _store.GetProfileRevisionsAsync(profile, cancellationToken).ConfigureAwait(false);
        return _memoryRevisions[profile].OrderByDescending(revision => revision.CreatedAtUtc).ToArray();
    }

    private async Task PersistDraftAsync(
        SparkyProfileRevisionRecord revision,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (_store is not null)
            await _store.SaveProfileRevisionAsync(revision, AdminActor, reason, cancellationToken).ConfigureAwait(false);
        _memoryRevisions[revision.Profile].Add(revision);
        _memoryAudit[revision.Profile].Add(new SparkyProfileAuditRecord(
            Guid.NewGuid().ToString("N"), revision.Profile, AdminActor, "draft_saved", revision.RevisionId,
            "success", ["draft"], reason, DateTimeOffset.UtcNow));
    }

    private async Task<bool> ActivateRevisionCoreAsync(
        SparkyApiProfile profile,
        SparkyProfileRevisionRecord revision,
        string action,
        string? sourceRevisionId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var configuration = DeserializeDraft(revision.ConfigurationJson);
        var bundle = _bundles.GetRequired(configuration.BundleVersion);
        IReadOnlyList<string> changedFields = GetChangedFields(
            configuration,
            _active.TryGetValue(profile, out var previousSnapshot) ? previousSnapshot.Configuration : null);
        if (_store is not null && !await _store.ActivateProfileRevisionAsync(
                profile, revision.RevisionId, AdminActor, action, sourceRevisionId, reason,
                changedFields, cancellationToken).ConfigureAwait(false))
            return false;

        DateTimeOffset activatedAt = DateTimeOffset.UtcNow;
        var activeRecord = revision with { IsActive = true, IsDraft = false, ActivatedAtUtc = activatedAt };
        var existing = _memoryRevisions[profile].FindIndex(item => item.RevisionId == revision.RevisionId);
        if (existing >= 0)
            _memoryRevisions[profile][existing] = activeRecord;
        else
            _memoryRevisions[profile].Add(activeRecord);
        for (int index = 0; index < _memoryRevisions[profile].Count; index++)
        {
            if (_memoryRevisions[profile][index].RevisionId != revision.RevisionId && _memoryRevisions[profile][index].IsActive)
                _memoryRevisions[profile][index] = _memoryRevisions[profile][index] with { IsActive = false };
        }

        _memoryAudit[profile].Add(new SparkyProfileAuditRecord(
            Guid.NewGuid().ToString("N"), profile, AdminActor, action, sourceRevisionId,
            "success", changedFields, reason, activatedAt));
        _active[profile] = new SparkyProfileSnapshot(profile, revision.RevisionId, activatedAt, configuration, bundle);
        _drafts.TryRemove(profile, out _);
        return true;
    }

    private bool TryCreateSnapshot(SparkyProfileRevisionRecord revision, out SparkyProfileSnapshot snapshot)
    {
        snapshot = null!;
        try
        {
            var configuration = DeserializeDraft(revision.ConfigurationJson);
            var errors = SparkyProfileValidator.Validate(configuration, _languageIds, _defaults.MaxConcurrentRequests);
            if (errors.Count > 0)
                return false;
            var bundle = _bundles.GetRequired(configuration.BundleVersion);
            snapshot = new SparkyProfileSnapshot(
                revision.Profile,
                revision.RevisionId,
                revision.ActivatedAtUtc ?? revision.CreatedAtUtc,
                configuration,
                bundle);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException)
        {
            return false;
        }
    }

    private static SparkyProfileRevisionRecord NewRevision(
        SparkyApiProfile profile,
        SparkyProfileDraft configuration,
        bool isDraft) => new(
            profile,
            "rev-" + Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            null,
            JsonSerializer.Serialize(configuration, JsonOptions),
            configuration.BundleVersion,
            IsActive: false,
            IsDraft: isDraft);

    internal static SparkyProfileDraft DeserializeDraft(string json)
    {
        var configuration = JsonSerializer.Deserialize<SparkyProfileDraft>(json, JsonOptions)
            ?? throw new InvalidDataException("The Sparky profile configuration is empty.");
        configuration = configuration with { LiveKnowledge = configuration.LiveKnowledge ?? new SparkyLiveKnowledgeSettings() };
        return configuration.SchemaVersion switch
        {
            1 => configuration with { SchemaVersion = 2 },
            2 => configuration,
            _ => throw new InvalidDataException("The Sparky profile schema version is unsupported.")
        };
    }

    private static IReadOnlyList<string> GetChangedFields(
        SparkyProfileDraft configuration,
        SparkyProfileDraft? previous) =>
        previous is null
            ? [
                "modelId", "personaStyle", "routeAliases", "retrieval", "answerLimits",
                "inferenceLimits", "conversationContext", "answerModelTuning", "liveKnowledge", "bundleVersion", "contentCapture", "retention"
            ]
            : GetDifferentFields(configuration, previous);

    private static IReadOnlyList<string> GetDifferentFields(
        SparkyProfileDraft current,
        SparkyProfileDraft previous)
    {
        var changed = new List<string>(11);
        if (!string.Equals(current.ModelId, previous.ModelId, StringComparison.Ordinal))
            changed.Add("modelId");
        if (!string.Equals(current.PersonaStyle, previous.PersonaStyle, StringComparison.Ordinal))
            changed.Add("personaStyle");
        if (!JsonEqual(current.RouteAliases, previous.RouteAliases))
            changed.Add("routeAliases");
        if (!JsonEqual(current.Retrieval, previous.Retrieval))
            changed.Add("retrieval");
        if (current.MaxAnswerCharacters != previous.MaxAnswerCharacters)
            changed.Add("answerLimits");
        if (current.MaxCompletionTokens != previous.MaxCompletionTokens ||
            current.InferenceTimeoutSeconds != previous.InferenceTimeoutSeconds ||
            current.MaxConcurrentRequests != previous.MaxConcurrentRequests)
            changed.Add("inferenceLimits");
        if (current.ConversationHistoryTurns != previous.ConversationHistoryTurns ||
            current.ConversationHistoryCharacters != previous.ConversationHistoryCharacters)
            changed.Add("conversationContext");
        if (current.AnswerTemperature != previous.AnswerTemperature || current.AnswerTopP != previous.AnswerTopP)
            changed.Add("answerModelTuning");
        if (!JsonEqual(current.LiveKnowledge, previous.LiveKnowledge))
            changed.Add("liveKnowledge");
        if (!string.Equals(current.BundleVersion, previous.BundleVersion, StringComparison.Ordinal))
            changed.Add("bundleVersion");
        if (current.CaptureContent != previous.CaptureContent)
            changed.Add("contentCapture");
        if (current.ContentRetentionDays != previous.ContentRetentionDays ||
            current.MetadataRetentionDays != previous.MetadataRetentionDays)
            changed.Add("retention");
        return changed;
    }

    private static bool JsonEqual<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonOptions),
        JsonSerializer.Serialize(right, JsonOptions),
        StringComparison.Ordinal);
}
