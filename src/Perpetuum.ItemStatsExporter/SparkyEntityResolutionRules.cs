using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Perpetuum.ItemStatsExporter;

internal enum SparkyEntityKind
{
    Item,
    Robot
}

internal sealed record SparkyEntityTierRule(
    SparkyEntityKind EntityKind,
    int TierType,
    int? TierLevel,
    string CanonicalLabel,
    IReadOnlyDictionary<int, IReadOnlyList<string>> AliasesByLanguage);

internal sealed record SparkyCalibrationTemplateRule(
    string Suffix,
    IReadOnlyDictionary<int, IReadOnlyList<string>> AliasesByLanguage);

internal sealed record SparkyEntityResolutionRules(
    int Version,
    IReadOnlyList<SparkyEntityTierRule> TierRules,
    SparkyCalibrationTemplateRule CalibrationTemplate)
{
    public const int CurrentVersion = 1;
    private const string CatalogPath = "tools/sparky-entity-resolution-rules.json";

    public static SparkyEntityResolutionRules Load(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("A repository root is required.", nameof(repositoryRoot));
        }

        string fullRoot = Path.GetFullPath(repositoryRoot);
        string path = SparkyKnowledgeSourceCatalog.ResolveRepositoryFile(fullRoot, CatalogPath);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !TryReadInt(root, "version", out int version) || version != CurrentVersion ||
            !root.TryGetProperty("tierRules", out JsonElement tierRulesElement) || tierRulesElement.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("calibrationTemplate", out JsonElement calibrationElement) || calibrationElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The Sparky entity-resolution rules catalog has an unsupported or invalid schema.");
        }

        var tierRules = new List<SparkyEntityTierRule>();
        var tierPairs = new HashSet<(SparkyEntityKind Kind, int Type, int? Level)>();
        var normalizedAliases = new Dictionary<(SparkyEntityKind Kind, string Alias), (int Type, int? Level)>();

        foreach (JsonElement element in tierRulesElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("entityKind", out JsonElement kindElement) || kindElement.ValueKind != JsonValueKind.String ||
                !TryParseEntityKind(kindElement.GetString(), out SparkyEntityKind kind) ||
                !TryReadInt(element, "tierType", out int tierType) ||
                !element.TryGetProperty("tierLevel", out JsonElement tierLevelElement) ||
                !TryReadNullableInt(tierLevelElement, out int? tierLevel) ||
                !element.TryGetProperty("canonicalLabel", out JsonElement labelElement) || labelElement.ValueKind != JsonValueKind.String ||
                !element.TryGetProperty("aliasesByLanguage", out JsonElement aliasesElement))
            {
                throw new InvalidDataException("The Sparky entity-resolution rules catalog contains an invalid tier rule.");
            }

            string canonicalLabel = labelElement.GetString()!.Trim();
            if (canonicalLabel.Length == 0 || !IsAllowedPair(kind, tierType, tierLevel) ||
                !tierPairs.Add((kind, tierType, tierLevel)))
            {
                throw new InvalidDataException("The Sparky entity-resolution rules catalog contains an invalid or duplicate entity-kind tier mapping.");
            }

            IReadOnlyDictionary<int, IReadOnlyList<string>> aliases = ReadAliases(aliasesElement, "tier rule");
            if (!aliases.Values.SelectMany(value => value).Any(alias => NormalizeTierAlias(alias) == NormalizeTierAlias(canonicalLabel)))
            {
                throw new InvalidDataException($"The Sparky tier rule '{canonicalLabel}' must include its canonical label as an alias.");
            }

            foreach (string alias in aliases.Values.SelectMany(value => value))
            {
                string normalized = NormalizeTierAlias(alias);
                var key = (kind, normalized);
                var pair = (tierType, tierLevel);
                if (normalizedAliases.TryGetValue(key, out var existingPair) && existingPair != pair)
                {
                    throw new InvalidDataException($"The Sparky tier alias '{alias}' maps to more than one tier pair for {kind} entities.");
                }

                normalizedAliases[key] = pair;
            }

            tierRules.Add(new SparkyEntityTierRule(kind, tierType, tierLevel, canonicalLabel, aliases));
        }

        if (tierRules.Count == 0)
        {
            throw new InvalidDataException("The Sparky entity-resolution rules catalog has no tier rules.");
        }

        if (!calibrationElement.TryGetProperty("suffix", out JsonElement suffixElement) || suffixElement.ValueKind != JsonValueKind.String ||
            !string.Equals(suffixElement.GetString(), "_cprg", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Sparky calibration-template suffix must be exactly '_cprg'.");
        }

        if (!calibrationElement.TryGetProperty("aliasesByLanguage", out JsonElement calibrationAliasesElement))
        {
            throw new InvalidDataException("The Sparky calibration-template rule is missing its aliases.");
        }

        IReadOnlyDictionary<int, IReadOnlyList<string>> calibrationAliases = ReadAliases(calibrationAliasesElement, "calibration-template rule");
        var categoryKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string alias in calibrationAliases.Values.SelectMany(value => value))
        {
            string normalized = NormalizeTierAlias(alias);
            if (!categoryKeys.Add(normalized) || normalizedAliases.ContainsKey((SparkyEntityKind.Item, normalized)))
            {
                throw new InvalidDataException($"The Sparky calibration-template alias '{alias}' is duplicated or conflicts with an item tier alias.");
            }
        }

        var rules = new SparkyEntityResolutionRules(
            version,
            tierRules.AsReadOnly(),
            new SparkyCalibrationTemplateRule(suffixElement.GetString()!, calibrationAliases));
        rules.Validate();
        return rules;
    }

    public void Validate()
    {
        if (Version != CurrentVersion || TierRules is null || TierRules.Count == 0 || CalibrationTemplate is null ||
            !string.Equals(CalibrationTemplate.Suffix, "_cprg", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Sparky entity-resolution rules catalog has an unsupported or invalid schema.");
        }

        var tierPairs = new HashSet<(SparkyEntityKind Kind, int Type, int? Level)>();
        var normalizedAliases = new Dictionary<(SparkyEntityKind Kind, string Alias), (int Type, int? Level)>();
        foreach (SparkyEntityTierRule? rule in TierRules)
        {
            if (rule is null || !Enum.IsDefined(rule.EntityKind) ||
                !IsAllowedPair(rule.EntityKind, rule.TierType, rule.TierLevel) ||
                string.IsNullOrWhiteSpace(rule.CanonicalLabel) ||
                !tierPairs.Add((rule.EntityKind, rule.TierType, rule.TierLevel)) ||
                !HasAliases(rule.AliasesByLanguage))
            {
                throw new InvalidDataException("The Sparky entity-resolution rules catalog contains an invalid or duplicate entity-kind tier mapping.");
            }

            bool canonicalAliasPresent = false;
            foreach (string alias in rule.AliasesByLanguage.Values.SelectMany(value => value))
            {
                string normalized = NormalizeTierAlias(alias);
                canonicalAliasPresent |= normalized == NormalizeTierAlias(rule.CanonicalLabel);
                var key = (rule.EntityKind, normalized);
                var pair = (rule.TierType, rule.TierLevel);
                if (normalizedAliases.TryGetValue(key, out var existingPair) && existingPair != pair)
                {
                    throw new InvalidDataException($"The Sparky tier alias '{alias}' maps to more than one tier pair for {rule.EntityKind} entities.");
                }

                normalizedAliases[key] = pair;
            }

            if (!canonicalAliasPresent)
            {
                throw new InvalidDataException($"The Sparky tier rule '{rule.CanonicalLabel}' must include its canonical label as an alias.");
            }
        }

        if (!HasAliases(CalibrationTemplate.AliasesByLanguage))
        {
            throw new InvalidDataException("The Sparky calibration-template rule has no aliases.");
        }

        var categoryAliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (string alias in CalibrationTemplate.AliasesByLanguage.Values.SelectMany(value => value))
        {
            string normalized = NormalizeTierAlias(alias);
            if (!categoryAliases.Add(normalized) || normalizedAliases.ContainsKey((SparkyEntityKind.Item, normalized)))
            {
                throw new InvalidDataException($"The Sparky calibration-template alias '{alias}' is duplicated or conflicts with an item tier alias.");
            }
        }
    }

    public IReadOnlyList<string> GetAliases(SparkyEntityTierRule rule, int languageId)
    {
        return GetAliases(rule.AliasesByLanguage, languageId);
    }

    public IReadOnlyList<string> GetCalibrationAliases(int languageId)
    {
        return GetAliases(CalibrationTemplate.AliasesByLanguage, languageId);
    }

    public SparkyEntityTierRule? FindTierRule(SparkyEntityKind kind, int? tierType, int? tierLevel)
    {
        return TierRules.FirstOrDefault(rule => rule.EntityKind == kind && rule.TierType == tierType && rule.TierLevel == tierLevel);
    }

    public static string NormalizeTierAlias(string value)
    {
        var normalized = new StringBuilder(value.Length + 8);
        foreach (char character in value.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(character);
            }
            else if (character == '+')
            {
                normalized.Append(" plus ");
            }
            else if (character is '-' or '\u2212')
            {
                normalized.Append(" minus ");
            }
            else
            {
                normalized.Append(' ');
            }
        }

        return string.Join(' ', normalized.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<string> GetAliases(
        IReadOnlyDictionary<int, IReadOnlyList<string>> aliasesByLanguage,
        int languageId)
    {
        var result = new List<string>();
        if (aliasesByLanguage.TryGetValue(languageId, out IReadOnlyList<string>? localized))
        {
            result.AddRange(localized);
        }

        if (languageId != 0 && aliasesByLanguage.TryGetValue(0, out IReadOnlyList<string>? english))
        {
            result.AddRange(english);
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IReadOnlyDictionary<int, IReadOnlyList<string>> ReadAliases(JsonElement element, string description)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"The Sparky {description} aliases must be an object keyed by language ID.");
        }

        var aliasesByLanguage = new Dictionary<int, IReadOnlyList<string>>();
        foreach (JsonProperty languageProperty in element.EnumerateObject())
        {
            if (!int.TryParse(languageProperty.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int languageId) || languageId < 0 ||
                languageProperty.Value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"The Sparky {description} contains an invalid language alias list.");
            }

            string[] aliases = languageProperty.Value.EnumerateArray()
                .Select(value => value.ValueKind == JsonValueKind.String
                    ? value.GetString()!.Trim()
                    : throw new InvalidDataException($"The Sparky {description} contains a non-string alias."))
                .ToArray();
            if (aliases.Length == 0 || aliases.Any(string.IsNullOrWhiteSpace) || aliases.Any(alias => NormalizeTierAlias(alias).Length == 0))
            {
                throw new InvalidDataException($"The Sparky {description} contains an empty alias list or blank alias.");
            }

            if (!aliasesByLanguage.TryAdd(languageId, Array.AsReadOnly(aliases)))
            {
                throw new InvalidDataException($"The Sparky {description} contains duplicate language IDs.");
            }
        }

        if (aliasesByLanguage.Count == 0)
        {
            throw new InvalidDataException($"The Sparky {description} has no aliases.");
        }

        return new ReadOnlyDictionary<int, IReadOnlyList<string>>(aliasesByLanguage);
    }

    private static bool HasAliases(IReadOnlyDictionary<int, IReadOnlyList<string>>? aliasesByLanguage)
    {
        return aliasesByLanguage is { Count: > 0 } &&
               aliasesByLanguage.All(entry => entry.Key >= 0 && entry.Value is { Count: > 0 } &&
                   entry.Value.All(alias => !string.IsNullOrWhiteSpace(alias) && NormalizeTierAlias(alias).Length > 0));
    }

    private static bool TryParseEntityKind(string? value, out SparkyEntityKind kind)
    {
        if (string.Equals(value, "item", StringComparison.OrdinalIgnoreCase))
        {
            kind = SparkyEntityKind.Item;
            return true;
        }

        if (string.Equals(value, "robot", StringComparison.OrdinalIgnoreCase))
        {
            kind = SparkyEntityKind.Robot;
            return true;
        }

        kind = default;
        return false;
    }

    private static bool IsAllowedPair(SparkyEntityKind kind, int tierType, int? tierLevel)
    {
        return kind switch
        {
            SparkyEntityKind.Item => tierType switch
            {
                1 => tierLevel is >= 0 and <= 5,
                2 => tierLevel is >= 1 and <= 4,
                3 => tierLevel is 0 or 2 or 3 or 4,
                _ => false
            },
            SparkyEntityKind.Robot => tierType == 1 && tierLevel is >= 1 and <= 3 || tierType == 2 && tierLevel is null,
            _ => false
        };
    }

    private static bool TryReadInt(JsonElement parent, string property, out int value)
    {
        value = default;
        return parent.TryGetProperty(property, out JsonElement element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool TryReadNullableInt(JsonElement element, out int? value)
    {
        value = null;
        if (element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int number))
        {
            value = number;
            return true;
        }

        return false;
    }
}
