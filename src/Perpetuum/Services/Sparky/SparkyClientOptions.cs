using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Perpetuum.Services.Sparky;

internal sealed record SparkyClientOptions(
    Uri ApiUri,
    string ApiToken,
    string Profile,
    bool UsesLegacySettings,
    TimeSpan RequestTimeout,
    int MaxQuestionCharacters,
    int MaxAnswerCharacters,
    int QueueCapacity,
    int WorkerCount,
    int PendingPerCallerLimit,
    TimeSpan ContextLifetime,
    int ContextMaxTurns,
    int ContextMaxCharacters,
    int ContextCapacity,
    string SenderCharacterNick)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static bool TryCreate(GlobalConfiguration configuration, out SparkyClientOptions? options)
    {
        options = null;
        if (!Uri.TryCreate(configuration.SparkyApiUrl, UriKind.Absolute, out var apiUri) ||
            apiUri.Scheme != Uri.UriSchemeHttps ||
            !IPAddress.TryParse(apiUri.Host, out var address) ||
            !address.Equals(IPAddress.Loopback) ||
            apiUri.UserInfo.Length != 0 ||
            apiUri.Query.Length != 0 ||
            apiUri.Fragment.Length != 0 ||
            apiUri.AbsolutePath != "/v1/answer" ||
            apiUri.Port is < 1 or > 65535)
        {
            return false;
        }

        if (configuration.SparkyRequestTimeoutSeconds is < 1 or > 300 ||
            configuration.SparkyMaxQuestionCharacters is < 1 or > 2000 ||
            configuration.SparkyMaxAnswerCharacters is < 1 or > 2000 ||
            configuration.SparkyQueueCapacity is < 1 or > 512 ||
            configuration.SparkyWorkerCount is < 1 or > 16 ||
            configuration.SparkyPendingPerCallerLimit is < 1 or > 10 ||
            configuration.SparkyContextLifetimeMinutes is < 1 or > 1440 ||
            configuration.SparkyContextMaxTurns is < 2 or > SparkyContextStore.HardMaximumTurns ||
            configuration.SparkyContextMaxCharacters is < SparkyContextStore.MinimumMaximumCharacters or > SparkyContextStore.HardMaximumCharacters ||
            configuration.SparkyContextCapacity is < 1 or > 100000 ||
            string.IsNullOrWhiteSpace(configuration.SparkySenderCharacterNick) ||
            configuration.SparkySenderCharacterNick.Length > 100 ||
            string.IsNullOrWhiteSpace(configuration.SparkyProtectedSettingsPath))
        {
            return false;
        }

        try
        {
            var path = Path.GetFullPath(configuration.SparkyProtectedSettingsPath);
            var protectedSettingsRoot = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Sparky"));
            var protectedSettingsPrefix = protectedSettingsRoot.TrimEnd(Path.DirectorySeparatorChar) +
                                          Path.DirectorySeparatorChar;
            if (!path.StartsWith(protectedSettingsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var file = new FileInfo(path);
            if (!file.Exists || file.Length is <= 0 or > 16384)
            {
                return false;
            }

            var settingsJson = File.ReadAllText(path, Encoding.UTF8);
            var settings = JsonSerializer.Deserialize<ProtectedSettings>(settingsJson, JsonOptions);
            if (settings is null || string.IsNullOrWhiteSpace(settings.ProtectedToken) ||
                !TryResolveProtectedProfile(settings.Version, settings.Profile, out var profile, out var usesLegacySettings))
            {
                return false;
            }

            var protectedBytes = Convert.FromBase64String(settings.ProtectedToken);
            var clearBytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                DataProtectionScope.LocalMachine);

            try
            {
                var token = Encoding.UTF8.GetString(clearBytes);
                if (token.Length is < 16 or > 4096 || token.Any(char.IsControl))
                {
                    return false;
                }

                options = new SparkyClientOptions(
                    apiUri,
                    token,
                    profile,
                    usesLegacySettings,
                    TimeSpan.FromSeconds(configuration.SparkyRequestTimeoutSeconds),
                    configuration.SparkyMaxQuestionCharacters,
                    configuration.SparkyMaxAnswerCharacters,
                    configuration.SparkyQueueCapacity,
                    configuration.SparkyWorkerCount,
                    configuration.SparkyPendingPerCallerLimit,
                    TimeSpan.FromMinutes(configuration.SparkyContextLifetimeMinutes),
                    configuration.SparkyContextMaxTurns,
                    configuration.SparkyContextMaxCharacters,
                    configuration.SparkyContextCapacity,
                    configuration.SparkySenderCharacterNick);
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(clearBytes);
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           JsonException or FormatException or CryptographicException or
                                           ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return false;
        }
    }

    internal static bool TryResolveProtectedProfile(
        int version,
        string? configuredProfile,
        out string profile,
        out bool usesLegacySettings)
    {
        usesLegacySettings = false;
        if (version == 1)
        {
            profile = "dev";
            usesLegacySettings = true;
            return true;
        }

        if (version == 2 && configuredProfile is "dev" or "live")
        {
            profile = configuredProfile;
            return true;
        }

        profile = string.Empty;
        return false;
    }

    private sealed class ProtectedSettings
    {
        public int Version { get; init; }
        public string? Profile { get; init; }
        public string? ProtectedToken { get; init; }
    }
}
