using Microsoft.Data.SqlClient;
using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Data
{
    public interface IAuthenticator
    {
        Task<AuthOutcome> AuthenticateAsync(string email, string password);
    }

    public interface IAuthenticatorFactory
    {
        IAuthenticator Create(ConnectionSettings connection);
    }

    public sealed class AuthenticatorFactory : IAuthenticatorFactory
    {
        public IAuthenticator Create(ConnectionSettings connection)
        {
            return new Authenticator(connection);
        }
    }

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
        public AdminAccessLevel AccessLevel { get; init; } = AdminAccessLevel.NotDefined;
        public string? Email { get; init; }
        public string? ErrorMessage { get; init; }
    }

    public class Authenticator : IAuthenticator
    {
        private readonly ConnectionSettings _connection;
        private readonly AdminAccessLevel _minimumAccess;

        public Authenticator(
            ConnectionSettings connection,
            AdminAccessLevel minimumAccess = AdminAccessLevel.GameAdmin)
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
                cmd.Parameters.Add(new SqlParameter("@password", PasswordHash.Compute(password)));

                await using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return new AuthOutcome { Result = AuthResult.InvalidCredentials };
                }

                var accountId = reader.GetInt32(0);
                var accLevel = (AdminAccessLevel)reader.GetInt32(1);

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
    }
}
