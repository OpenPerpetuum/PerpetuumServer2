using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Sessions;
using Perpetuum.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Perpetuum.Services.Channels.ChatCommands
{
    public class AdminCommandRouter
    {
        private delegate void CommandDelegate(AdminCommandData data);

        private readonly GlobalConfiguration _config;
        private readonly ISessionManager _sessionManager;
        private readonly IDictionary<string, CommandDelegate> _commands;
        public AdminCommandRouter(GlobalConfiguration configuration, ISessionManager sessionManager)
        {
            _config = configuration;
            _sessionManager = sessionManager;

            _commands = typeof(AdminCommandHandlers).GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(ChatCommand), false).Length > 0)
                .Select(m => new KeyValuePair<string, CommandDelegate>(
                    ((ChatCommand)m.GetCustomAttribute(typeof(ChatCommand))).Command,
                    (CommandDelegate)Delegate.CreateDelegate(typeof(CommandDelegate), m)))
                .ToDictionary();
        }

        public void TryParseAdminCommand(Character sender, string text, IRequest request, Channel channel, IChannelManager channelManager)
        {
            var isAdmin = IsAdmin(sender);
            if (isAdmin)
            {
                channel.SendMessageToAll(_sessionManager, sender, text); //in the future, it will be displayed only in the secure channel
                WriteLogToDb(sender, text); //enhancement todo: put the log in the next part to be able to log all the admin details
                ParseAdminCommand(sender, text, request, channel, channelManager);
            }
            else //not an admin, id and text logged for abuse check
            {
                WriteLogToDb(sender, text);
            }
        }

        public bool IsAdminCommand(string text)
        {
            return text.StartsWith("#");
        }
        private bool IsAdmin(Character sender)
        {
            return sender.AccessLevel == AccessLevel.admin;
        }

        private void WriteLogToDb(Character sender, string text)
        {
            var str_trunc = text;

            if (text.Length > 255)
            {
                str_trunc = text.Substring(1, 255); //text truncated to avoid big text spam insertion in db
            }

            Db.Query().CommandText("insert into adminCommandLog (characterid, acclevel, message) values (@characterid, @acclevel, @text)")
                .SetParameter("@characterid", sender.Id)
                .SetParameter("@acclevel", (int) sender.AccessLevel)
                .SetParameter("@text", str_trunc)
                .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);

        }

        private void ParseAdminCommand(Character sender, string text, IRequest request, Channel channel, IChannelManager channelManager)
        {

            string[] command = text.Split(new char[] { ',' });

            var data = AdminCommandData.Create(sender, command, request, channel, channelManager, _sessionManager, _config.EnableDev);

            // Commands can only be issued in secure channel
            if (channel.Type == ChannelType.Admin)
            {
                TryInvokeCommand(data);
                return;
            }

            // Unless it is the command to secure the channel
            if (data.Command.Name == "secure")
            {
                //channel is secure, the admin command can be displayed
                channel.SendMessageToAll(_sessionManager, sender, text);

                AdminCommandHandlers.Secure(data);
                return;
            }
            channel.SendMessageToAll(_sessionManager, sender, "Channel must be secured before sending commands.");
        }

        private void TryInvokeCommand(AdminCommandData data)
        {
            if (_commands.TryGetValue(data.Command.Name, out CommandDelegate commandMethod))
            {
                commandMethod(data);
            }
            else
            {
                AdminCommandHandlers.Unknown(data);
            }
        }
    }
}
