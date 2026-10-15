using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Perpetuum.ItemStatsExporter;

internal static class LocalModelAnswerer
{
    private static readonly HttpClient Client = new() { Timeout = Timeout.InfiniteTimeSpan };

    internal static string BuildClarificationGuidance() =>
        "This is a clarification-only response. Treat Reason as lookup-result metadata. CandidateNames are exact possible catalog matches only. RequestedTerm and MissingEntities came from player wording and are not verified facts. Do not describe the subject or infer why it is missing. State briefly what the local records could not verify, then ask one useful clarification. If an exact game term is missing, invite the player to provide its English name while keeping your reply in their language.";

    internal static object ToModelItemFacts(ItemStatsLookupAnswer item) => new
    {
        displayName = item.DisplayName,
        stats = item.Stats.Select(stat => new { stat.Label, stat.Value, stat.Unit }).ToArray(),
        ammoVariants = item.AmmoVariants.Select(ammo => new
        {
            ammo.DisplayName,
            damage = ammo.Damage.Select(damage => new { damage.Type, damage.Value }).ToArray()
        }).ToArray(),
        recipe = item.Recipe is null
            ? null
            : new
            {
                item.Recipe.OutputQuantity,
                components = item.Recipe.Components.Select(component => new { component.DisplayName, component.Amount }).ToArray()
            },
        item.RecipeAvailable,
        item.RecipeTranslationsComplete,
        fullMaterials = item.FullMaterials is null
            ? null
            : new
            {
                item.FullMaterials.OutputQuantity,
                materials = item.FullMaterials.Materials.Select(material => new { material.DisplayName, material.Amount }).ToArray()
            },
        item.FullMaterialsAvailable
    };

    public static async Task<string> AnswerAsync(
        string baseUrl,
        string modelId,
        string question,
        ItemStatsLookupAnswer verifiedFacts,
        int maxAnswerCharacters,
        int maxCompletionTokens,
        TimeSpan timeout,
        string? apiToken = null,
        CancellationToken cancellationToken = default)
    {
        SparkyLocalizedAnswer answer = await AnswerGroundedAsync(
            baseUrl,
            modelId,
            question,
            0,
            "English",
            ToModelItemFacts(verifiedFacts),
            maxAnswerCharacters,
            maxCompletionTokens,
            timeout,
            apiToken,
            cancellationToken,
            requiredExactStrings: SparkyRecipeAnswerFormatter.GetRequiredExactStrings(question, question, verifiedFacts)).ConfigureAwait(false);
        return answer.Answer;
    }

