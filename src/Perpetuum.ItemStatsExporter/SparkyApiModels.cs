using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Perpetuum.ItemStatsExporter;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyApiRequest(
    Guid? RequestId,
    string? Question,
    string? ContextItem = null,
    int? ContextLanguageId = null,
    IReadOnlyList<SparkyConversationTurn>? ContextTurns = null,
    string? ContextSummary = null,
    SparkyPendingClarification? PendingClarification = null,
    string? CallerRank = null)
{
    public const int MaximumContextTurns = 16;
    public const int MaximumContextCharacters = 8000;
    public const int MaximumTurnCharacters = 2000;
    public const int MaximumSummaryCharacters = 700;
    public const int MaximumCandidates = 8;
    public const int MaximumCandidateCharacters = 120;

    public bool TryResolveRequestId(SparkyApiProfile profile, out Guid requestId)
    {
        if (RequestId is { } providedRequestId && providedRequestId != Guid.Empty)
        {
            requestId = providedRequestId;
            return true;
        }

        if (RequestId is null && profile == SparkyApiProfile.Dev)
        {
            requestId = Guid.NewGuid();
            return true;
        }

        requestId = Guid.Empty;
        return false;
    }

    public bool TryValidateConversationContext(out string error)
    {
        error = "invalid_conversation_context";
        if (ContextTurns is { Count: > MaximumContextTurns } ||
            ContextSummary?.Length > MaximumSummaryCharacters)
        {
            return false;
        }

        int totalCharacters = 0;
        foreach (var turn in ContextTurns ?? [])
        {
            if (turn is null || turn.Role is not ("user" or "assistant") ||
                string.IsNullOrWhiteSpace(turn.Content) || turn.Content.Length > MaximumTurnCharacters ||
                ContainsUnsupportedControlCharacters(turn.Content))
            {
                return false;
            }

            totalCharacters += turn.Content.Length;
            if (totalCharacters > MaximumContextCharacters)
            {
                return false;
            }
        }

        if (ContextSummary is not null && ContainsUnsupportedControlCharacters(ContextSummary))
        {
            return false;
        }

        if (PendingClarification is { } pending &&
            (string.IsNullOrWhiteSpace(pending.OriginalQuestion) ||
             pending.OriginalQuestion.Length > MaximumTurnCharacters ||
             ContainsUnsupportedControlCharacters(pending.OriginalQuestion) ||
             pending.RequestedAnswerType?.Length > 80 ||
             pending.RequestedAnswerType?.Any(char.IsControl) == true ||
             pending.CandidateNames is null ||
             pending.CandidateNames.Count is < 1 or > MaximumCandidates ||
             pending.CandidateNames.Any(candidate => string.IsNullOrWhiteSpace(candidate) ||
                                                     candidate.Length > MaximumCandidateCharacters ||
                                                     ContainsUnsupportedControlCharacters(candidate)) ||
             pending.CandidateNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != pending.CandidateNames.Count))
        {
            return false;
        }

        if (CallerRank?.Length > 40 || CallerRank?.Any(char.IsControl) == true)
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public SparkyCallerRank NormalizeCallerRank() =>
        Enum.TryParse(CallerRank, ignoreCase: true, out SparkyCallerRank rank) && Enum.IsDefined(rank)
            ? rank
            : SparkyCallerRank.Agent;

    private static bool ContainsUnsupportedControlCharacters(string value) =>
        value.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t'));
}

internal sealed record SparkyApiResponse(
    string Status,
    string? Item = null,
    string? Answer = null,
    IReadOnlyList<string>? Candidates = null,
    int? LanguageId = null,
    SparkyConversationUpdate? ContextUpdate = null);

internal sealed record SparkyApiError(string Error);

internal sealed record SparkyConversationTurn(string Role, string Content);

internal sealed record SparkyPendingClarification(
    string OriginalQuestion,
    string? RequestedAnswerType,
    IReadOnlyList<string> CandidateNames);

internal sealed record SparkyConversationUpdate(
    string? Summary,
    string? ResolvedEntity,
    SparkyPendingClarification? PendingClarification);

internal enum SparkyCallerRank
{
    Technician,
    Agent,
    Officer,
    CEO,
    CommandDirector
}

internal sealed record SparkyLocalizedAnswer(int LanguageId, string Answer, string? FinishStatus = null);

internal enum SparkyApiProfile
{
    Dev,
    Live
}

internal sealed class SparkyApiCredentialResolver
{
    private readonly (SparkyApiProfile Profile, string Token)[] _credentials;

    public SparkyApiCredentialResolver(string devToken, string? liveToken)
    {
        if (!IsValidToken(devToken) || (liveToken is not null && !IsValidToken(liveToken)))
        {
            throw new InvalidDataException("Sparky API credentials must be non-empty and meet the configured length bounds.");
        }

        if (liveToken is not null && TokensEqual(devToken, liveToken))
        {
            throw new InvalidDataException("Sparky Dev and Live API credentials must be distinct.");
        }

        _credentials = liveToken is null
            ? [(SparkyApiProfile.Dev, devToken)]
            : [(SparkyApiProfile.Dev, devToken), (SparkyApiProfile.Live, liveToken)];
    }

    public bool TryResolve(string? presentedToken, out SparkyApiProfile profile)
    {
        profile = default;
        if (string.IsNullOrEmpty(presentedToken) || presentedToken.Length > 4096)
        {
            return false;
        }

        var presentedBytes = Encoding.UTF8.GetBytes(presentedToken);
        try
        {
            bool resolved = false;
            foreach (var credential in _credentials)
            {
                var expectedBytes = Encoding.UTF8.GetBytes(credential.Token);
                try
                {
                    bool matches = expectedBytes.Length == presentedBytes.Length &&
                                   CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
                    if (matches)
                    {
                        profile = credential.Profile;
                        resolved = true;
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(expectedBytes);
                }
            }

            return resolved;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presentedBytes);
        }
    }

    private static bool IsValidToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        token.Length is >= 32 and <= 4096 &&
        !token.Any(char.IsControl);

    private static bool TokensEqual(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        try
        {
            return leftBytes.Length == rightBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }
}
