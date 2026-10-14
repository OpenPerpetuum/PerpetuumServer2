using Newtonsoft.Json;
using Perpetuum.Host;
using Perpetuum.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Perpetuum.Services.Sparky
{
    public sealed class SparkyQuestionCaptureService
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new(false);
        private static readonly Regex DiscordUserMention = new("<@!?[0-9]+>", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly object _sync = new();
        private readonly GlobalConfiguration _configuration;
        private readonly IHostStateService _hostStateService;
        private readonly HashSet<ulong> _allowedDiscordChannelIds = new();

        private string _archivePath;
        private string _sessionMetadataPath;
        private DateTimeOffset _sessionExpiresAtUtc;
        private long _recordsAppendedThisProcess;
        private bool _isActive;

        public SparkyQuestionCaptureService(GlobalConfiguration configuration, IHostStateService hostStateService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _hostStateService = hostStateService ?? throw new ArgumentNullException(nameof(hostStateService));
            _hostStateService.StateChanged += OnHostStateChanged;

            Initialize();
        }

        public bool TryCaptureGameMessage(string channelName, ulong? discordChannelId, string message)
        {
            if (!discordChannelId.HasValue)
            {
                return false;
            }

            return TryCapture("Game", discordChannelId.Value, channelName, message, message);
        }

        public bool TryCaptureDiscordMessage(ulong discordChannelId, string channelLabel, string candidateText, string storedText)
        {
            return TryCapture("Discord", discordChannelId, channelLabel, candidateText, storedText);
        }

        private void Initialize()
        {
            if (!_configuration.SparkyQuestionCaptureEnabled)
            {
                LogStatus("disabled by configuration");
                return;
            }

            try
            {
                ValidateConfiguration();
                _archivePath = ResolveArchivePath();
                _sessionMetadataPath = _archivePath + ".session.json";

                string archiveDirectory = Path.GetDirectoryName(_archivePath);
                if (string.IsNullOrWhiteSpace(archiveDirectory))
                {
                    throw new InvalidDataException("The configured capture archive path has no parent directory.");
                }

                Directory.CreateDirectory(archiveDirectory);
                RepairIncompleteTrailingLine(_archivePath);

                SessionMetadata session = File.Exists(_sessionMetadataPath)
                    ? ReadSessionMetadata()
                    : CreateSessionMetadata();

                _sessionExpiresAtUtc = session.ExpiresAtUtc.ToUniversalTime();
                _isActive = DateTimeOffset.UtcNow < _sessionExpiresAtUtc;

                if (_isActive)
                {
                    Logger.Info($"[Sparky capture] Enabled until {_sessionExpiresAtUtc:O}; records appended this process: 0.");
                }
                else
                {
                    Logger.Info("[Sparky capture] Disabled because the saved session deadline has passed; records appended this process: 0.");
                }
            }
            catch (Exception exception)
            {
                _isActive = false;
                LogFailure("startup", exception);
            }
        }

        private void ValidateConfiguration()
        {
            List<ulong> configuredIds = _configuration.SparkyQuestionCaptureDiscordChannelIds;
            bool validIds = configuredIds != null &&
                            configuredIds.Count == 4 &&
                            configuredIds.All(id => id != 0) &&
                            configuredIds.Distinct().Count() == 4;
            bool validDuration = _configuration.SparkyQuestionCaptureDurationHours is >= 1 and <= 48;

            if (!validIds || !validDuration)
            {
                throw new InvalidDataException("Capture requires four unique nonzero Discord channel IDs and a duration from 1 through 48 hours.");
            }

            _allowedDiscordChannelIds.UnionWith(configuredIds);
        }

        private string ResolveArchivePath()
        {
            string gameRoot = _configuration.GameRoot;
            if (string.IsNullOrWhiteSpace(gameRoot))
            {
                throw new InvalidDataException("The game root is not configured.");
            }

            string configuredPath = _configuration.SparkyQuestionCapturePath;
            string path = string.IsNullOrWhiteSpace(configuredPath)
                ? Path.Combine(gameRoot, "logs", "sparky-question-capture.jsonl")
                : Path.IsPathFullyQualified(configuredPath)
                    ? configuredPath
                    : Path.Combine(gameRoot, configuredPath);

            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root) || fullPath.StartsWith("\\\\", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The capture archive path must be on a local drive.");
            }

            if (new DriveInfo(root).DriveType == DriveType.Network)
            {
                throw new InvalidDataException("The capture archive path must be on a local drive.");
            }

            return fullPath;
        }

        private SessionMetadata ReadSessionMetadata()
        {
            string json = File.ReadAllText(_sessionMetadataPath, Encoding.UTF8);
            SessionMetadata metadata = JsonConvert.DeserializeObject<SessionMetadata>(json);
            if (metadata == null ||
                metadata.StartedAtUtc == default ||
                metadata.ExpiresAtUtc <= metadata.StartedAtUtc ||
                metadata.ExpiresAtUtc - metadata.StartedAtUtc > TimeSpan.FromHours(48) ||
                metadata.ExpiresAtUtc - metadata.StartedAtUtc < TimeSpan.FromHours(1))
            {
                throw new InvalidDataException("The saved capture session metadata is invalid.");
            }

            return metadata;
        }

        private SessionMetadata CreateSessionMetadata()
        {
            DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
            var metadata = new SessionMetadata
            {
                StartedAtUtc = startedAtUtc,
                ExpiresAtUtc = startedAtUtc.AddHours(_configuration.SparkyQuestionCaptureDurationHours)
            };

            PersistSessionMetadata(metadata);
            return metadata;
        }

        private void PersistSessionMetadata(SessionMetadata metadata)
        {
            string temporaryPath = _sessionMetadataPath + ".tmp";
            byte[] bytes = Utf8WithoutBom.GetBytes(JsonConvert.SerializeObject(metadata, Formatting.None));

            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _sessionMetadataPath, overwrite: false);
        }

        private bool TryCapture(string source, ulong discordChannelId, string channelLabel, string candidateText, string storedText)
        {
            lock (_sync)
            {
                if (!_isActive)
                {
                    return false;
                }

                if (DateTimeOffset.UtcNow >= _sessionExpiresAtUtc)
                {
                    _isActive = false;
                    LogStatus("session deadline passed");
                    return false;
                }

                if (!_allowedDiscordChannelIds.Contains(discordChannelId) ||
                    string.IsNullOrWhiteSpace(channelLabel) ||
                    !SparkyQuestionCandidateFilter.IsCandidate(candidateText))
                {
                    return false;
                }

                try
                {
                    AppendRecord(source, channelLabel, storedText);
                    _recordsAppendedThisProcess++;
                    return true;
                }
                catch (Exception exception)
                {
                    _isActive = false;
                    LogFailure("archive write", exception);
                    return false;
                }
            }
        }

        private void AppendRecord(string source, string channelLabel, string storedText)
        {
            string text = storedText ?? string.Empty;
            if (source == "Discord")
            {
                text = DiscordUserMention.Replace(text, "[user mention]");
            }

            var record = new CaptureRecord
            {
                CapturedAtUtc = DateTime.UtcNow,
                Source = source,
                Channel = channelLabel,
                Text = text
            };

            var settings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DateFormatString = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"
            };
            byte[] bytes = Utf8WithoutBom.GetBytes(JsonConvert.SerializeObject(record, Formatting.None, settings) + "\n");

            using (var stream = new FileStream(_archivePath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
        }

        private static void RepairIncompleteTrailingLine(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                if (stream.Length == 0)
                {
                    return;
                }

                long originalLength = stream.Length;
                stream.Position = originalLength - 1;
                if (stream.ReadByte() == '\n')
                {
                    return;
                }

                long position = originalLength - 1;
                while (position >= 0)
                {
                    stream.Position = position;
                    if (stream.ReadByte() == '\n')
                    {
                        stream.SetLength(position + 1);
                        stream.Flush(flushToDisk: true);
                        return;
                    }

                    position--;
                }

                stream.SetLength(0);
                stream.Flush(flushToDisk: true);
            }
        }

        private void OnHostStateChanged(IHostStateService hostStateService, HostState state)
        {
            if (state == HostState.Off)
            {
                LogStatus("host stopped");
            }
        }

        private void LogStatus(string detail)
        {
            lock (_sync)
            {
                string state = _isActive ? "enabled" : "disabled";
                Logger.Info($"[Sparky capture] {state} ({detail}); records appended this process: {_recordsAppendedThisProcess}.");
            }
        }

        private static void LogFailure(string operation, Exception exception)
        {
            Logger.Error($"[Sparky capture] Disabled after {operation} failure ({exception.GetType().Name}).");
        }

        private sealed class CaptureRecord
        {
            [JsonProperty("capturedAtUtc")]
            public DateTime CapturedAtUtc { get; set; }

            [JsonProperty("source")]
            public string Source { get; set; }

            [JsonProperty("channel")]
            public string Channel { get; set; }

            [JsonProperty("text")]
            public string Text { get; set; }
        }

        private sealed class SessionMetadata
        {
            [JsonProperty("startedAtUtc")]
            public DateTimeOffset StartedAtUtc { get; set; }

            [JsonProperty("expiresAtUtc")]
            public DateTimeOffset ExpiresAtUtc { get; set; }
        }
    }
}
