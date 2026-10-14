using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkySettings
{
    private static readonly HashSet<string> ProtectedSettings = new(StringComparer.Ordinal)
    {
        "SPARKY_API_TOKEN",
        "SPARKY_DEV_API_TOKEN",
        "SPARKY_LIVE_API_TOKEN",
        "SPARKY_LMSTUDIO_API_TOKEN",
        "SPARKY_SERVICE_CERT_PASSWORD",
        "SPARKY_ADMIN_API_TOKEN"
    };

    private readonly IReadOnlyDictionary<string, string> _values;
    private readonly bool _usesConfigurationFile;

    private SparkySettings(IReadOnlyDictionary<string, string> values, bool usesConfigurationFile)
    {
        _values = values;
        _usesConfigurationFile = usesConfigurationFile;
    }

    public static SparkySettings Load(string? configurationPath)
    {
        if (string.IsNullOrWhiteSpace(configurationPath))
        {
            return new SparkySettings(new Dictionary<string, string>(StringComparer.Ordinal), usesConfigurationFile: false);
        }

        configurationPath = Path.IsPathFullyQualified(configurationPath)
            ? configurationPath
            : Path.GetFullPath(configurationPath);
        if (!File.Exists(configurationPath))
        {
            throw new InvalidDataException("The configured Sparky settings file does not exist.");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(configurationPath));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The Sparky settings file must contain a JSON object.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!property.Name.StartsWith("SPARKY_", StringComparison.Ordinal) ||
                property.Value.ValueKind != JsonValueKind.String ||
                !values.TryAdd(property.Name, property.Value.GetString() ?? string.Empty))
            {
                throw new InvalidDataException("The Sparky settings file contains an invalid or duplicate setting.");
            }
        }

        foreach (var protectedName in ProtectedSettings)
        {
            if (!values.TryGetValue(protectedName, out var protectedValue))
            {
                continue;
            }

            if (!protectedValue.StartsWith("dpapi:", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{protectedName} must be DPAPI protected in the settings file.");
            }

            try
            {
                var encrypted = Convert.FromBase64String(protectedValue[6..]);
                var clear = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.LocalMachine);
                try
                {
                    values[protectedName] = Encoding.UTF8.GetString(clear);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(clear);
                    CryptographicOperations.ZeroMemory(encrypted);
                }
            }
            catch (CryptographicException exception)
            {
                throw new InvalidDataException($"{protectedName} could not be decrypted on this computer.", exception);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException($"{protectedName} has an invalid DPAPI value.", exception);
            }
        }

        return new SparkySettings(values, usesConfigurationFile: true);
    }

    public string? Get(string name)
    {
        if (_usesConfigurationFile)
        {
            return _values.GetValueOrDefault(name);
        }

        return Environment.GetEnvironmentVariable(name);
    }

    public string GetRequired(string name)
    {
        var value = Get(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{name} is required.");
        }

        return value.Trim();
    }

    public string Get(string name, string defaultValue)
    {
        var value = Get(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    public int GetPositiveInteger(string name, int defaultValue)
    {
        var configuredValue = Get(name);
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return defaultValue;
        }

        if (!int.TryParse(configuredValue, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var value) || value < 1)
        {
            throw new InvalidDataException($"{name} must be a positive integer.");
        }

        return value;
    }

    public bool GetBoolean(string name, bool defaultValue)
    {
        var configuredValue = Get(name);
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return defaultValue;
        }

        if (!bool.TryParse(configuredValue, out var value))
        {
            throw new InvalidDataException($"{name} must be true or false.");
        }

        return value;
    }
}
