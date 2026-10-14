using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Perpetuum.ItemStatsExporter;

internal sealed class SparkyAdminAuthentication : IDisposable
{
    private readonly byte[] _adminTokenHash;
    private readonly byte[][] _gameTokenHashes;

    public SparkyAdminAuthentication(string adminToken, params string?[] gameTokens)
    {
        if (!IsValidToken(adminToken))
        {
            throw new InvalidDataException("SPARKY_ADMIN_API_TOKEN must contain at least 32 valid characters.");
        }

        _adminTokenHash = HashToken(adminToken);
        _gameTokenHashes = gameTokens
            .Where(token => !string.IsNullOrEmpty(token))
            .Select(token => HashToken(token!))
            .ToArray();

        if (_gameTokenHashes.Any(tokenHash => CryptographicOperations.FixedTimeEquals(_adminTokenHash, tokenHash)))
        {
            Dispose();
            throw new InvalidDataException("The Sparky admin token must be distinct from Dev and Live API credentials.");
        }
    }

    public bool IsAuthorized(string? authorizationHeader)
    {
        if (!AuthenticationHeaderValue.TryParse(authorizationHeader, out var authorization) ||
            !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            authorization.Parameter is not { Length: >= 32 and <= 4096 } parameter ||
            parameter.Any(char.IsControl))
        {
            return false;
        }

        byte[] presentedHash = HashToken(parameter);
        try
        {
            bool matches = CryptographicOperations.FixedTimeEquals(_adminTokenHash, presentedHash);
            foreach (byte[] gameHash in _gameTokenHashes)
            {
                matches |= CryptographicOperations.FixedTimeEquals(gameHash, presentedHash);
            }

            return matches && !_gameTokenHashes.Any(gameHash => CryptographicOperations.FixedTimeEquals(gameHash, presentedHash));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(presentedHash);
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_adminTokenHash);
        foreach (byte[] tokenHash in _gameTokenHashes)
        {
            CryptographicOperations.ZeroMemory(tokenHash);
        }
    }

    private static bool IsValidToken(string? token) =>
        !string.IsNullOrWhiteSpace(token) &&
        token.Length is >= 32 and <= 4096 &&
        !token.Any(char.IsControl);

    private static byte[] HashToken(string token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(token);
        try
        {
            return SHA256.HashData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
