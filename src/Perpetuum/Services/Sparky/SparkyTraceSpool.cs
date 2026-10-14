using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perpetuum.Services.Sparky;

public sealed class SparkyTraceSpool : IAsyncDisposable
{
    private const int GapMarkerReserveBytes = 512;
    private const int MaximumSpoolBytes = 128 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 4,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly int _maxSpoolBytes;
    private readonly int _maxLineBytes;
    private readonly List<SparkyTraceEvent> _pending = [];
    private readonly Dictionary<Guid, byte[]> _serializedByEventId = [];
    private readonly HashSet<string> _pendingGapSources = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gapMarkerSources = new(StringComparer.Ordinal);
    private bool _initialized;
    private bool _disposed;
    private long _fileBytes;

    public SparkyTraceSpool(string path, int maxSpoolBytes, int maxLineBytes = 1024)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A local Sparky trace spool path is required.", nameof(path));
        }

        if (maxSpoolBytes < 1024 || maxSpoolBytes > MaximumSpoolBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSpoolBytes));
        }

        if (maxLineBytes < 256 || maxLineBytes > 16 * 1024 || maxLineBytes >= maxSpoolBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLineBytes));
        }

        Path = System.IO.Path.GetFullPath(path);
        _maxSpoolBytes = maxSpoolBytes;
        _maxLineBytes = maxLineBytes;
        ValidateLocalPath(Path);
    }

    public string Path { get; }

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_initialized)
            {
                return;
            }

            ValidatePathComponents(Path);
            string parent = System.IO.Path.GetDirectoryName(Path)
                ?? throw new InvalidDataException("The Sparky trace spool path has no parent directory.");
            Directory.CreateDirectory(parent);
            ValidatePathComponents(Path);

            if (!File.Exists(Path))
            {
                using var created = new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
                created.Flush(flushToDisk: true);
            }

            RecoverExistingFile(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<SparkyTraceAppendDisposition> AppendAsync(
        SparkyTraceEvent traceEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(traceEvent);
        if (!traceEvent.IsValid)
        {
            throw new InvalidDataException("The Sparky trace event is invalid.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(traceEvent, JsonOptions);
            if (json.Length + 1 > _maxLineBytes)
            {
                throw new InvalidDataException("The Sparky trace event exceeds the spool line limit.");
            }

            if (_serializedByEventId.TryGetValue(traceEvent.EventId, out byte[]? existing))
            {
                if (existing.AsSpan().SequenceEqual(json))
                {
                    return SparkyTraceAppendDisposition.Duplicate;
                }

                throw new InvalidDataException("A Sparky trace event ID was reused with different metadata.");
            }

            int lineBytes = json.Length + 1;
            long regularLimit = Math.Max(0, _maxSpoolBytes - GapMarkerReserveBytes);
            if (_fileBytes + lineBytes > regularLimit)
            {
                if (!_gapMarkerSources.Contains(traceEvent.Source))
                {
                    _pendingGapSources.Add(traceEvent.Source);
                }

                WritePendingGapMarkers();
                return SparkyTraceAppendDisposition.CapacityExceeded;
            }

            AppendLine(json);
            _pending.Add(traceEvent);
            _serializedByEventId.Add(traceEvent.EventId, json);
            _fileBytes += lineBytes;
            return SparkyTraceAppendDisposition.Added;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task<IReadOnlyList<SparkyTraceEvent>> ReadPendingAsync(
        int maximumCount,
        CancellationToken cancellationToken)
    {
        if (maximumCount is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            return _pending.Take(maximumCount).ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal async Task AcknowledgeAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventIds);
        if (eventIds.Count == 0)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureInitialized();
            var acknowledged = eventIds.ToHashSet();
            if (!_pending.Any(traceEvent => acknowledged.Contains(traceEvent.EventId)))
            {
                return;
            }

            SparkyTraceEvent[] remaining = _pending
                .Where(traceEvent => !acknowledged.Contains(traceEvent.EventId))
                .ToArray();
            RewriteAtomically(remaining);
            _pending.Clear();
            _pending.AddRange(remaining);
            _serializedByEventId.Clear();
            _gapMarkerSources.Clear();
            _fileBytes = 0;
            foreach (var traceEvent in remaining)
            {
                byte[] json = JsonSerializer.SerializeToUtf8Bytes(traceEvent, JsonOptions);
                _serializedByEventId.Add(traceEvent.EventId, json);
                if (traceEvent.Stage == "trace_gap" && traceEvent.DetailCode == "spool_overflow")
                {
                    _gapMarkerSources.Add(traceEvent.Source);
                }

                _fileBytes += json.Length + 1;
            }

            WritePendingGapMarkers();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RecoverExistingFile(CancellationToken cancellationToken)
    {
        ValidatePathComponents(Path);
        using var stream = new FileStream(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        using var line = new MemoryStream(_maxLineBytes);
        var buffer = new byte[4096];
        long consumedBytes = 0;
        long lastCompleteLineEnd = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = stream.Read(buffer, 0, buffer.Length);
            if (count == 0)
            {
                break;
            }

            for (int index = 0; index < count; index++)
            {
                byte value = buffer[index];
                consumedBytes++;
                if (value == (byte)'\n')
                {
                    if (line.Length > 0 && line.GetBuffer()[line.Length - 1] == (byte)'\r')
                    {
                        line.SetLength(line.Length - 1);
                    }

                    if (line.Length == 0)
                    {
                        throw new InvalidDataException("The Sparky trace spool contains an empty complete line.");
                    }

                    byte[] json = line.ToArray();
                    ReadAndIndexLine(json, consumedBytes - lastCompleteLineEnd);
                    line.SetLength(0);
                    lastCompleteLineEnd = consumedBytes;
                    continue;
                }

                if (line.Length >= _maxLineBytes)
                {
                    throw new InvalidDataException("The Sparky trace spool contains a line over its configured limit.");
                }

                line.WriteByte(value);
            }
        }

        if (line.Length > 0)
        {
            // Only an incomplete final record is safe to truncate. Invalid complete records above remain untouched.
            stream.SetLength(lastCompleteLineEnd);
            stream.Flush(flushToDisk: true);
            consumedBytes = lastCompleteLineEnd;
        }

        _fileBytes = consumedBytes;
        WritePendingGapMarkers();
    }

    private void ReadAndIndexLine(byte[] json, long lineBytes)
    {
        if (lineBytes > _maxLineBytes)
        {
            throw new InvalidDataException("The Sparky trace spool contains a line over its configured limit.");
        }

        SparkyTraceEvent? traceEvent;
        try
        {
            traceEvent = JsonSerializer.Deserialize<SparkyTraceEvent>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The Sparky trace spool contains an invalid complete record.", exception);
        }

        if (traceEvent is null || !traceEvent.IsValid)
        {
            throw new InvalidDataException("The Sparky trace spool contains an invalid complete record.");
        }

        if (_serializedByEventId.TryGetValue(traceEvent.EventId, out byte[]? existing))
        {
            if (!existing.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(traceEvent, JsonOptions)))
            {
                throw new InvalidDataException("The Sparky trace spool contains conflicting event IDs.");
            }
        }
        else
        {
            _pending.Add(traceEvent);
            _serializedByEventId.Add(traceEvent.EventId, JsonSerializer.SerializeToUtf8Bytes(traceEvent, JsonOptions));
            if (traceEvent.Stage == "trace_gap" && traceEvent.DetailCode == "spool_overflow")
            {
                _gapMarkerSources.Add(traceEvent.Source);
            }
        }

        _fileBytes += lineBytes;
    }

    private void AppendLine(byte[] json)
    {
        ValidatePathComponents(Path);
        using var stream = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        stream.Write(json);
        stream.WriteByte((byte)'\n');
        stream.Flush(flushToDisk: true);
    }

    private void RewriteAtomically(IReadOnlyList<SparkyTraceEvent> events)
    {
        ValidatePathComponents(Path);
        string tempPath = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                foreach (var traceEvent in events)
                {
                    byte[] json = JsonSerializer.SerializeToUtf8Bytes(traceEvent, JsonOptions);
                    stream.Write(json);
                    stream.WriteByte((byte)'\n');
                }

                stream.Flush(flushToDisk: true);
            }

            ValidatePathComponents(Path);
            File.Move(tempPath, Path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private void WritePendingGapMarkers()
    {
        foreach (string source in _pendingGapSources.ToArray())
        {
            if (_gapMarkerSources.Contains(source))
            {
                _pendingGapSources.Remove(source);
                continue;
            }

            var marker = SparkyTraceEvent.CreateGap(source, "spool_overflow");
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(marker, JsonOptions);
            int lineBytes = json.Length + 1;
            if (lineBytes > _maxLineBytes || _fileBytes + lineBytes > _maxSpoolBytes)
            {
                continue;
            }

            AppendLine(json);
            _pending.Add(marker);
            _serializedByEventId.Add(marker.EventId, json);
            _gapMarkerSources.Add(source);
            _fileBytes += lineBytes;
            _pendingGapSources.Remove(source);
        }
    }

    private static void ValidateLocalPath(string fullPath)
    {
        string root = System.IO.Path.GetPathRoot(fullPath)
            ?? throw new InvalidDataException("The Sparky trace spool path must be rooted on a local drive.");
        if (fullPath.StartsWith("\\\\", StringComparison.Ordinal) || new DriveInfo(root).DriveType == DriveType.Network)
        {
            throw new InvalidDataException("The Sparky trace spool path must be on a local drive.");
        }
    }

    private static void ValidatePathComponents(string fullPath)
    {
        string root = System.IO.Path.GetPathRoot(fullPath)
            ?? throw new InvalidDataException("The Sparky trace spool path must be rooted on a local drive.");
        string current = root;
        string relative = System.IO.Path.GetRelativePath(root, fullPath);
        string[] components = relative.Split(
            new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < components.Length; index++)
        {
            current = System.IO.Path.Combine(current, components[index]);
            bool isFinal = index == components.Length - 1;
            FileSystemInfo fileSystemInfo = isFinal ? new FileInfo(current) : new DirectoryInfo(current);
            if (fileSystemInfo.Exists || fileSystemInfo.LinkTarget is not null)
            {
                if ((fileSystemInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new SecurityException("The Sparky trace spool path must not contain reparse points.");
                }
            }
        }
    }

    private void EnsureInitialized()
    {
        ThrowIfDisposed();
        if (!_initialized)
        {
            throw new InvalidOperationException("The Sparky trace spool has not been initialized.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending.Clear();
            _serializedByEventId.Clear();
            _pendingGapSources.Clear();
            _gapMarkerSources.Clear();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
