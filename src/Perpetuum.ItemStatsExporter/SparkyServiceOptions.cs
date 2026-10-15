using System.Net;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Perpetuum.ItemStatsExporter;

internal sealed record SparkyServiceOptions(
    string ExportPath,
    Uri ServiceUri,
    X509Certificate2 ServiceCertificate,
    string ApiToken,
    string ModelBaseUrl,
    string ModelId,
    string? ModelApiToken,
    int MaxRequestBodyBytes,
    int MaxQuestionCharacters,
    int MaxAnswerCharacters,
    int MaxCompletionTokens,
    TimeSpan InferenceTimeout,
    int MaxConcurrentRequests,
    SparkyTunnelOptions? Tunnel,
    string? LiveApiToken = null,
    string? TraceDatabasePath = null,
    bool DevTraceCaptureContent = true,
    bool LiveTraceCaptureContent = false,
    int TraceContentRetentionDays = 30,
    int TraceMetadataRetentionDays = 90,
    Uri? AdminServiceUri = null,
    string? AdminApiToken = null,
    SparkyTunnelOptions? AdminTunnel = null,
    int ConversationHistoryTurns = 8,
    int ConversationHistoryCharacters = 4000,
    double AnswerTemperature = 0.2,
    double AnswerTopP = 0.9) : IDisposable
{
    private const string DefaultModelBaseUrl = "http://localhost:1234/v1";

    public bool ShouldCaptureTraceContent(SparkyApiProfile profile) =>
        profile == SparkyApiProfile.Live ? LiveTraceCaptureContent : DevTraceCaptureContent;

    public static SparkyServiceOptions Load(string exportPath, string? configurationPath = null)
    {
        var settings = SparkySettings.Load(configurationPath);
        var apiToken = settings.Get("SPARKY_DEV_API_TOKEN", settings.Get("SPARKY_API_TOKEN", string.Empty));
        if (apiToken.Length < 32)
        {
            throw new InvalidDataException("SPARKY_DEV_API_TOKEN or SPARKY_API_TOKEN must be at least 32 characters long.");
        }

        var liveApiTokenValue = settings.Get("SPARKY_LIVE_API_TOKEN", string.Empty);
        var liveApiToken = string.IsNullOrWhiteSpace(liveApiTokenValue) ? null : liveApiTokenValue;
        _ = new SparkyApiCredentialResolver(apiToken, liveApiToken);
        var adminApiToken = settings.GetRequired("SPARKY_ADMIN_API_TOKEN");
        using (var adminAuthentication = new SparkyAdminAuthentication(adminApiToken, apiToken, liveApiToken))
        {
        }

        var modelId = settings.GetRequired("SPARKY_LMSTUDIO_MODEL_ID");
        var modelBaseUrl = settings.Get("SPARKY_LMSTUDIO_BASE_URL", DefaultModelBaseUrl).TrimEnd('/');
        var modelUri = ReadModelUri(modelBaseUrl);
        var modelApiToken = settings.Get("SPARKY_LMSTUDIO_API_TOKEN", string.Empty);

        var serviceUri = ReadLoopbackServiceUri(settings.Get("SPARKY_SERVICE_URL", "https://127.0.0.1:5080"), "SPARKY_SERVICE_URL");
        var adminServiceUri = ReadLoopbackServiceUri(settings.Get("SPARKY_ADMIN_SERVICE_URL", "https://127.0.0.1:5081"), "SPARKY_ADMIN_SERVICE_URL");
        EnsureDifferentListenerPorts(serviceUri, adminServiceUri);
        var certificatePath = settings.GetRequired("SPARKY_SERVICE_CERT_PATH");
        if (!Path.IsPathFullyQualified(certificatePath))
        {
            certificatePath = Path.GetFullPath(certificatePath);
        }

        if (!File.Exists(certificatePath))
        {
            throw new InvalidDataException("SPARKY_SERVICE_CERT_PATH does not exist.");
        }

        X509Certificate2 certificate;
        try
        {
            var certificatePassword = settings.Get("SPARKY_SERVICE_CERT_PASSWORD", string.Empty);
            certificate = new X509Certificate2(
                certificatePath,
                string.IsNullOrEmpty(certificatePassword) ? null : certificatePassword,
                X509KeyStorageFlags.MachineKeySet);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidDataException("The configured Sparky service certificate could not be loaded.", exception);
        }

        if (!certificate.HasPrivateKey || DateTime.UtcNow < certificate.NotBefore.ToUniversalTime() ||
            DateTime.UtcNow > certificate.NotAfter.ToUniversalTime())
        {
            certificate.Dispose();
            throw new InvalidDataException("The configured Sparky service certificate must have a private key and be currently valid.");
        }

        if (!HasLoopbackServerIdentity(certificate))
        {
            certificate.Dispose();
            throw new InvalidDataException("The configured Sparky service certificate must include the 127.0.0.1 IP SAN and TLS server-auth usage.");
        }

        try
        {
            var tunnelEnabled = settings.GetBoolean("SPARKY_TUNNEL_ENABLED", defaultValue: false);
            var tunnel = tunnelEnabled ? LoadTunnelOptions(serviceUri, settings) : null;
            var adminTunnelEnabled = settings.GetBoolean("SPARKY_ADMIN_TUNNEL_ENABLED", defaultValue: false);
            var adminTunnel = adminTunnelEnabled
                ? LoadTunnelOptions(
                    adminServiceUri,
                    settings,
                    prefix: "SPARKY_ADMIN",
                    forwardPortSetting: "SPARKY_ADMIN_FORWARD_PORT",
                    name: "admin-panel")
                : null;
            if (tunnel is not null && adminTunnel is not null && tunnel.ForwardPort == adminTunnel.ForwardPort)
            {
                throw new InvalidDataException("The game API and admin tunnels must use different loopback ports on the remote SSH host.");
            }

            var requestBodyBytes = settings.GetPositiveInteger("SPARKY_MAX_REQUEST_BODY_BYTES", 131072);
            if (requestBodyBytes is < 8192 or > 1048576)
            {
                throw new InvalidDataException("SPARKY_MAX_REQUEST_BODY_BYTES must be between 8192 and 1048576 bytes.");
            }
            var maxQuestionCharacters = settings.GetPositiveInteger("SPARKY_MAX_QUESTION_CHARACTERS", 2000);
            var maxAnswerCharacters = settings.GetPositiveInteger("SPARKY_MAX_ANSWER_CHARACTERS", 2000);
            var maxCompletionTokens = settings.GetPositiveInteger("SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS", 700);
            var timeoutSeconds = settings.GetPositiveInteger("SPARKY_LMSTUDIO_TIMEOUT_SECONDS", 120);
            var maxConcurrentRequests = settings.GetPositiveInteger("SPARKY_SERVICE_MAX_CONCURRENCY", 2);
            var commonApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(commonApplicationData))
            {
                commonApplicationData = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory) ?? string.Empty, "ProgramData");
            }

            var defaultTraceDirectory = Path.Combine(commonApplicationData, "Sparky", "traces");
            var traceDatabasePath = settings.Get(
                "SPARKY_TRACE_DB_PATH",
                Path.Combine(defaultTraceDirectory, "sparky-traces.db"));
            if (!Path.IsPathFullyQualified(traceDatabasePath))
            {
                traceDatabasePath = Path.GetFullPath(traceDatabasePath);
            }

            var devTraceCaptureContent = settings.GetBoolean("SPARKY_DEV_TRACE_CAPTURE_CONTENT", defaultValue: true);
            var liveTraceCaptureContent = settings.GetBoolean("SPARKY_LIVE_TRACE_CAPTURE_CONTENT", defaultValue: false);
            var traceContentRetentionDays = settings.GetPositiveInteger("SPARKY_TRACE_CONTENT_RETENTION_DAYS", 30);
            var traceMetadataRetentionDays = settings.GetPositiveInteger("SPARKY_TRACE_METADATA_RETENTION_DAYS", 90);
            if (traceContentRetentionDays > 3650 || traceMetadataRetentionDays > 3650 ||
                traceMetadataRetentionDays < traceContentRetentionDays)
            {
                throw new InvalidDataException("Sparky trace retention must be at most ten years and metadata retention cannot be shorter than content retention.");
            }

            if (!Path.IsPathFullyQualified(exportPath))
            {
                exportPath = Path.GetFullPath(exportPath);
            }

            return new SparkyServiceOptions(
                exportPath,
                serviceUri,
                certificate,
                apiToken,
                modelBaseUrl,
                modelId,
                string.IsNullOrWhiteSpace(modelApiToken) ? null : modelApiToken,
                requestBodyBytes,
                maxQuestionCharacters,
                maxAnswerCharacters,
                maxCompletionTokens,
                TimeSpan.FromSeconds(timeoutSeconds),
                maxConcurrentRequests,
                tunnel,
                liveApiToken,
                traceDatabasePath,
                devTraceCaptureContent,
                liveTraceCaptureContent,
                traceContentRetentionDays,
                traceMetadataRetentionDays,
                adminServiceUri,
                adminApiToken,
                adminTunnel);
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    public void Dispose() => ServiceCertificate.Dispose();

    public SparkyApiCredentialResolver CreateApiCredentialResolver() =>
        new(ApiToken, LiveApiToken);

    private static SparkyTunnelOptions LoadTunnelOptions(
        Uri serviceUri,
        SparkySettings settings,
        string prefix = "SPARKY",
        string forwardPortSetting = "SPARKY_VDM_FORWARD_PORT",
        string name = "game-api")
    {
        var host = settings.GetRequired($"{prefix}_SSH_HOST");
        if (host.StartsWith("-", StringComparison.Ordinal) || host.Any(char.IsWhiteSpace) ||
            Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            throw new InvalidDataException($"{prefix}_SSH_HOST must be a valid host name or IP address.");
        }

        var user = settings.GetRequired($"{prefix}_SSH_USER");
        if (user.StartsWith("-", StringComparison.Ordinal) ||
            user.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new InvalidDataException($"{prefix}_SSH_USER contains unsupported characters.");
        }

        var privateKeySetting = $"{prefix}_SSH_PRIVATE_KEY_PATH";
        var knownHostsSetting = $"{prefix}_SSH_KNOWN_HOSTS_PATH";
        var privateKeyPath = ReadRequiredPath(privateKeySetting, settings);
        var knownHostsPath = ReadRequiredPath(knownHostsSetting, settings);
        var sshPath = settings.Get(
            $"{prefix}_SSH_CLIENT_PATH",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe"));

        if (!Path.IsPathFullyQualified(sshPath))
        {
            sshPath = Path.GetFullPath(sshPath);
        }

        if (!File.Exists(privateKeyPath))
        {
            throw new InvalidDataException($"{privateKeySetting} does not exist.");
        }

        if (!File.Exists(knownHostsPath))
        {
            throw new InvalidDataException($"{knownHostsSetting} does not exist.");
        }

        if (!File.Exists(sshPath))
        {
            throw new InvalidDataException($"{prefix}_SSH_CLIENT_PATH does not exist.");
        }

        var sshPort = settings.GetPositiveInteger($"{prefix}_SSH_PORT", 22);
        var forwardPort = settings.GetPositiveInteger(forwardPortSetting, serviceUri.Port + (name == "admin-panel" ? 1 : 0));
        var reconnectMinSeconds = settings.GetPositiveInteger($"{prefix}_SSH_RECONNECT_MIN_SECONDS", 5);
        var reconnectMaxSeconds = settings.GetPositiveInteger($"{prefix}_SSH_RECONNECT_MAX_SECONDS", 60);
        if (sshPort > 65535 || forwardPort > 65535 || reconnectMaxSeconds < reconnectMinSeconds)
        {
            throw new InvalidDataException($"{prefix} SSH ports must be valid TCP ports and the reconnect maximum must not be less than the minimum.");
        }

        return new SparkyTunnelOptions(
            host,
            sshPort,
            user,
            privateKeyPath,
            knownHostsPath,
            forwardPort,
            sshPath,
            serviceUri.Port,
            reconnectMinSeconds,
            reconnectMaxSeconds,
            name);
    }

    private static string ReadRequiredPath(string name, SparkySettings settings)
    {
        var path = settings.GetRequired(name);
        return Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(path);
    }

    internal static Uri ReadLoopbackServiceUri(string value, string settingName = "SPARKY_SERVICE_URL")
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !IPAddress.TryParse(uri.Host, out var address) ||
            !IPAddress.IsLoopback(address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            uri.Port is < 1 or > 65535 ||
            uri.UserInfo.Length > 0 ||
            uri.Query.Length > 0 ||
            uri.Fragment.Length > 0 ||
            uri.AbsolutePath != "/")
        {
            throw new InvalidDataException($"{settingName} must be an HTTPS URL on the IPv4 loopback address with no path or credentials.");
        }

        return uri;
    }

    internal static void EnsureDifferentListenerPorts(Uri gameApiUri, Uri adminApiUri)
    {
        if (gameApiUri.Port == adminApiUri.Port)
        {
            throw new InvalidDataException("SPARKY_SERVICE_URL and SPARKY_ADMIN_SERVICE_URL must use different ports.");
        }
    }

    private static Uri ReadModelUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            uri.UserInfo.Length > 0 ||
            uri.Query.Length > 0 ||
            uri.Fragment.Length > 0 ||
            (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback))
        {
            throw new InvalidDataException("SPARKY_LMSTUDIO_BASE_URL must be an absolute HTTP or HTTPS URL without credentials, query, or fragment; non-loopback URLs must use HTTPS.");
        }

        return uri;
    }

    private static bool HasLoopbackServerIdentity(X509Certificate2 certificate)
    {
        const string subjectAlternativeNameOid = "2.5.29.17";
        const string enhancedKeyUsageOid = "2.5.29.37";
        const string serverAuthenticationOid = "1.3.6.1.5.5.7.3.1";
        var expectedAddress = IPAddress.Loopback.GetAddressBytes();

        var subjectAlternativeName = certificate.Extensions
            .Cast<X509Extension>()
            .FirstOrDefault(extension => extension.Oid?.Value == subjectAlternativeNameOid);
        var hasLoopbackAddress = false;
        if (subjectAlternativeName is not null)
        {
            var reader = new AsnReader(subjectAlternativeName.RawData, AsnEncodingRules.DER);
            var names = reader.ReadSequence();
            var ipAddressTag = new Asn1Tag(TagClass.ContextSpecific, 7);
            while (names.HasData)
            {
                if (names.PeekTag().HasSameClassAndValue(ipAddressTag))
                {
                    hasLoopbackAddress |= names.ReadOctetString(ipAddressTag).AsSpan().SequenceEqual(expectedAddress);
                }
                else
                {
                    names.ReadEncodedValue();
                }
            }
        }

        var enhancedKeyUsage = certificate.Extensions
            .Cast<X509Extension>()
            .FirstOrDefault(extension => extension.Oid?.Value == enhancedKeyUsageOid);
        if (enhancedKeyUsage is null)
        {
            return false;
        }

        var usageReader = new AsnReader(enhancedKeyUsage.RawData, AsnEncodingRules.DER);
        var usages = usageReader.ReadSequence();
        var hasServerAuthentication = false;
        while (usages.HasData)
        {
            hasServerAuthentication |= usages.ReadObjectIdentifier() == serverAuthenticationOid;
        }

        return hasLoopbackAddress && hasServerAuthentication;
    }

}

internal sealed record SparkyTunnelOptions(
    string Host,
    int SshPort,
    string User,
    string PrivateKeyPath,
    string KnownHostsPath,
    int ForwardPort,
    string SshClientPath,
    int LocalServicePort,
    int ReconnectMinSeconds,
    int ReconnectMaxSeconds,
    string Name = "game-api");