    public static Task<SparkyLocalizedAnswer> AnswerLocalizedAsync(
        string baseUrl,
        string modelId,
        string question,
        ItemStatsLookupAnswer verifiedFacts,
        int languageId,
        string languageName,
        int maxAnswerCharacters,
        int maxCompletionTokens,
        TimeSpan timeout,
        string? apiToken = null,
        CancellationToken cancellationToken = default,
        string? personaStyle = null) => AnswerGroundedAsync(
            baseUrl,
            modelId,
            question,
            languageId,
            languageName,
            ToModelItemFacts(verifiedFacts),
            maxAnswerCharacters,
            maxCompletionTokens,
            timeout,
            apiToken,
            cancellationToken,
            personaStyle,
            requiredExactStrings: SparkyRecipeAnswerFormatter.GetRequiredExactStrings(question, question, verifiedFacts));
    public static Task<SparkyLocalizedAnswer> AnswerBanterAsync(
        string baseUrl,
        string modelId,
        string question,
        int languageId,
        string languageName,
        int maxAnswerCharacters,
        int maxCompletionTokens,
        TimeSpan timeout,
        string? apiToken = null,
        CancellationToken cancellationToken = default,
        string? personaStyle = null) => AnswerGroundedAsync(
            baseUrl,
            modelId,
            question,
            languageId,
            languageName,
            new { kind = "small talk" },
            maxAnswerCharacters,
            maxCompletionTokens,
            timeout,
            apiToken,
            cancellationToken,
            personaStyle,
            SparkyAnswerOutcome.SmallTalk);
    public static async Task<SparkyLocalizedAnswer> AnswerGroundedAsync(
        string baseUrl,
        string modelId,
        string question,
        int languageId,
        string languageName,
        object verifiedEvidence,
        int maxAnswerCharacters,
        int maxCompletionTokens,
        TimeSpan timeout,
        string? apiToken = null,
        CancellationToken cancellationToken = default,
        string? personaStyle = null,
        SparkyAnswerOutcome? outcome = null,
        IReadOnlyList<SparkyConversationTurn>? contextTurns = null,
        string? contextSummary = null,
        SparkyPendingClarification? pendingClarification = null,
        SparkyCallerRank callerRank = SparkyCallerRank.Agent,
        IReadOnlyList<string>? requiredExactStrings = null,
        HttpClient? modelClient = null,
        double answerTemperature = 0.2,
        double answerTopP = 0.9)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/'), UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https") ||
            (baseUri.Scheme == "http" && !baseUri.IsLoopback))
        {
            throw new InvalidDataException("SPARKY_LMSTUDIO_BASE_URL must be an absolute loopback HTTP or HTTPS base URL.");
        }

        string robotFactGuidance = verifiedEvidence is SparkyRobotAnswerFacts robotFacts
            ? robotFacts.Focus switch
            {
                SparkyRobotQuestionFocus.ModuleCompatibility when robotFacts.Module is not null =>
                    "For robot module compatibility, HasStaticSlotEligibility means only that at least one component slot passes static slot and allowed-robot rules. CurrentLoadoutFitEvaluated is false: occupied slots and total CPU/power-grid capacity were not checked. Do not claim the module fits the player's current robot. State eligible slots and include CompatibilityScope exactly as supplied.",
                SparkyRobotQuestionFocus.Fastest =>
                    "SpeedRanking contains the tied fastest assembled robot definitions and their verified baseline maximum speeds. Report only those entries; this baseline excludes player-specific fit, extension levels, and effects.",
                SparkyRobotQuestionFocus.CompareStats =>
                    "Comparisons contains values and precomputed second-minus-first differences for matching stat fields. Use those exact differences; do not calculate or infer additional values.",
                _ => string.Empty
            }
            : string.Empty;
        string clarificationGuidance = verifiedEvidence is SparkyClarificationEvidence
            ? BuildClarificationGuidance()
            : string.Empty;
        string seasonStandingGuidance = verifiedEvidence is SparkySeasonStandingAnswerFacts seasonFacts
            ? SparkySeasonStandingResolver.GetPromptGuidance(seasonFacts)
            : string.Empty;
        SparkyAnswerOutcome selectedOutcome = outcome ??
            (verifiedEvidence is SparkyClarificationEvidence
                ? SparkyAnswerOutcome.Clarification
                : SparkyAnswerOutcome.FactualAnswer);
        string systemPrompt = SparkyResponseComposer.BuildSystemPrompt(
            languageId,
            languageName,
            selectedOutcome,
            callerRank,
            personaStyle,
            $"{robotFactGuidance}\n{clarificationGuidance}\n{seasonStandingGuidance}");
        string userPayload = SparkyResponseComposer.BuildUserPayload(
            question,
            contextTurns,
            contextSummary,
            pendingClarification,
            verifiedEvidence,
            requiredExactStrings);
        string retryConstraint = string.Empty;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            var payload = new
            {
                model = modelId,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt + retryConstraint },
                    new { role = "user", content = userPayload }
                },
                temperature = Math.Clamp(double.IsFinite(answerTemperature) ? answerTemperature : 0.2, 0, 2),
                top_p = Math.Clamp(double.IsFinite(answerTopP) ? answerTopP : 0.9, 0.01, 1),
                max_tokens = maxCompletionTokens,
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{baseUrl.TrimEnd('/')}/chat/completions"))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            using var timeoutSource = new CancellationTokenSource(timeout);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            if (!string.IsNullOrWhiteSpace(apiToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            }

            using var response = await (modelClient ?? Client).SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCancellation.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"LM Studio returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
            }

            using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync(linkedCancellation.Token).ConfigureAwait(false));
            if (responseJson.RootElement.ValueKind != JsonValueKind.Object ||
                !responseJson.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0 ||
                !choices[0].TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var contentElement) ||
                contentElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("LM Studio returned no structured assistant answer.");
            }

            string? content = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidDataException($"LM Studio returned an empty grounded answer. {DescribeEmptyAnswer(responseJson.RootElement, choices[0], message)}");
            }

            if (SparkyResponseComposer.TryParseAnswer(
                    content ?? string.Empty,
                    languageId,
                    maxAnswerCharacters,
                    requiredExactStrings,
                    out string answer,
                    out string validationError))
            {
                return new SparkyLocalizedAnswer(languageId, answer, ReadFinishStatus(choices[0]));
            }

            if (attempt == 1)
            {
                throw new InvalidDataException($"LM Studio returned an unusable grounded answer after one retry ({validationError}).");
            }

            retryConstraint = $"\n\nRetry correction: the previous output violated {validationError}. Return a fresh JSON object in languageId {languageId}; preserve every requiredExactStrings value verbatim; keep answer within {maxAnswerCharacters} characters; do not include internal identifiers.";
        }

        throw new InvalidOperationException("The grounded answer retry loop ended unexpectedly.");
    }

    private static string? ReadFinishStatus(JsonElement choice) =>
        choice.TryGetProperty("finish_reason", out var finishReason) &&
        finishReason.ValueKind == JsonValueKind.String
            ? finishReason.GetString()
            : null;

    private static string DescribeEmptyAnswer(JsonElement root, JsonElement choice, JsonElement message)
    {
        var finishReason = choice.TryGetProperty("finish_reason", out var finishReasonElement)
            ? finishReasonElement.ToString()
            : "unavailable";
        var completionTokens = TryReadInteger(root, "usage", "completion_tokens");
        var reasoningTokens = TryReadInteger(root, "usage", "completion_tokens_details", "reasoning_tokens");
        var hasReasoningContent = message.TryGetProperty("reasoning_content", out var reasoningElement) &&
                                  reasoningElement.ValueKind == JsonValueKind.String &&
                                  !string.IsNullOrWhiteSpace(reasoningElement.GetString());

        return $"finish_reason={finishReason}; completion_tokens={completionTokens}; " +
               $"reasoning_tokens={reasoningTokens}; reasoning_content_present={hasReasoningContent}.";
    }

    private static string TryReadInteger(JsonElement element, params string[] propertyPath)
    {
        foreach (var property in propertyPath)
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty(property, out element))
            {
                return "unavailable";
            }
        }

        return element.TryGetInt32(out var value) ? value.ToString() : "unavailable";
    }

}
