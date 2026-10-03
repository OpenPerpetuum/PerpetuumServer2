using System;
using System.Collections.Generic;
using Perpetuum.CliClient.Models;

namespace Perpetuum.CliClient
{
    public static class ConsoleHelper
    {
        public static void WriteBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
  ___  ____  _____ _   _ ____  _____ ____ _____ _   _ _   _ __  __ 
 / _ \|  _ \| ____| \ | |  _ \| ____|  _ \_   _| | | | | | |  \/  |
| | | | |_) |  _| |  \| | |_) |  _| | |_) || | | | | | | | | |\/| |
| |_| |  __/| |___| |\  |  __/| |___|  _ < | | | |_| | |_| | |  | |
 \___/|_|   |_____|_| \_|_|   |_____|_| \_\|_|  \___/ \___/|_|  |_|
                 SERVER CLI CLIENT (OpenPerpetuum)
");
            Console.ResetColor();
        }

        public static void Info(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("[INFO] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        public static void Success(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[SUCCESS] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        public static void Warn(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[WARN] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        public static void Error(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[ERROR] ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        public static string ReadLine(string prompt, string? defaultValue = null)
        {
            Console.ForegroundColor = ConsoleColor.White;
            if (!string.IsNullOrEmpty(defaultValue))
            {
                Console.Write($"{prompt} [{defaultValue}]: ");
            }
            else
            {
                Console.Write($"{prompt}: ");
            }
            Console.ResetColor();

            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input) && defaultValue != null)
            {
                return defaultValue;
            }
            return input?.Trim() ?? string.Empty;
        }

        public static string ReadPassword(string prompt)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{prompt}: ");
            Console.ResetColor();

            var pass = new System.Text.StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (pass.Length > 0)
                    {
                        pass.Remove(pass.Length - 1, 1);
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    pass.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            return pass.ToString();
        }

        public static void PrintWelcome(WelcomeInfo welcome, string endpoint)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("------------------- SERVER INFO -------------------");
            Console.ResetColor();
            Console.WriteLine($"  Server:      {welcome.WorldName} ({endpoint})");
            Console.WriteLine($"  Version:     {welcome.Version}");
            Console.WriteLine($"  Server Time: {welcome.OSTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");
            if (!string.IsNullOrEmpty(welcome.ResourceServerUrl))
                Console.WriteLine($"  Assets URL:  {welcome.ResourceServerUrl}");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("---------------------------------------------------");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void PrintAccount(AccountInfo account)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("------------------ ACCOUNT INFO ------------------");
            Console.ResetColor();
            Console.WriteLine($"  Account ID:   {account.AccountId}");
            Console.WriteLine($"  Email:        {account.Email}");
            Console.WriteLine($"  Access Level: {account.AccessLevel}");
            Console.WriteLine($"  Credits:      {account.Credit:N0} NIC");
            Console.WriteLine($"  Subscriber:   {(account.IsSubscriber ? "Yes" : "No")}");
            if (account.ValidUntil.HasValue)
                Console.WriteLine($"  Valid Until:  {account.ValidUntil.Value:yyyy-MM-dd HH:mm:ss}");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("---------------------------------------------------");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void PrintCharactersTable(List<CharacterSummary> characters, int extensionPoints)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"---------------- AVAILABLE CHARACTERS (EP: {extensionPoints:N0}) ----------------");
            Console.ResetColor();

            if (characters.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  No characters found on this account.");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("-------------------------------------------------------------------");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  # | ID      | Nickname        | Location / Base           | Status | Credits (NIC)");
            Console.WriteLine("----+---------+-----------------+---------------------------+--------+--------------");
            Console.ResetColor();

            for (int i = 0; i < characters.Count; i++)
            {
                var c = characters[i];
                var location = c.IsDocked
                    ? (!string.IsNullOrEmpty(c.BaseName) ? c.BaseName : $"Base #{c.BaseEid}")
                    : (c.ZoneId.HasValue ? $"Zone #{c.ZoneId}" : "In Space");

                var status = c.IsDocked ? "Docked" : "In Zone";
                if (c.InUse) status += " (Online)";

                Console.WriteLine($" {i + 1,2} | {c.CharacterId,7} | {c.Nick,-15} | {location,-25} | {status,-6} | {c.Credit,13:N0}");
            }

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("-------------------------------------------------------------------");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void PrintSelectedCharacter(CharacterSelectResult result, string nick = "")
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("================ CHARACTER SELECTED ================");
            Console.ResetColor();
            Console.WriteLine($"  Nickname:        {(string.IsNullOrEmpty(nick) ? "N/A" : nick)}");
            Console.WriteLine($"  Character ID:    {result.CharacterId}");
            Console.WriteLine($"  Root EID:        {result.RootEid}");
            Console.WriteLine($"  Corporation EID: {result.CorporationEid}");
            if (result.AllianceEid > 0)
                Console.WriteLine($"  Alliance EID:    {result.AllianceEid}");
            Console.WriteLine($"  Status:          {(result.IsDocked ? "Docked at Base" : "Active in Zone")}");
            if (result.ZoneData != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("  Zone Details:");
                foreach (var kvp in result.ZoneData)
                {
                    Console.WriteLine($"    - {kvp.Key}");
                }
                Console.ResetColor();
            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("====================================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void PrintChannelsTable(List<ChannelInfo> channels, string title = "AVAILABLE CHANNELS")
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"---------------- {title} ({channels.Count}) ----------------");
            Console.ResetColor();

            if (channels.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  No channels found.");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("-------------------------------------------------------------------");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  # | Channel Name             | Type         | Users | Topic");
            Console.WriteLine("----+--------------------------+--------------+-------+-----------------------------");
            Console.ResetColor();

            for (int i = 0; i < channels.Count; i++)
            {
                var c = channels[i];
                var pw = c.HasPassword ? " [PW]" : "";
                var topic = string.IsNullOrEmpty(c.Topic) ? "-" : (c.Topic.Length > 30 ? c.Topic[..27] + "..." : c.Topic);
                Console.WriteLine($" {i + 1,2} | {c.Name + pw,-24} | {c.TypeName,-12} | {c.MemberCount,5} | {topic}");
            }

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("-------------------------------------------------------------------");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void PrintChatMessage(ChatMessage chat)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{chat.Timestamp:HH:mm:ss}] ");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"[#{chat.ChannelName}] ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"<{chat.SenderId}>: ");
            Console.ResetColor();
            Console.WriteLine(chat.Message);
        }

        public static void PrintChannelHistoryBox(string channelName, string? topic, int memberCount, IReadOnlyList<ChatMessage> recentMessages, int visibleLines = 5)
        {
            var width = 78;
            try
            {
                if (Console.WindowWidth > 20)
                {
                    width = Math.Clamp(Console.WindowWidth - 2, 50, 95);
                }
            }
            catch { }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            var headerTitle = $" # {channelName} ";
            var topicInfo = !string.IsNullOrEmpty(topic) ? $" - Topic: {topic}" : "";
            var headerRight = memberCount > 0 ? $" [{memberCount} online] " : " ";
            var headerText = $"───{headerTitle}{topicInfo}";
            if (headerText.Length + headerRight.Length >= width)
            {
                headerText = headerText.Substring(0, Math.Max(10, width - headerRight.Length - 4)) + "...";
            }
            var remainingDashes = Math.Max(0, width - headerText.Length - headerRight.Length - 2);

            Console.WriteLine($"┌{headerText}{new string('─', remainingDashes)}{headerRight}┐");
            Console.ResetColor();

            if (recentMessages.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                var emptyMsg = $"│  (no recent messages in #{channelName})";
                var padEmpty = Math.Max(0, width - emptyMsg.Length - 1);
                Console.WriteLine($"{emptyMsg}{new string(' ', padEmpty)}│");
                Console.ResetColor();
            }
            else
            {
                var displayList = recentMessages.TakeLast(visibleLines).ToList();
                foreach (var msg in displayList)
                {
                    var timeStr = $"[{msg.Timestamp:HH:mm:ss}]";
                    var senderStr = $"<{msg.SenderId}>:";
                    var text = msg.Message ?? string.Empty;

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write("│ ");
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.Write($"{timeStr} ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"{senderStr} ");
                    Console.ResetColor();

                    var prefixLen = 2 + timeStr.Length + 1 + senderStr.Length + 1;
                    var availableTextLen = width - prefixLen - 2;
                    if (availableTextLen > 0 && text.Length > availableTextLen)
                    {
                        text = text.Substring(0, availableTextLen - 3) + "...";
                    }

                    Console.Write(text);
                    var pad = Math.Max(0, width - prefixLen - text.Length - 1);
                    Console.WriteLine(new string(' ', pad) + "│");
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"└{new string('─', width - 2)}┘");
            Console.ResetColor();
        }

        public static void PrintStorageTable(StorageResult storage, string characterNick = "")
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("------------------- STATION PRIVATE STORAGE -------------------");
            Console.ResetColor();

            if (!string.IsNullOrWhiteSpace(characterNick))
            {
                Console.WriteLine($"  Character:   {characterNick}");
            }

            var baseLabel = !string.IsNullOrWhiteSpace(storage.BaseName)
                ? $"{storage.BaseName} (#{storage.BaseEid})"
                : (storage.BaseEid > 0 ? $"Base #{storage.BaseEid}" : "N/A");
            Console.WriteLine($"  Location:    {baseLabel}");
            Console.WriteLine($"  Container:   #{storage.ContainerEid} (Items: {storage.TotalItemCount:N0}, Total Volume: {storage.TotalVolume:N2} m³)");

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("---------------------------------------------------------------");
            Console.ResetColor();

            if (storage.Items.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  No items found in private storage at this station.");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("---------------------------------------------------------------");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  # | Item Name / Type              | Qty     | Pack | Health | Volume (m³) | Item EID");
            Console.WriteLine("----+-------------------------------+---------+------+--------+-------------+----------------");
            Console.ResetColor();

            for (int i = 0; i < storage.Items.Count; i++)
            {
                var item = storage.Items[i];
                PrintStorageItemRow(i + 1, item, indent: 0);
            }

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("---------------------------------------------------------------");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintStorageItemRow(int index, StorageItemInfo item, int indent)
        {
            var prefix = indent > 0 ? new string(' ', indent * 2) + "└── " : "";
            var name = prefix + item.DisplayName;
            if (name.Length > 29)
            {
                name = name.Substring(0, 26) + "...";
            }

            var idxStr = indent == 0 ? $"{index,2}" : "  ";
            var packStr = item.IsRepackaged ? "Yes" : "No";
            var healthStr = $"{item.Health:F0}%";
            var volStr = $"{item.Volume:F2}";

            Console.WriteLine($" {idxStr} | {name,-29} | {item.Quantity,7:N0} | {packStr,-4} | {healthStr,6} | {volStr,11} | {item.Eid}");

            if (item.Items.Count > 0)
            {
                for (int j = 0; j < item.Items.Count; j++)
                {
                    PrintStorageItemRow(j + 1, item.Items[j], indent + 1);
                }
            }
        }
    }
}
