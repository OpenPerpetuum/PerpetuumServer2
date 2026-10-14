using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Perpetuum.ItemStatsExporter;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record SparkyApiRequest(
    Guid? RequestId,
    string? Question,
    string? ContextItem = null,
    int? ContextLanguageId = null)
{
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
}

internal sealed record SparkyApiResponse(
    string Status,
    string? Item = null,
    string? Answer = null,
    IReadOnlyList<string>? Candidates = null,
    int? LanguageId = null);

internal sealed record SparkyApiError(string Error);

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
