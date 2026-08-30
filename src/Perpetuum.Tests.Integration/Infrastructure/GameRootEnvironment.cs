using Newtonsoft.Json;

namespace Perpetuum.Tests.Integration.Infrastructure
{
    /// <summary>
    /// Reads the same perpetuum.ini the server reads, from the directory named by
    /// PERPETUUM_GAMEROOT. The file is JSON deserialized into GlobalConfiguration by
    /// PerpetuumBootstrapper; this does the same, so the connection string can never drift
    /// from the one the server uses.
    /// </summary>
    public sealed class GameRootEnvironment
    {
        public const string GameRootVariable = "PERPETUUM_GAMEROOT";
        public const string AllowWriteVariable = "PERPETUUM_TESTDB_ALLOW_WRITE";

        public required string GameRoot { get; init; }
        public required string ConnectionString { get; init; }

        public static bool WritesAllowed
            => Environment.GetEnvironmentVariable(AllowWriteVariable) == "1";

        public static bool TryLoad(out GameRootEnvironment? environment, out string? reason)
        {
            environment = null;

            string? gameRoot = Environment.GetEnvironmentVariable(GameRootVariable);
            if (string.IsNullOrWhiteSpace(gameRoot))
            {
                reason = $"{GameRootVariable} is not set.";
                return false;
            }

            if (!Directory.Exists(gameRoot))
            {
                reason = $"{GameRootVariable} points at a directory that does not exist: {gameRoot}";
                return false;
            }

            string iniPath = Path.Combine(gameRoot, "perpetuum.ini");
            if (!File.Exists(iniPath))
            {
                reason = $"perpetuum.ini not found under {gameRoot}";
                return false;
            }

            GlobalConfiguration? configuration;
            try
            {
                configuration = JsonConvert.DeserializeObject<GlobalConfiguration>(File.ReadAllText(iniPath));
            }
            catch (Exception ex)
            {
                reason = $"perpetuum.ini could not be parsed: {ex.Message}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
            {
                reason = "perpetuum.ini carries no ConnectionString.";
                return false;
            }

            environment = new GameRootEnvironment
            {
                GameRoot = gameRoot,
                ConnectionString = configuration.ConnectionString,
            };
            reason = null;
            return true;
        }
    }
}
