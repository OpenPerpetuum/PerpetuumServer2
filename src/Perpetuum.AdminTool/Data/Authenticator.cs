using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Data
{
    public enum AuthResult
    {
        Success,
        InvalidCredentials,
        InsufficientAccess,
        ConnectionFailed
    }

    public class AuthOutcome
    {
        public AuthResult Result { get; init; }
        public int? AccountId { get; init; }
        public AccessLevel AccessLevel { get; init; } = AccessLevel.notDefined;
        public string? Email { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public class Authenticator
    {
        private readonly ConnectionSettings _connection;
        private readonly AccessLevel _minimumAccess;

        public Authenticator(ConnectionSettings connection, AccessLevel minimumAccess = AccessLevel.gameAdmin)
        {
            _connection = connection;
            _minimumAccess = minimumAccess;
        }

        public async Task<AuthOutcome> AuthenticateAsync(string email, string password)
        {
            try
            {
                await using var cn = new SqlConnection(_connection.BuildConnectionString());
                await cn.OpenAsync();

                await using var cmd = cn.CreateCommand();
                cmd.CommandText =
                    "select accountid, accLevel from accounts " +
                    "where email = @email and password = @password";
                cmd.Parameters.Add(new SqlParameter("@email", email));
                cmd.Parameters.Add(new SqlParameter("@password", ToSha1(password)));

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new AuthOutcome { Result = AuthResult.InvalidCredentials };
                }

                var accountId = reader.GetInt32(0);
                var accLevel = (AccessLevel)reader.GetInt32(1);

                if ((accLevel & _minimumAccess) != _minimumAccess)
                {
                    return new AuthOutcome
                    {
                        Result = AuthResult.InsufficientAccess,
                        AccountId = accountId,
                        AccessLevel = accLevel,
                        Email = email
                    };
                }

                return new AuthOutcome
                {
                    Result = AuthResult.Success,
                    AccountId = accountId,
                    AccessLevel = accLevel,
                    Email = email
                };
            }
            catch (Exception ex)
            {
                return new AuthOutcome
                {
                    Result = AuthResult.ConnectionFailed,
                    ErrorMessage = ex.Message
                };
            }
        }

        private string ToSha1(string input)
        {
            if (input.IsNullOrEmpty())
            {
                return "";
            }

            System.Text.ASCIIEncoding encoding = new();
            byte[] bytes = encoding.GetBytes(input);

            System.Security.Cryptography.SHA1CryptoServiceProvider sha = new();

            string result = System.BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);

            return result;
        }
    }
}
