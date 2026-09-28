using System;

namespace Perpetuum.CliClient
{
    public class CommandLineOptions
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 17700;
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PasswordHash { get; set; }
        public string? Character { get; set; }
        public string? Channel { get; set; }
        public string? ChatMessage { get; set; }
        public bool ListChannels { get; set; }
        public bool ListStorage { get; set; }
        public bool CreateAccount { get; set; }
        public string? CreateCharacterNick { get; set; }
        public int RaceId { get; set; } = 1;
        public int SchoolId { get; set; } = 1;
        public int MajorId { get; set; } = 1;
        public int SparkId { get; set; } = 1;
        public bool NonInteractive { get; set; }
        public bool ShowHelp { get; set; }

        public static CommandLineOptions Parse(string[] args)
        {
            var options = new CommandLineOptions();

            // Check environment variables first as defaults
            var envHost = Environment.GetEnvironmentVariable("PERPETUUM_HOST");
            if (!string.IsNullOrWhiteSpace(envHost))
            {
                options.Host = envHost;
            }

            var envPort = Environment.GetEnvironmentVariable("PERPETUUM_PORT");
            if (!string.IsNullOrWhiteSpace(envPort) && int.TryParse(envPort, out var p))
            {
                options.Port = p;
            }

            var envEmail = Environment.GetEnvironmentVariable("PERPETUUM_EMAIL");
            if (!string.IsNullOrWhiteSpace(envEmail))
            {
                options.Email = envEmail;
            }

            var envPass = Environment.GetEnvironmentVariable("PERPETUUM_PASSWORD");
            if (!string.IsNullOrWhiteSpace(envPass))
            {
                options.Password = envPass;
            }

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                switch (arg.ToLowerInvariant())
                {
                    case "--help":
                    case "-help":
                    case "-?":
                    case "/?":
                        options.ShowHelp = true;
                        break;

                    case "--host":
                    case "-h":
                        if (i + 1 < args.Length) options.Host = args[++i];
                        break;

                    case "--port":
                    case "-p":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var portVal)) options.Port = portVal;
                        break;

                    case "--email":
                    case "-e":
                    case "--user":
                    case "-u":
                        if (i + 1 < args.Length) options.Email = args[++i];
                        break;

                    case "--password":
                    case "--pass":
                        if (i + 1 < args.Length) options.Password = args[++i];
                        break;

                    case "--password-hash":
                    case "--hash":
                        if (i + 1 < args.Length) options.PasswordHash = args[++i];
                        break;

                    case "--character":
                    case "-c":
                    case "--char":
                        if (i + 1 < args.Length) options.Character = args[++i];
                        break;

                    case "--channel":
                    case "--chan":
                        if (i + 1 < args.Length) options.Channel = args[++i];
                        break;

                    case "--chat-msg":
                    case "--msg":
                        if (i + 1 < args.Length) options.ChatMessage = args[++i];
                        break;

                    case "--list-channels":
                    case "--channels":
                        options.ListChannels = true;
                        break;

                    case "--storage":
                    case "--list-storage":
                    case "--hangar":
                    case "--inventory":
                    case "-s":
                        options.ListStorage = true;
                        break;

                    case "--create-account":
                    case "--register":
                        options.CreateAccount = true;
                        break;

                    case "--create-char":
                    case "--create-character":
                    case "-cc":
                        if (i + 1 < args.Length) options.CreateCharacterNick = args[++i];
                        break;

                    case "--race":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var raceVal)) options.RaceId = raceVal;
                        break;

                    case "--school":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var schoolVal)) options.SchoolId = schoolVal;
                        break;

                    case "--major":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var majorVal)) options.MajorId = majorVal;
                        break;

                    case "--spark":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var sparkVal)) options.SparkId = sparkVal;
                        break;

                    case "--non-interactive":
                    case "-n":
                    case "--batch":
                        options.NonInteractive = true;
                        break;

                    default:
                        if (arg.StartsWith("--host=", StringComparison.OrdinalIgnoreCase))
                            options.Host = arg.Substring(7);
                        else if (arg.StartsWith("--port=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring(7), out var pv))
                            options.Port = pv;
                        else if (arg.StartsWith("--email=", StringComparison.OrdinalIgnoreCase))
                            options.Email = arg.Substring(8);
                        else if (arg.StartsWith("--password=", StringComparison.OrdinalIgnoreCase))
                            options.Password = arg.Substring(11);
                        else if (arg.StartsWith("--character=", StringComparison.OrdinalIgnoreCase))
                            options.Character = arg.Substring(12);
                        else if (arg.StartsWith("--create-char=", StringComparison.OrdinalIgnoreCase))
                            options.CreateCharacterNick = arg.Substring(14);
                        else if (arg.StartsWith("--create-character=", StringComparison.OrdinalIgnoreCase))
                            options.CreateCharacterNick = arg.Substring(19);
                        else if (arg.StartsWith("--race=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring(7), out var rv))
                            options.RaceId = rv;
                        else if (arg.StartsWith("--school=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring(9), out var sv))
                            options.SchoolId = sv;
                        else if (arg.StartsWith("--major=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring(8), out var mv))
                            options.MajorId = mv;
                        else if (arg.StartsWith("--spark=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg.Substring(8), out var spv))
                            options.SparkId = spv;
                        else if (arg.StartsWith("--channel=", StringComparison.OrdinalIgnoreCase))
                            options.Channel = arg.Substring(10);
                        else if (arg.StartsWith("--chat-msg=", StringComparison.OrdinalIgnoreCase))
                            options.ChatMessage = arg.Substring(11);
                        break;
                }
            }

            return options;
        }

        public static void PrintHelp()
        {
            Console.WriteLine(@"Perpetuum Server CLI Client

Usage:
  dotnet run --project src/Perpetuum.CliClient [options]

Options:
  -h, --host <host>           Server hostname or IP (default: 127.0.0.1 / PERPETUUM_HOST)
  -p, --port <port>           Server port (default: 17700 / PERPETUUM_PORT)
  -e, --email <email>         Account email / username (default: PERPETUUM_EMAIL)
      --password <pass>       Account plaintext password (default: PERPETUUM_PASSWORD)
      --password-hash <hash>  Account SHA-1 password hash (40-hex)
  -c, --character <id|name>   Character ID or nickname to auto-select
      --channel <name>        Chat channel to join/select
      --chat-msg <text>       Chat message to send to the channel
      --list-channels         List available chat channels
  -s, --storage               List private station storage after character selection
      --create-account        Create a new account on the server
      --create-char <nick>    Create a new character with nickname
      --race <1-3>            Character race (1=Truxal, 2=Thelodica, 3=Nia-Korp; default: 1)
      --school <1-3>          Character school (1=Military, 2=Industrial, 3=Syndicate; default: 1)
      --major <1-5>           Character major (default: 1)
      --spark <1-5>           Character spark (default: 1)
  -n, --non-interactive       Run non-interactively (exit after character selection/message)
      --help                  Show this help text

Examples:
  # Interactive mode
  dotnet run --project src/Perpetuum.CliClient

  # Connect, create a character, and select it
  dotnet run --project src/Perpetuum.CliClient -- -e pilot@example.com --password secret --create-char NewPilot

  # Connect, select character, and join shell
  dotnet run --project src/Perpetuum.CliClient -- -h 127.0.0.1 -p 17700 -e pilot@example.com --password secret -c PilotName

  # Non-interactive / scripting: send a chat message
  dotnet run --project src/Perpetuum.CliClient -- -e pilot@example.com --password secret -c PilotName --channel General --chat-msg ""Hello world!"" -n
");
        }
    }
}
