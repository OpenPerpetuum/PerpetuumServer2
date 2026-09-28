using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Perpetuum.CliClient.Models;
using Perpetuum.GenXY;

namespace Perpetuum.CliClient
{
    internal class Program
    {
        private static PerpetuumClient? _client;
        private static bool _exitRequested;
        private static string _selectedCharacterNick = string.Empty;
        private static long _selectedCharacterBaseEid;
        private static string _selectedCharacterBaseName = string.Empty;
        private static bool _selectedCharacterIsDocked;
        private static string _activeChannelName = string.Empty;
        private static List<ChannelInfo> _lastChannelsList = new();

        private static async Task<int> Main(string[] args)
        {
            var options = CommandLineOptions.Parse(args);
            if (options.ShowHelp)
            {
                CommandLineOptions.PrintHelp();
                return 0;
            }

            ConsoleHelper.WriteBanner();

            using var client = new PerpetuumClient();
            _client = client;

            // Setup graceful shutdown on Ctrl+C
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                _exitRequested = true;
                ConsoleHelper.Warn("Shutdown requested, closing session...");
                Task.Run(async () =>
                {
                    try
                    {
                        if (_client?.SelectedCharacter != null)
                        {
                            await _client.DeselectCharacterAsync();
                        }
                        if (_client?.Account != null)
                        {
                            await _client.SignOutAsync();
                        }
                    }
                    catch { }
                    finally
                    {
                        _client?.Disconnect();
                        Environment.Exit(0);
                    }
                });
            };

            // Register background message logging
            client.MessageReceived += msg =>
            {
                if (msg.Command == Commands.SignIn || msg.Command == Commands.CharacterList || msg.Command == Commands.CharacterSelect || msg.Command == Commands.Welcome || msg.Command == Commands.ChannelNotification)
                {
                    return; // already handled by command response tasks or chat event
                }

                var sender = (msg as Message)?.Sender ?? string.Empty;
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine($"\n[SERVER MSG] Command: {msg.Command?.Text} Sender: {sender}");
                if (msg.Data != null && msg.Data.Count > 0)
                {
                    foreach (var kvp in msg.Data)
                    {
                        Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
                    }
                }
                Console.ResetColor();
            };

            // Register chat message notifications
            client.ChatMessageReceived += chat =>
            {
                ConsoleHelper.PrintChatMessage(chat);
            };

            client.Disconnected += () =>
            {
                if (!_exitRequested)
                {
                    ConsoleHelper.Warn("Disconnected from server.");
                }
            };

            try
            {
                // Step 1: Connect & Handshake
                ConsoleHelper.Info($"Connecting to Perpetuum Server at {options.Host}:{options.Port}...");
                var welcome = await client.ConnectAsync(options.Host, options.Port);
                ConsoleHelper.Success("Connected and RSA/RC4 encryption handshake established.");
                ConsoleHelper.PrintWelcome(welcome, $"{options.Host}:{options.Port}");

                // Step 2 (Optional): Create Account
                if (options.CreateAccount)
                {
                    var email = options.Email;
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        email = ConsoleHelper.ReadLine("Enter new account email");
                    }
                    var password = options.Password;
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        password = ConsoleHelper.ReadPassword("Enter new account password");
                    }

                    ConsoleHelper.Info($"Creating account for {email}...");
                    await client.CreateAccountAsync(email, password);
                    ConsoleHelper.Success("Account created successfully!");
                    options.Email = email;
                    options.Password = password;
                }

                // Step 3: Login
                AccountInfo account;
                while (true)
                {
                    var email = options.Email;
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        email = ConsoleHelper.ReadLine("Email / Username");
                    }

                    var password = options.Password;
                    var passwordHash = options.PasswordHash;

                    if (string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(passwordHash))
                    {
                        password = ConsoleHelper.ReadPassword("Password");
                    }

