using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perpetuum.Services.Sparky;

internal enum SparkyLiveKnowledgeOutboxAppendDisposition
{
    Added,
    Duplicate,
    CapacityExceeded
}

internal sealed class SparkyLiveKnowledgeOutbox
{
    public const int MaximumFileBytes = 2 * 1024 * 1024;
    public const int MaximumFinalizations = 256;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 12,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly string _path;
    private readonly int _maxBytes;
    private readonly int _maxRecords;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<SparkyLiveKnowledgeUpdate> _pending = [];
    private bool _initialized;

    public SparkyLiveKnowledgeOutbox(
        string path,
        int maxBytes = MaximumFileBytes,
        int maxRecords = MaximumFinalizations)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An outbox path is required.", nameof(path));
        if (maxBytes < 1 || maxBytes > MaximumFileBytes || maxRecords is < 1 or > MaximumFinalizations)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));

        _path = Path.GetFullPath(path);
        _maxBytes = maxBytes;
        _maxRecords = maxRecords;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            if (!File.Exists(_path))
            {
                _initialized = true;
                return;
            }

            var info = new FileInfo(_path);
            if (info.Length > _maxBytes)
                throw new InvalidDataException("The Sparky season-finalization outbox exceeds its configured size limit.");

            var pending = new List<SparkyLiveKnowledgeUpdate>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in await File.ReadAllLinesAsync(_path, Encoding.UTF8, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                SparkyLiveKnowledgeUpdate update;
                try
                {
                    update = JsonSerializer.Deserialize<SparkyLiveKnowledgeUpdate>(line, JsonOptions)
                        ?? throw new JsonException("Empty outbox entry.");
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException("The Sparky season-finalization outbox contains an invalid entry.", exception);
                }

                if (!IsValidFinalization(update))
                    throw new InvalidDataException("The Sparky season-finalization outbox contains an invalid finalization.");

                string key = GetKey(update);
                if (keys.Add(key))
                    pending.Add(update);
            }

            if (pending.Count > _maxRecords)
                throw new InvalidDataException("The Sparky season-finalization outbox exceeds its finalization limit.");

            _pending = pending;
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SparkyLiveKnowledgeOutboxAppendDisposition> AppendAsync(
        SparkyLiveKnowledgeUpdate update,
        CancellationToken cancellationToken)
    {
        if (!IsValidFinalization(update))
            throw new InvalidDataException("Only a bounded, profile-scoped season finalization can enter the Sparky outbox.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            string key = GetKey(update);
            if (_pending.Any(pending => string.Equals(GetKey(pending), key, StringComparison.Ordinal)))
                return SparkyLiveKnowledgeOutboxAppendDisposition.Duplicate;
            if (_pending.Count >= _maxRecords)
                return SparkyLiveKnowledgeOutboxAppendDisposition.CapacityExceeded;

            byte[] line = JsonSerializer.SerializeToUtf8Bytes(update, JsonOptions);
            long existingLength = File.Exists(_path) ? new FileInfo(_path).Length : 0;
            long newLength = existingLength + line.Length + Encoding.UTF8.GetByteCount(Environment.NewLine);
            if (newLength > _maxBytes)
                return SparkyLiveKnowledgeOutboxAppendDisposition.CapacityExceeded;

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await using (var stream = new FileStream(
                _path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine), cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            _pending.Add(update);
            return SparkyLiveKnowledgeOutboxAppendDisposition.Added;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<SparkyLiveKnowledgeUpdate>> ReadPendingAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            return _pending.ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> AcknowledgeAsync(SparkyLiveKnowledgeUpdate update, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            string key = GetKey(update);
            int index = _pending.FindIndex(pending => string.Equals(GetKey(pending), key, StringComparison.Ordinal));
            if (index < 0)
                return false;

            var remaining = _pending.Where((_, pendingIndex) => pendingIndex != index).ToList();
            await RewriteAtomicallyAsync(remaining, cancellationToken).ConfigureAwait(false);
            _pending = remaining;
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RewriteAtomicallyAsync(
        IReadOnlyList<SparkyLiveKnowledgeUpdate> updates,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string temporaryPath = _path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                foreach (var update in updates)
                {
                    byte[] line = JsonSerializer.SerializeToUtf8Bytes(update, JsonOptions);
                    await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(Environment.NewLine), cancellationToken).ConfigureAwait(false);
                }

                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (new FileInfo(temporaryPath).Length > _maxBytes)
                throw new InvalidDataException("The rewritten Sparky season-finalization outbox exceeds its size limit.");
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool IsValidFinalization(SparkyLiveKnowledgeUpdate? update)
    {
        if (update is null || update.SchemaVersion != 1 || update.Provider != "seasons" ||
            update.Profile is not ("dev" or "live") || update.Operation != "finalize-season" ||
            update.Current is not null || update.Finalization is not { } finalization ||
            update.CapturedAtUtc.Offset != TimeSpan.Zero || finalization.SeasonId <= 0 ||
            string.IsNullOrWhiteSpace(finalization.Name) || finalization.Name.Length > 120 ||
            finalization.Name.Any(char.IsControl) || finalization.EndTimeUtc.Offset != TimeSpan.Zero ||
            finalization.TopTen is null || finalization.TopTen.Count > 10)
        {
            return false;
        }

        for (int index = 0; index < finalization.TopTen.Count; index++)
        {
            var standing = finalization.TopTen[index];
            if (standing is null || standing.Rank != index + 1 ||
                string.IsNullOrWhiteSpace(standing.DisplayName) || standing.DisplayName.Length > 100 ||
                standing.DisplayName.Any(char.IsControl) || !double.IsFinite(standing.TotalPoints) || standing.TotalPoints < 0)
            {
                return false;
            }
        }

        return update.ContentHash.Length == 64 && update.ContentHash.All(Uri.IsHexDigit) &&
               string.Equals(update.ContentHash, SparkyLiveKnowledgeHash.ComputeFinalization(update), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetKey(SparkyLiveKnowledgeUpdate update) =>
        $"{update.Profile}:{update.Finalization!.SeasonId}:{update.ContentHash.ToLowerInvariant()}";

    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("The Sparky live-knowledge outbox has not been initialized.");
    }
}
