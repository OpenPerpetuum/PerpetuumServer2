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
    SparkyTunnelOptions? Tunnel) : IDisposable
{
    private const string DefaultModelBaseUrl = "http://localhost:1234/v1";

    public static SparkyServiceOptions Load(string exportPath, string? configurationPath = null)
    {
        var settings = SparkySettings.Load(configurationPath);
        var apiToken = settings.GetRequired("SPARKY_API_TOKEN");
        if (apiToken.Length < 32)
        {
            throw new InvalidDataException("SPARKY_API_TOKEN must be at least 32 characters long.");
        }

        var modelId = settings.GetRequired("SPARKY_LMSTUDIO_MODEL_ID");
        var modelBaseUrl = settings.Get("SPARKY_LMSTUDIO_BASE_URL", DefaultModelBaseUrl).TrimEnd('/');
        var modelUri = ReadModelUri(modelBaseUrl);
        var modelApiToken = settings.Get("SPARKY_LMSTUDIO_API_TOKEN", string.Empty);

        var serviceUri = ReadLoopbackServiceUri(settings.Get("SPARKY_SERVICE_URL", "https://127.0.0.1:5080"));
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

            var requestBodyBytes = settings.GetPositiveInteger("SPARKY_MAX_REQUEST_BODY_BYTES", 8192);
            var maxQuestionCharacters = settings.GetPositiveInteger("SPARKY_MAX_QUESTION_CHARACTERS", 2000);
            var maxAnswerCharacters = settings.GetPositiveInteger("SPARKY_MAX_ANSWER_CHARACTERS", 2000);
            var maxCompletionTokens = settings.GetPositiveInteger("SPARKY_LMSTUDIO_MAX_COMPLETION_TOKENS", 700);
            var timeoutSeconds = settings.GetPositiveInteger("SPARKY_LMSTUDIO_TIMEOUT_SECONDS", 120);
            var maxConcurrentRequests = settings.GetPositiveInteger("SPARKY_SERVICE_MAX_CONCURRENCY", 2);

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
                tunnel);
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    public void Dispose() => ServiceCertificate.Dispose();

    private static SparkyTunnelOptions LoadTunnelOptions(Uri serviceUri, SparkySettings settings)
    {
        var host = settings.GetRequired("SPARKY_SSH_HOST");
        if (host.StartsWith("-", StringComparison.Ordinal) || host.Any(char.IsWhiteSpace) ||
            Uri.CheckHostName(host) == UriHostNameType.Unknown)
        {
            throw new InvalidDataException("SPARKY_SSH_HOST must be a valid host name or IP address.");
        }

        var user = settings.GetRequired("SPARKY_SSH_USER");
        if (user.StartsWith("-", StringComparison.Ordinal) ||
            user.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new InvalidDataException("SPARKY_SSH_USER contains unsupported characters.");
        }

        var privateKeyPath = ReadRequiredPath("SPARKY_SSH_PRIVATE_KEY_PATH", settings);
        var knownHostsPath = ReadRequiredPath("SPARKY_SSH_KNOWN_HOSTS_PATH", settings);
        var sshPath = settings.Get(
            "SPARKY_SSH_CLIENT_PATH",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe"));

        if (!Path.IsPathFullyQualified(sshPath))
        {
            sshPath = Path.GetFullPath(sshPath);
        }

        if (!File.Exists(privateKeyPath))
        {
            throw new InvalidDataException("SPARKY_SSH_PRIVATE_KEY_PATH does not exist.");
        }

        if (!File.Exists(knownHostsPath))
        {
            throw new InvalidDataException("SPARKY_SSH_KNOWN_HOSTS_PATH does not exist.");
        }

        if (!File.Exists(sshPath))
        {
            throw new InvalidDataException("SPARKY_SSH_CLIENT_PATH does not exist.");
        }

        var sshPort = settings.GetPositiveInteger("SPARKY_SSH_PORT", 22);
        var forwardPort = settings.GetPositiveInteger("SPARKY_VDM_FORWARD_PORT", 5080);
        var reconnectMinSeconds = settings.GetPositiveInteger("SPARKY_SSH_RECONNECT_MIN_SECONDS", 5);
        var reconnectMaxSeconds = settings.GetPositiveInteger("SPARKY_SSH_RECONNECT_MAX_SECONDS", 60);
        if (sshPort > 65535 || forwardPort > 65535 || reconnectMaxSeconds < reconnectMinSeconds)
        {
            throw new InvalidDataException("SSH ports must be valid TCP ports and the reconnect maximum must not be less than the minimum.");
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
            reconnectMaxSeconds);
    }

    private static string ReadRequiredPath(string name, SparkySettings settings)
    {
        var path = settings.GetRequired(name);
        return Path.IsPathFullyQualified(path) ? path : Path.GetFullPath(path);
    }

    private static Uri ReadLoopbackServiceUri(string value)
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
            throw new InvalidDataException("SPARKY_SERVICE_URL must be an HTTPS URL on the IPv4 loopback address with no path or credentials.");
        }

        return uri;
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
    int ReconnectMaxSeconds);