                    ConsoleHelper.Info($"Signing in as '{email}'...");
                    try
                    {
                        account = await client.SignInAsync(email, password ?? passwordHash!, alreadyHashed: !string.IsNullOrEmpty(passwordHash));
                        ConsoleHelper.Success($"Signed in successfully as {account.Email} (Account ID: {account.AccountId})");
                        ConsoleHelper.PrintAccount(account);
                        break;
                    }
                    catch (PerpetuumClientException pex)
                    {
                        ConsoleHelper.Error(pex.Message);
                        if (options.NonInteractive)
                        {
                            return 1;
                        }

                        // Reset credentials so user can re-enter
                        options.Email = null;
                        options.Password = null;
                        options.PasswordHash = null;
                    }
                }

                // Step 3.5 (Optional): Create Character via CLI option
                if (!string.IsNullOrWhiteSpace(options.CreateCharacterNick))
                {
                    ConsoleHelper.Info($"Creating character '{options.CreateCharacterNick}'...");
                    var newCharId = await client.CreateCharacterAsync(options.CreateCharacterNick, options.RaceId, options.SchoolId, options.MajorId, options.SparkId);
                    ConsoleHelper.Success($"Character '{options.CreateCharacterNick}' created successfully! (Character ID: {newCharId})");
                    options.Character = options.CreateCharacterNick;
                }

                // Step 4: Get Character List
                ConsoleHelper.Info("Retrieving characters...");
                var charListResult = await client.GetCharacterListAsync();
                ConsoleHelper.PrintCharactersTable(charListResult.Characters, charListResult.ExtensionPoints);

                // Step 5: Select Character
                CharacterSummary? targetChar = null;
                if (!string.IsNullOrWhiteSpace(options.Character))
                {
                    if (int.TryParse(options.Character, out var cid))
                    {
                        targetChar = charListResult.Characters.FirstOrDefault(c => c.CharacterId == cid);
                    }
                    if (targetChar == null)
                    {
                        targetChar = charListResult.Characters.FirstOrDefault(c => string.Equals(c.Nick, options.Character, StringComparison.OrdinalIgnoreCase));
                    }

                    if (targetChar == null)
                    {
                        ConsoleHelper.Warn($"Character '{options.Character}' not found on account.");
                    }
                }

                if (targetChar == null && charListResult.Characters.Count == 0 && !options.NonInteractive)
                {
                    var createChoice = ConsoleHelper.ReadLine("No characters found on this account. Create a new character now? (Y/n)", "Y");
                    if (createChoice.Equals("y", StringComparison.OrdinalIgnoreCase) || createChoice.Equals("yes", StringComparison.OrdinalIgnoreCase))
                    {
                        var newNick = ConsoleHelper.ReadLine("Enter character nickname");
                        if (!string.IsNullOrWhiteSpace(newNick))
                        {
                            ConsoleHelper.Info($"Creating character '{newNick}'...");
                            var newId = await client.CreateCharacterAsync(newNick);
                            ConsoleHelper.Success($"Character '{newNick}' created! (ID: {newId})");
                            charListResult = await client.GetCharacterListAsync();
                            ConsoleHelper.PrintCharactersTable(charListResult.Characters, charListResult.ExtensionPoints);
                            targetChar = charListResult.Characters.FirstOrDefault(c => c.CharacterId == newId);
                        }
                    }
                }

                if (targetChar == null && charListResult.Characters.Count > 0)
                {
                    if (options.NonInteractive)
                    {
                        // In non-interactive mode with no character specified, pick the first character
                        targetChar = charListResult.Characters[0];
                    }
                    else
                    {
                        while (targetChar == null)
                        {
                            var input = ConsoleHelper.ReadLine($"Select character (1-{charListResult.Characters.Count}, ID, Nick, 'new' to create, or 'q' to skip)", "1");
                            if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "quit", StringComparison.OrdinalIgnoreCase))
                            {
                                break;
                            }

                            if (string.Equals(input, "new", StringComparison.OrdinalIgnoreCase) || string.Equals(input, "create", StringComparison.OrdinalIgnoreCase))
                            {
                                var newNick = ConsoleHelper.ReadLine("Enter character nickname");
                                if (!string.IsNullOrWhiteSpace(newNick))
                                {
                                    ConsoleHelper.Info($"Creating character '{newNick}'...");
                                    var newId = await client.CreateCharacterAsync(newNick);
                                    ConsoleHelper.Success($"Character '{newNick}' created! (ID: {newId})");
                                    charListResult = await client.GetCharacterListAsync();
                                    ConsoleHelper.PrintCharactersTable(charListResult.Characters, charListResult.ExtensionPoints);
                                    targetChar = charListResult.Characters.FirstOrDefault(c => c.CharacterId == newId);
                                    if (targetChar != null) break;
                                }
                                continue;
                            }

                            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= charListResult.Characters.Count)
                            {
                                targetChar = charListResult.Characters[idx - 1];
                            }
                            else if (int.TryParse(input, out var charId))
                            {
                                targetChar = charListResult.Characters.FirstOrDefault(c => c.CharacterId == charId);
                            }
                            else
                            {
                                targetChar = charListResult.Characters.FirstOrDefault(c => string.Equals(c.Nick, input, StringComparison.OrdinalIgnoreCase));
                            }

                            if (targetChar == null)
                            {
                                ConsoleHelper.Warn("Invalid selection, please try again.");
                            }
                        }
                    }
                }

                if (targetChar != null)
                {
                    ConsoleHelper.Info($"Selecting character '{targetChar.Nick}' (ID: {targetChar.CharacterId})...");
                    var selectResult = await client.SelectCharacterAsync(targetChar.CharacterId, targetChar.BaseEid, targetChar.BaseName);
                    _selectedCharacterNick = targetChar.Nick;
                    _selectedCharacterBaseEid = targetChar.BaseEid;
                    _selectedCharacterBaseName = targetChar.BaseName;
                    _selectedCharacterIsDocked = targetChar.IsDocked;
                    ConsoleHelper.Success($"Character '{targetChar.Nick}' selected successfully!");
                    ConsoleHelper.PrintSelectedCharacter(selectResult, targetChar.Nick);

                    if (targetChar.IsDocked)
                    {
                        if (options.ListStorage)
                        {
                            ConsoleHelper.Info("Retrieving private station storage...");
                            try
                            {
                                var storage = await client.GetStorageAsync(targetChar.BaseEid);
                                ConsoleHelper.PrintStorageTable(storage, targetChar.Nick);
                            }
                            catch (Exception ex)
                            {
                                ConsoleHelper.Warn($"Could not retrieve station storage: {ex.Message}");
                            }
                        }
                        else
                        {
                            var baseLabel = !string.IsNullOrEmpty(targetChar.BaseName) ? targetChar.BaseName : $"Base #{targetChar.BaseEid}";
                            ConsoleHelper.Info($"Docked at: {baseLabel}. Type 'storage' to view private storage items.");
                        }
                    }
                }
                else
                {
                    ConsoleHelper.Info("No character selected.");
                }

                // Handle CLI channel / chat operations
                if (options.ListChannels)
                {
                    ConsoleHelper.Info("Retrieving chat channels...");
                    var channels = await client.GetChannelsAsync();
                    _lastChannelsList = channels;
                    ConsoleHelper.PrintChannelsTable(channels);
                }

                if (!string.IsNullOrWhiteSpace(options.Channel))
                {
                    _activeChannelName = options.Channel;
                    ConsoleHelper.Info($"Joining channel '{_activeChannelName}'...");
                    try
                    {
                        await client.JoinChannelAsync(_activeChannelName);
                        ConsoleHelper.Success($"Joined channel '{_activeChannelName}'.");
                    }
                    catch (PerpetuumClientException pex)
                    {
                        ConsoleHelper.Warn($"Join channel notice: {pex.Message}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(options.ChatMessage))
                {
                    var targetChan = !string.IsNullOrWhiteSpace(options.Channel) ? options.Channel : "General";
                    ConsoleHelper.Info($"Sending chat message to channel '{targetChan}': {options.ChatMessage}");
                    await client.SendChatMessageAsync(targetChan, options.ChatMessage);
                    ConsoleHelper.Success("Chat message sent successfully.");
                }

                if (options.NonInteractive)
                {
                    ConsoleHelper.Success("Operation completed in non-interactive mode.");
                    return 0;
                }

                // Step 6: Interactive REPL loop
                await RunInteractiveLoopAsync(client);
                return 0;
            }
            catch (Exception ex)
            {
                ConsoleHelper.Error($"Fatal error: {ex.Message}");
                return 1;
            }
            finally
            {
                try
                {
                    if (client.SelectedCharacter != null)
                    {
                        await client.DeselectCharacterAsync();
                    }
                    if (client.Account != null)
                    {
                        await client.SignOutAsync();
                    }
                }
                catch { }
                client.Disconnect();
            }
        }

        private static async Task RunInteractiveLoopAsync(PerpetuumClient client)
        {
            Console.WriteLine();
            ConsoleHelper.Info("Entered interactive shell. Type 'help' for commands, 'exit' to quit.");
            Console.WriteLine();

            while (!_exitRequested && client.IsConnected)
            {
                var promptLabel = BuildPromptLabel(client);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write(promptLabel);
                Console.ResetColor();

                var line = Console.ReadLine();
                if (line == null)
                {
                    break;
                }

                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var cmd = parts[0].ToLowerInvariant();

                try
                {
                    switch (cmd)
                    {
                        case "help":
                        case "?":
                            PrintShellHelp();
                            break;

                        case "whoami":
                        case "me":
                        case "info":
                            if (client.Account != null)
                            {
                                ConsoleHelper.PrintAccount(client.Account);
                            }
                            if (client.SelectedCharacter != null)
                            {
                                ConsoleHelper.PrintSelectedCharacter(client.SelectedCharacter, _selectedCharacterNick);
                            }
                            if (!string.IsNullOrEmpty(_activeChannelName))
                            {
                                ConsoleHelper.Info($"Active chat channel: #{_activeChannelName}");
                            }
                            break;

                        case "chars":
                        case "list":
                            ConsoleHelper.Info("Refreshing character list...");
                            var charList = await client.GetCharacterListAsync();
                            ConsoleHelper.PrintCharactersTable(charList.Characters, charList.ExtensionPoints);
                            break;

                        case "create-char":
                        case "createchar":
                        case "newchar":
                        case "char-create":
                            string charNick = parts.Length > 1 ? parts[1] : string.Empty;
                            if (string.IsNullOrWhiteSpace(charNick))
                            {
                                charNick = ConsoleHelper.ReadLine("Enter character nickname");
                            }
                            if (string.IsNullOrWhiteSpace(charNick))
                            {
                                ConsoleHelper.Warn("Character nickname cannot be empty.");
                                break;
                            }

                            int race = 1;
                            int school = 1;
                            int major = 1;
                            int spark = 1;

                            for (int i = 2; i < parts.Length; i++)
                            {
                                var eqIdx = parts[i].IndexOf('=');
                                if (eqIdx > 0)
                                {
                                    var key = parts[i].Substring(0, eqIdx).ToLowerInvariant();
                                    var val = parts[i].Substring(eqIdx + 1);
                                    if (key == "race" && int.TryParse(val, out var rv)) race = rv;
                                    else if (key == "school" && int.TryParse(val, out var sv)) school = sv;
                                    else if (key == "major" && int.TryParse(val, out var mv)) major = mv;
                                    else if (key == "spark" && int.TryParse(val, out var spv)) spark = spv;
                                }
                                else if (int.TryParse(parts[i], out var numVal))
                                {
                                    if (i == 2) race = numVal;
                                    else if (i == 3) school = numVal;
                                    else if (i == 4) major = numVal;
                                    else if (i == 5) spark = numVal;
                                }
                            }

                            ConsoleHelper.Info($"Creating character '{charNick}' (Race: {race}, School: {school})...");
                            var createdCharId = await client.CreateCharacterAsync(charNick, race, school, major, spark);
                            ConsoleHelper.Success($"Character '{charNick}' created successfully! (Character ID: {createdCharId})");

                            var refreshedChars = await client.GetCharacterListAsync();
                            ConsoleHelper.PrintCharactersTable(refreshedChars.Characters, refreshedChars.ExtensionPoints);

                            var selectNow = ConsoleHelper.ReadLine($"Select character '{charNick}' now? (Y/n)", "Y");
                            if (selectNow.Equals("y", StringComparison.OrdinalIgnoreCase) || selectNow.Equals("yes", StringComparison.OrdinalIgnoreCase))
                            {
                                if (client.SelectedCharacter != null)
                                {
                                    await client.DeselectCharacterAsync();
                                }
                                var createdChar = refreshedChars.Characters.FirstOrDefault(c => c.CharacterId == createdCharId);
                                var baseEid = createdChar?.BaseEid ?? 0;
                                var baseName = createdChar?.BaseName ?? string.Empty;
                                var isDocked = createdChar?.IsDocked ?? true;

                                var selectRes = await client.SelectCharacterAsync(createdCharId, baseEid, baseName);
                                _selectedCharacterNick = charNick;
                                _selectedCharacterBaseEid = baseEid;
                                _selectedCharacterBaseName = baseName;
                                _selectedCharacterIsDocked = isDocked;
                                ConsoleHelper.Success($"Character '{charNick}' selected.");
                                ConsoleHelper.PrintSelectedCharacter(selectRes, charNick);
                            }
                            break;

                        case "select":
                            if (parts.Length < 2)
                            {
                                ConsoleHelper.Warn("Usage: select <CharacterID | CharacterName | #>");
                                break;
                            }
                            var listForSelect = await client.GetCharacterListAsync();
                            var targetArg = parts[1];
                            CharacterSummary? target = null;
                            if (int.TryParse(targetArg, out var num) && num >= 1 && num <= listForSelect.Characters.Count)
                            {
                                target = listForSelect.Characters[num - 1];
                            }
                            else if (int.TryParse(targetArg, out var cid))
                            {
                                target = listForSelect.Characters.FirstOrDefault(c => c.CharacterId == cid);
                            }
                            else
                            {
                                target = listForSelect.Characters.FirstOrDefault(c => string.Equals(c.Nick, targetArg, StringComparison.OrdinalIgnoreCase));
                            }

                            if (target == null)
                            {
                                ConsoleHelper.Error($"Character '{targetArg}' not found.");
                                break;
                            }

                            ConsoleHelper.Info($"Selecting character '{target.Nick}' (ID: {target.CharacterId})...");
                            var selRes = await client.SelectCharacterAsync(target.CharacterId, target.BaseEid, target.BaseName);
                            _selectedCharacterNick = target.Nick;
                            _selectedCharacterBaseEid = target.BaseEid;
                            _selectedCharacterBaseName = target.BaseName;
                            _selectedCharacterIsDocked = target.IsDocked;
                            ConsoleHelper.Success($"Character '{target.Nick}' selected.");
                            ConsoleHelper.PrintSelectedCharacter(selRes, target.Nick);
                            if (target.IsDocked)
                            {
                                var baseLabel = !string.IsNullOrEmpty(target.BaseName) ? target.BaseName : $"Base #{target.BaseEid}";
                                ConsoleHelper.Info($"Docked at: {baseLabel}. Type 'storage' to view private storage items.");
                            }
                            break;

                        case "deselect":
                            if (client.SelectedCharacter == null)
                            {
                                ConsoleHelper.Warn("No character currently selected.");
                                break;
                            }
                            ConsoleHelper.Info("Deselecting character...");
                            await client.DeselectCharacterAsync();
                            _selectedCharacterNick = string.Empty;
                            _selectedCharacterBaseEid = 0;
                            _selectedCharacterBaseName = string.Empty;
                            _selectedCharacterIsDocked = false;
                            _activeChannelName = string.Empty;
                            ConsoleHelper.Success("Character deselected.");
                            break;

                        case "channels":
                        case "chans":
                            bool myOnly = parts.Length > 1 && string.Equals(parts[1], "my", StringComparison.OrdinalIgnoreCase);
                            ConsoleHelper.Info(myOnly ? "Retrieving joined channels..." : "Retrieving public channels...");
                            var channels = await client.GetChannelsAsync(myChannelsOnly: myOnly);
                            _lastChannelsList = channels;
                            ConsoleHelper.PrintChannelsTable(channels, myOnly ? "MY CHANNELS" : "AVAILABLE CHANNELS");
                            break;

                        case "channel":
                        case "chan":
                        case "select-channel":
                            if (parts.Length < 2)
                            {
                                if (!string.IsNullOrEmpty(_activeChannelName))
                                {
                                    ConsoleHelper.Info($"Active channel: #{_activeChannelName}");
                                }
                                else
                                {
                                    ConsoleHelper.Warn("Usage: channel <name | # | list>");
                                }
                                break;
                            }

                            var chanArg = parts[1];
                            string targetChanName = chanArg;

                            if (int.TryParse(chanArg, out var chanIdx) && chanIdx >= 1 && chanIdx <= _lastChannelsList.Count)
                            {
                                targetChanName = _lastChannelsList[chanIdx - 1].Name;
                            }

                            if (targetChanName.StartsWith("#"))
                            {
                                targetChanName = targetChanName.Substring(1);
                            }

                            ConsoleHelper.Info($"Selecting channel '{targetChanName}'...");
                            try
                            {
                                await client.JoinChannelAsync(targetChanName);
                            }
                            catch (PerpetuumClientException pex) when (pex.ErrorCode == ErrorCodes.CharacterAlreadyOnChannel)
                            {
                                // Already in channel, proceed
                            }

                            _activeChannelName = targetChanName;
                            ConsoleHelper.Success($"Active channel set to #{_activeChannelName}.");
                            break;

                        case "join":
                            if (parts.Length < 2)
                            {
                                ConsoleHelper.Warn("Usage: join <channelName> [password]");
                                break;
                            }
                            var joinName = parts[1].TrimStart('#');
                            var password = parts.Length > 2 ? parts[2] : string.Empty;

                            ConsoleHelper.Info($"Joining channel '{joinName}'...");
                            await client.JoinChannelAsync(joinName, password);
                            _activeChannelName = joinName;
                            ConsoleHelper.Success($"Joined and selected channel #{_activeChannelName}.");
                            break;

                        case "leave":
                            var leaveName = parts.Length > 1 ? parts[1].TrimStart('#') : _activeChannelName;
                            if (string.IsNullOrEmpty(leaveName))
                            {
                                ConsoleHelper.Warn("Usage: leave <channelName>");
                                break;
                            }
                            ConsoleHelper.Info($"Leaving channel '{leaveName}'...");
                            await client.LeaveChannelAsync(leaveName);
                            if (string.Equals(_activeChannelName, leaveName, StringComparison.OrdinalIgnoreCase))
                            {
                                _activeChannelName = string.Empty;
                            }
                            ConsoleHelper.Success($"Left channel '{leaveName}'.");
                            break;

                        case "msg":
                        case "chat":
                        case "say":
                            if (parts.Length < 2)
                            {
                                ConsoleHelper.Warn("Usage: msg <text>  OR  msg #<channel> <text>");
                                break;
                            }

                            string msgChannel = _activeChannelName;
                            string messageText;

                            if (parts[1].StartsWith("#") && parts.Length > 2)
                            {
                                msgChannel = parts[1].Substring(1);
                                messageText = trimmed.Substring(parts[0].Length + parts[1].Length + 2).Trim();
                            }
                            else
                            {
                                messageText = trimmed.Substring(parts[0].Length + 1).Trim();
                            }

                            if (string.IsNullOrEmpty(msgChannel))
                            {
                                ConsoleHelper.Warn("No active channel selected. Use 'channel <name>' or 'msg #<channel> <text>'.");
                                break;
                            }

                            if (client.SelectedCharacter == null)
                            {
                                ConsoleHelper.Warn("Select a character first before chatting.");
                                break;
                            }

                            await client.SendChatMessageAsync(msgChannel, messageText);
                            break;

                        case "profile":
                            if (client.SelectedCharacter == null)
                            {
                                ConsoleHelper.Warn("Select a character first to view profile.");
                                break;
                            }
                            ConsoleHelper.Info("Requesting character profile...");
                            var profileCmd = Commands.GetCommandByText("characterGetMyProfile") ?? new Command("characterGetMyProfile");
                            var profRes = await client.SendAsync(new Message(profileCmd, new Dictionary<string, object>()));
                            PerpetuumClient.CheckResponseError(profRes);
                            ConsoleHelper.Success("Profile data received:");
                            PrintDictionary(profRes.Data, 1);
                            break;

                        case "storage":
                        case "hangar":
                        case "inventory":
                        case "items":
                        case "list-storage":
                            if (client.SelectedCharacter == null)
                            {
                                ConsoleHelper.Warn("Select a character first to view station storage.");
                                break;
                            }

                            long targetBaseEid = _selectedCharacterBaseEid;
                            if (parts.Length > 1 && long.TryParse(parts[1], out var customBaseEid))
                            {
                                targetBaseEid = customBaseEid;
                            }

                            if (targetBaseEid == 0 && !_selectedCharacterIsDocked)
                            {
                                ConsoleHelper.Warn("Character is not currently docked at a station. Usage: storage [baseEid]");
                                break;
                            }

                            var baseNameStr = !string.IsNullOrEmpty(_selectedCharacterBaseName) && targetBaseEid == _selectedCharacterBaseEid
                                ? $"{_selectedCharacterBaseName} (#{targetBaseEid})"
                                : $"Base #{targetBaseEid}";
                            ConsoleHelper.Info($"Retrieving station storage ({baseNameStr})...");
                            var storageRes = await client.GetStorageAsync(targetBaseEid > 0 ? targetBaseEid : (long?)null);
                            ConsoleHelper.PrintStorageTable(storageRes, _selectedCharacterNick);
                            break;

                        case "send":
                        case "cmd":
                            if (parts.Length < 2)
                            {
                                ConsoleHelper.Warn("Usage: send <commandName> [key1=val1 key2=val2 ...]");
                                break;
                            }
                            var commandName = parts[1];
                            var paramDict = new Dictionary<string, object>();
                            for (int i = 2; i < parts.Length; i++)
                            {
                                var eqIdx = parts[i].IndexOf('=');
                                if (eqIdx > 0)
                                {
                                    var k = parts[i].Substring(0, eqIdx);
                                    var v = parts[i].Substring(eqIdx + 1);
                                    if (int.TryParse(v, out var iv))
                                        paramDict[k] = iv;
                                    else if (long.TryParse(v, out var lv))
                                        paramDict[k] = lv;
                                    else if (bool.TryParse(v, out var bv))
                                        paramDict[k] = bv;
                                    else
                                        paramDict[k] = v;
                                }
                            }

                            ConsoleHelper.Info($"Sending command '{commandName}'...");
                            var customRes = await client.SendCommandAsync(commandName, paramDict);
                            ConsoleHelper.Success($"Received response for '{customRes.Command?.Text}':");
                            PrintDictionary(customRes.Data, 1);
                            break;

                        case "status":
                        case "ping":
                            ConsoleHelper.Info($"Connected to: {client.RemoteEndPoint}");
                            ConsoleHelper.Info($"Session authenticated: {client.Account != null} (Account ID: {client.Account?.AccountId})");
                            ConsoleHelper.Info($"Selected character: {(!string.IsNullOrEmpty(_selectedCharacterNick) ? _selectedCharacterNick : "None")}");
                            ConsoleHelper.Info($"Active channel: {(!string.IsNullOrEmpty(_activeChannelName) ? $"#{_activeChannelName}" : "None")}");
                            break;

                        case "cls":
                        case "clear":
                            Console.Clear();
                            ConsoleHelper.WriteBanner();
                            break;

                        case "logout":
                        case "signout":
                            ConsoleHelper.Info("Signing out...");
                            await client.SignOutAsync();
                            _selectedCharacterNick = string.Empty;
                            _activeChannelName = string.Empty;
                            ConsoleHelper.Success("Signed out.");
                            return;

                        case "exit":
                        case "quit":
                        case "q":
                            ConsoleHelper.Info("Exiting...");
                            return;

                        default:
                            ConsoleHelper.Warn($"Unknown command '{cmd}'. Type 'help' for available commands.");
                            break;
                    }
                }
                catch (PerpetuumClientException pex)
                {
                    ConsoleHelper.Error(pex.Message);
                }
                catch (Exception ex)
                {
                    ConsoleHelper.Error($"Command error: {ex.Message}");
                }
            }
        }

        private static string BuildPromptLabel(PerpetuumClient client)
        {
            if (!string.IsNullOrEmpty(_selectedCharacterNick))
            {
                if (!string.IsNullOrEmpty(_activeChannelName))
                {
                    return $"[Perpetuum:{_selectedCharacterNick} (#{_activeChannelName})]> ";
                }
                return $"[Perpetuum:{_selectedCharacterNick}]> ";
            }

            if (client.Account != null)
            {
                return $"[Perpetuum:{client.Account.Email}]> ";
            }

            return "[Perpetuum]> ";
        }

        private static void PrintShellHelp()
        {
            Console.WriteLine(@"
Interactive Commands:
  help, ?                         Show this help message
  whoami, me, info                Show account, character, and channel details
  chars, list                     List all characters on the account
  create-char <nick> [race=1]     Create a new character (aliases: newchar, createchar)
  select <#|id|name>              Select a character by table number, ID, or nickname
  deselect                        Deselect current character
  storage [baseEid]               List private station storage (aliases: hangar, inventory, items)

Chat & Channels:
  channels [public|my]            List available or joined chat channels
  channel <name|#>                Select/set the active chat channel
  join <name> [password]          Join a channel and set as active
  leave [name]                    Leave a channel
  msg <text>                      Send a message to the active channel
  msg #<channel> <text>           Send a message to a specific channel

General & Raw Commands:
  profile                         Request and display character profile
  send <cmd> [k=v ...]            Send a raw command to the server with optional key=value arguments
  status, ping                    Show connection and session status
  cls, clear                      Clear console screen
  logout, signout                 Sign out of account
  exit, quit, q                   Disconnect and exit
");
        }

        private static void PrintDictionary(IDictionary<string, object>? dict, int indent)
        {
            if (dict == null) return;
            var prefix = new string(' ', indent * 2);
            foreach (var kvp in dict)
            {
                if (kvp.Value is IDictionary<string, object> nested)
                {
                    Console.WriteLine($"{prefix}{kvp.Key}:");
                    PrintDictionary(nested, indent + 1);
                }
                else
                {
                    Console.WriteLine($"{prefix}{kvp.Key}: {kvp.Value}");
                }
            }
        }
    }
}
