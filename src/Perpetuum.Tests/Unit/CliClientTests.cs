using System;
using System.Collections.Generic;
using System.Text;
using Perpetuum.CliClient;
using Perpetuum.CliClient.Models;
using Perpetuum.GenXY;
using Perpetuum.Network;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class CliClientTests
    {
        [Fact]
        public void Password_hashing_produces_uppercase_sha1()
        {
            var plain = "secretPassword123";
            var hash = PerpetuumClient.HashPassword(plain);

            Assert.Equal(40, hash.Length);
            Assert.Equal(hash.ToUpperInvariant(), hash);
            Assert.Equal(hash, PerpetuumClient.HashPassword(plain));
        }

        [Fact]
        public void Password_hashing_preserves_existing_40_hex_hash()
        {
            var existingHash = "5BAA61E4C9B93F3F0682250B6CF8331B7EE68FD8";
            var result = PerpetuumClient.HashPassword(existingHash);

            Assert.Equal(existingHash, result);
        }

        [Fact]
        public void CommandLineOptions_parses_arguments_correctly()
        {
            var args = new[]
            {
                "--host", "192.168.1.50",
                "--port", "17800",
                "--email", "pilot@example.com",
                "--password", "myPass",
                "--character", "ThunderPilot",
                "--channel", "General",
                "--chat-msg", "Hello pilots!",
                "--list-channels",
                "--non-interactive"
            };

            var options = CommandLineOptions.Parse(args);

            Assert.Equal("192.168.1.50", options.Host);
            Assert.Equal(17800, options.Port);
            Assert.Equal("pilot@example.com", options.Email);
            Assert.Equal("myPass", options.Password);
            Assert.Equal("ThunderPilot", options.Character);
            Assert.Equal("General", options.Channel);
            Assert.Equal("Hello pilots!", options.ChatMessage);
            Assert.True(options.ListChannels);
            Assert.True(options.NonInteractive);
            Assert.False(options.ShowHelp);
        }

        [Fact]
        public void CommandLineOptions_parses_create_character_arguments_correctly()
        {
            var args = new[]
            {
                "--email", "pilot@example.com",
                "--password", "myPass",
                "--create-char", "NewPilotName",
                "--race", "2",
                "--school", "3",
                "--major", "4",
                "--spark", "5"
            };

            var options = CommandLineOptions.Parse(args);

            Assert.Equal("pilot@example.com", options.Email);
            Assert.Equal("myPass", options.Password);
            Assert.Equal("NewPilotName", options.CreateCharacterNick);
            Assert.Equal(2, options.RaceId);
            Assert.Equal(3, options.SchoolId);
            Assert.Equal(4, options.MajorId);
            Assert.Equal(5, options.SparkId);
        }

        [Fact]
        public void ClientDictionaryExtensions_GetBool_handles_types_and_defaults()
        {
            var dict = new Dictionary<string, object>
            {
                { "boolTrue", true },
                { "boolFalse", false },
                { "intOne", 1 },
                { "intZero", 0 },
                { "longOne", 1L },
                { "longZero", 0L },
                { "byteOne", (byte)1 },
                { "strTrue", "True" },
                { "strFalse", "false" },
                { "strOne", "1" },
                { "strZero", "0" },
                { "nullVal", null! }
            };

            Assert.True(dict.GetBool("boolTrue"));
            Assert.False(dict.GetBool("boolFalse"));
            Assert.True(dict.GetBool("intOne"));
            Assert.False(dict.GetBool("intZero"));
            Assert.True(dict.GetBool("longOne"));
            Assert.False(dict.GetBool("longZero"));
            Assert.True(dict.GetBool("byteOne"));
            Assert.True(dict.GetBool("strTrue"));
            Assert.False(dict.GetBool("strFalse"));
            Assert.True(dict.GetBool("strOne"));
            Assert.False(dict.GetBool("strZero"));
            Assert.False(dict.GetBool("nullVal"));
            Assert.False(dict.GetBool("missingKey"));
            Assert.True(dict.GetBool("missingKey", defaultValue: true));
        }

        [Fact]
        public void ClientDictionaryExtensions_GetInt_and_GetLong_handle_conversions()
        {
            var dict = new Dictionary<string, object>
            {
                { "intVal", 42 },
                { "longVal", 9876543210L },
                { "doubleVal", 123.45 },
                { "strInt", "500" },
                { "byteVal", (byte)7 }
            };

            Assert.Equal(42, dict.GetInt("intVal"));
            Assert.Equal(500, dict.GetInt("strInt"));
            Assert.Equal(7, dict.GetInt("byteVal"));
            Assert.Equal(123, dict.GetInt("doubleVal"));
            Assert.Equal(0, dict.GetInt("missingKey"));
            Assert.Equal(99, dict.GetInt("missingKey", 99));

            Assert.Equal(9876543210L, dict.GetLong("longVal"));
            Assert.Equal(42L, dict.GetLong("intVal"));
            Assert.Equal(500L, dict.GetLong("strInt"));
            Assert.Equal(0L, dict.GetLong("missingKey"));
            Assert.Equal(100L, dict.GetLong("missingKey", 100L));
        }

        [Fact]
        public void WelcomeInfo_parses_welcome_payload_with_genxy_types()
        {
            // GenXY serializes booleans as ints
            var data = new Dictionary<string, object>
            {
                { k.worldName, "Perpetuum Test World" },
                { "version", "p36.5-custom" },
                { k.OSTime, DateTime.UtcNow },
                { "steamLoginEnabled", 1 }, // int from Genxy
                { "resourceServerURL", "http://assets.local:1337" },
                { "dev", 1 } // int from Genxy
            };

            var welcome = WelcomeInfo.FromDictionary(data);

            Assert.Equal("Perpetuum Test World", welcome.WorldName);
            Assert.Equal("p36.5-custom", welcome.Version);
            Assert.NotNull(welcome.OSTime);
            Assert.True(welcome.SteamLoginEnabled);
            Assert.Equal("http://assets.local:1337", welcome.ResourceServerUrl);
            Assert.True(welcome.IsDev);
        }

        [Fact]
        public void AccountInfo_parses_sign_in_payload_with_genxy_types()
        {
            var validUntil = DateTime.UtcNow.AddDays(30);
            var data = new Dictionary<string, object>
            {
                { k.accountID, 42 },
                { k.email, "commander@syndicate.org" },
                { k.accLevel, (int)AccessLevel.gameAdmin },
                { k.credit, 5000000L }, // could be long from Genxy
                { k.isSubscriber, 1 },  // int from Genxy
                { k.emailConfirmed, 1 }, // int from Genxy
                { k.validUntil, validUntil }
            };

            var account = AccountInfo.FromDictionary(data);

            Assert.Equal(42, account.AccountId);
            Assert.Equal("commander@syndicate.org", account.Email);
            Assert.Equal(AccessLevel.gameAdmin, account.AccessLevel);
            Assert.Equal(5000000, account.Credit);
            Assert.True(account.IsSubscriber);
            Assert.True(account.EmailConfirmed);
            Assert.Equal(validUntil, account.ValidUntil);
        }

        [Fact]
        public void CharacterListResult_parses_nested_characters_dictionary()
        {
            var char0 = new Dictionary<string, object>
            {
                { k.characterID, 101 },
                { k.rootEID, 1000101L },
                { k.nick, "AlphaPilot" },
                { k.docked, 1 }, // int 1 from Genxy
                { k.baseEID, 5001L },
                { k.baseName, "The Syndicate Outpost" },
                { k.credit, 250000L }
            };

            var char1 = new Dictionary<string, object>
            {
                { k.characterID, 102 },
                { k.rootEID, 1000102L },
                { k.nick, "BetaScout" },
                { k.docked, 0 }, // int 0 from Genxy
                { k.zoneID, 3 },
                { k.credit, 120000L }
            };

            var charactersDict = new Dictionary<string, object>
            {
                { "c0", char0 },
                { "c1", char1 }
            };

            var innerResult = new Dictionary<string, object>
            {
                { "characters", charactersDict },
                { "extensionPoints", 75000 }
            };

            var rootData = new Dictionary<string, object>
            {
                { k.result, innerResult }
            };

            var listResult = CharacterListResult.FromDictionary(rootData);

            Assert.Equal(75000, listResult.ExtensionPoints);
            Assert.Equal(2, listResult.Characters.Count);

            var first = listResult.Characters[0];
            Assert.Equal(101, first.CharacterId);
            Assert.Equal("AlphaPilot", first.Nick);
            Assert.True(first.IsDocked);
            Assert.Equal("The Syndicate Outpost", first.BaseName);

            var second = listResult.Characters[1];
            Assert.Equal(102, second.CharacterId);
            Assert.Equal("BetaScout", second.Nick);
            Assert.False(second.IsDocked);
            Assert.Equal(3, second.ZoneId);
        }

        [Fact]
        public void CharacterSelectResult_parses_docked_and_zone_results()
        {
            var dockedData = new Dictionary<string, object>
            {
                { k.characterID, 101 },
                { k.rootEID, 1000101L },
                { k.corporationEID, 20001L },
                { k.allianceEID, 30001L }
            };

            var dockedRes = CharacterSelectResult.FromDictionary(dockedData);
            Assert.Equal(101, dockedRes.CharacterId);
            Assert.Equal(1000101L, dockedRes.RootEid);
            Assert.Equal(20001L, dockedRes.CorporationEid);
            Assert.Equal(30001L, dockedRes.AllianceEid);
            Assert.True(dockedRes.IsDocked);
            Assert.Null(dockedRes.ZoneData);

            var zoneData = new Dictionary<string, object>
            {
                { k.characterID, 102 },
                { k.rootEID, 1000102L },
                { k.corporationEID, 20002L },
                { k.zone, new Dictionary<string, object> { { "plugin", "zone_1" } } }
            };

            var zoneRes = CharacterSelectResult.FromDictionary(zoneData);
            Assert.Equal(102, zoneRes.CharacterId);
            Assert.False(zoneRes.IsDocked);
            Assert.NotNull(zoneRes.ZoneData);
        }

        [Fact]
        public void ChannelInfo_and_ChatMessage_parse_correctly()
        {
            var channelData = new Dictionary<string, object>
            {
                { k.name, "General" },
                { k.type, 0 },
                { k.password, 0 },
                { k.count, 15 },
                { k.topic, "Welcome to OpenPerpetuum" }
            };

            var channel = ChannelInfo.FromDictionary(channelData);
            Assert.Equal("General", channel.Name);
            Assert.Equal(0, channel.Type);
            Assert.Equal("Public", channel.TypeName);
            Assert.False(channel.HasPassword);
            Assert.Equal(15, channel.MemberCount);
            Assert.Equal("Welcome to OpenPerpetuum", channel.Topic);

            var notificationData = new Dictionary<string, object>
            {
                { k.channel, "General" },
                { k.command, 6 }, // ChannelNotify.Message
                { k.data, new Dictionary<string, object>
                    {
                        { k.sender, 501 },
                        { k.message, "Greetings from zone 0!" }
                    }
                }
            };

            var chat = ChatMessage.FromNotification(notificationData);
            Assert.NotNull(chat);
            Assert.Equal("General", chat.ChannelName);
            Assert.Equal(501, chat.SenderId);
            Assert.Equal("Greetings from zone 0!", chat.Message);
        }

        [Fact]
        public void GenXY_serialization_deserialization_models_roundtrip()
        {
            // Simulate how GenxyWriter encodes server messages
            var originalDict = new Dictionary<string, object>
            {
                { k.accountID, 123 },
                { k.email, "test@openperpetuum.com" },
                { k.accLevel, (int)AccessLevel.normal },
                { k.credit, 2000000 },
                { k.isSubscriber, 1 }, // GenXY writes boolean as int
                { k.emailConfirmed, 1 }
            };

            var genxyString = GenxyConverter.Serialize(originalDict);
            var deserializedDict = GenxyConverter.Deserialize(genxyString);

            var account = AccountInfo.FromDictionary(deserializedDict);
            Assert.Equal(123, account.AccountId);
            Assert.Equal("test@openperpetuum.com", account.Email);
            Assert.Equal(AccessLevel.normal, account.AccessLevel);
            Assert.Equal(2000000, account.Credit);
            Assert.True(account.IsSubscriber);
            Assert.True(account.EmailConfirmed);
        }

        [Fact]
        public void Error_checking_throws_PerpetuumClientException_on_server_error()
        {
            var errorData = new Dictionary<string, object>
            {
                { k.rErr, (int)ErrorCodes.NoSuchUser }
            };

            var msg = new Message(Commands.SignIn, errorData);

            var ex = Assert.Throws<PerpetuumClientException>(() => PerpetuumClient.CheckResponseError(msg));
            Assert.Equal(ErrorCodes.NoSuchUser, ex.ErrorCode);
            Assert.Contains("Invalid username/email or password", ex.Message);
        }

        [Fact]
        public void Commands_lookup_works_for_registered_commands()
        {
            var cmd = Commands.GetCommandByText("signIn");
            Assert.NotNull(cmd);
            Assert.Equal("signIn", cmd.Text);

            var welcomeCmd = Commands.GetCommandByText("welcome");
            Assert.NotNull(welcomeCmd);
            Assert.Equal("welcome", welcomeCmd.Text);

            var talkCmd = Commands.GetCommandByText("channelTalk");
            Assert.NotNull(talkCmd);
            Assert.Equal("channelTalk", talkCmd.Text);
        }

        [Fact]
        public void Full_handshake_and_uncompressed_message_codec_simulation()
        {
            // 1. Client generates 40-byte RC4 key and encrypts with RSA
            byte[] clientStreamKey = FastRandom.NextBytes(40);
            var clientRc4 = new Rc4(clientStreamKey);

            byte[] rsaEncrypted = Rsa.Encrypt(clientStreamKey)!;
            Assert.NotNull(rsaEncrypted);

            // 2. Server decrypts RSA and creates server RC4
            byte[] decryptedKey = Rsa.Decrypt(rsaEncrypted)!;
            Assert.NotNull(decryptedKey);
            Assert.Equal(clientStreamKey, decryptedKey);
            var serverRc4 = new Rc4(decryptedKey);

            // 3. Server sends Welcome message (uncompressed)
            var welcomeData = new Dictionary<string, object>
            {
                { k.worldName, "Perpetuum" },
                { "version", "p36.5" },
                { "steamLoginEnabled", 0 }
            };
            var serverWelcomeMsg = new Message(Commands.Welcome, welcomeData) { Sender = "Relay" };
            byte[] serverRawBytes = serverWelcomeMsg.ToBytes();

            // Server OnProcessOutputRawData logic:
            byte compressionLevel = 0;
            var serverOutPacket = new byte[serverRawBytes.Length + 3];
            Buffer.BlockCopy(serverRawBytes, 0, serverOutPacket, 3, serverRawBytes.Length);
            serverRc4.Encrypt(serverOutPacket);
            var serverWireData = new byte[serverOutPacket.Length + 1];
            Buffer.BlockCopy(serverOutPacket, 0, serverWireData, 1, serverOutPacket.Length);
            serverWireData[0] = compressionLevel;

            // 4. Client receives wire data and decodes:
            clientRc4.Decrypt(serverWireData, 1, serverWireData.Length - 1);
            int dataOffset = 4;
            if (serverWireData[0] == 2)
            {
                serverWireData = GZip.Decompress(serverWireData, 5);
                dataOffset--;
            }
            string clientReceivedText = Encoding.UTF8.GetString(serverWireData, dataOffset, serverWireData.Length - dataOffset);
            var clientParsedMessage = Message.Parse(clientReceivedText);

            Assert.Equal(Commands.Welcome.Text, clientParsedMessage.Command.Text);
            var welcome = WelcomeInfo.FromDictionary(clientParsedMessage.Data);
            Assert.Equal("Perpetuum", welcome.WorldName);
            Assert.Equal("p36.5", welcome.Version);
            Assert.False(welcome.SteamLoginEnabled);
        }

        [Fact]
        public void Full_compressed_message_codec_simulation()
        {
            // Setup keys
            byte[] streamKey = FastRandom.NextBytes(40);
            var clientRc4 = new Rc4(streamKey);
            var serverRc4 = new Rc4(streamKey);

            // Server builds a large character list payload (> 1024 bytes)
            var charactersDict = new Dictionary<string, object>();
            for (int i = 0; i < 20; i++)
            {
                charactersDict.Add($"c{i}", new Dictionary<string, object>
                {
                    { k.characterID, 1000 + i },
                    { k.rootEID, 5000000L + i },
                    { k.nick, $"TestPilotNumber_{i}_LongNameForPadding" },
                    { k.docked, 1 },
                    { k.baseName, "Syndicate Primary Alpha Base Research Station" },
                    { k.credit, 1000000L * (i + 1) }
                });
            }
            var rootData = new Dictionary<string, object>
            {
                { k.result, new Dictionary<string, object> { { "characters", charactersDict }, { "extensionPoints", 150000 } } }
            };
            var serverCharListMsg = new Message(Commands.CharacterList, rootData) { Sender = "Relay" };
            byte[] serverRawBytes = serverCharListMsg.ToBytes();
            Assert.True(serverRawBytes.Length > 1024, "Test payload should exceed 1024 bytes to trigger compression");

            // Server OnProcessOutputRawData logic:
            byte compressionLevel = 0;
            var serverOutPacket = new byte[serverRawBytes.Length + 3];
            Buffer.BlockCopy(serverRawBytes, 0, serverOutPacket, 3, serverRawBytes.Length);
            if (serverOutPacket.Length > 1024)
            {
                compressionLevel = 2;
                serverOutPacket = GZip.Compress(serverOutPacket);
            }
            serverRc4.Encrypt(serverOutPacket);
            var serverWireData = new byte[serverOutPacket.Length + 1];
            Buffer.BlockCopy(serverOutPacket, 0, serverWireData, 1, serverOutPacket.Length);
            serverWireData[0] = compressionLevel;

            Assert.Equal(2, serverWireData[0]);

            // Client receives wire data and decodes:
            clientRc4.Decrypt(serverWireData, 1, serverWireData.Length - 1);
            int dataOffset = 4;
            if (serverWireData[0] == 2)
            {
                serverWireData = GZip.Decompress(serverWireData, 5);
                dataOffset--;
            }
            string clientReceivedText = Encoding.UTF8.GetString(serverWireData, dataOffset, serverWireData.Length - dataOffset);
            var clientParsedMessage = Message.Parse(clientReceivedText);

            Assert.Equal(Commands.CharacterList.Text, clientParsedMessage.Command.Text);
            var result = CharacterListResult.FromDictionary(clientParsedMessage.Data);
            Assert.Equal(150000, result.ExtensionPoints);
            Assert.Equal(20, result.Characters.Count);
            Assert.Equal("TestPilotNumber_0_LongNameForPadding", result.Characters[0].Nick);
            Assert.Equal("TestPilotNumber_19_LongNameForPadding", result.Characters[19].Nick);
        }

        [Fact]
        public void StorageResult_and_StorageItemInfo_parse_correctly()
        {
            var defDict = new Dictionary<int, string>
            {
                { 101, "def_robot_arkhe" },
                { 202, "def_ammo_longrange_missile_a" },
                { 303, "def_module_laser_small" }
            };

            var item1 = new Dictionary<string, object>
            {
                { k.eid, 50001L },
                { k.definition, 101 },
                { k.quantity, 1 },
                { k.repackaged, 1 },
                { k.health, 100.0 },
                { k.volume, 50.0 },
                { k.parent, 10001L },
                { k.owner, 2001L }
            };

            var subItem = new Dictionary<string, object>
            {
                { k.eid, 50003L },
                { k.definition, 303 },
                { k.name, "Custom Laser" },
                { k.quantity, 2 },
                { k.repackaged, 0 },
                { k.health, 98.5 },
                { k.volume, 4.0 },
                { k.parent, 50002L },
                { k.owner, 2001L }
            };

            var item2 = new Dictionary<string, object>
            {
                { k.eid, 50002L },
                { k.definition, 202 },
                { k.quantity, 500 },
                { k.repackaged, 1 },
                { k.health, 100.0 },
                { k.volume, 10.5 },
                { k.parent, 10001L },
                { k.owner, 2001L },
                { k.items, new Dictionary<string, object> { { "c0", subItem } } }
            };

            var storagePayload = new Dictionary<string, object>
            {
                { k.eid, 10001L },
                { k.definition, 999 },
                { k.baseEID, 777L },
                { k.items, new Dictionary<string, object>
                    {
                        { "c0", item1 },
                        { "c1", item2 }
                    }
                }
            };

            var storage = StorageResult.FromDictionary(storagePayload, defDict);

            Assert.Equal(10001L, storage.ContainerEid);
            Assert.Equal(999, storage.ContainerDefinition);
            Assert.Equal(777L, storage.BaseEid);
            Assert.Equal(2, storage.Items.Count);
            Assert.Equal(501, storage.TotalItemCount);
            Assert.Equal(60.5, storage.TotalVolume);

            var first = storage.Items[0];
            Assert.Equal(50001L, first.Eid);
            Assert.Equal(101, first.Definition);
            Assert.Equal("def_robot_arkhe", first.DefinitionName);
            Assert.Equal("Robot Arkhe", first.DisplayName);
            Assert.Equal(1, first.Quantity);
            Assert.True(first.IsRepackaged);
            Assert.Equal(50.0, first.Volume);

            var second = storage.Items[1];
            Assert.Equal(50002L, second.Eid);
            Assert.Equal("def_ammo_longrange_missile_a", second.DefinitionName);
            Assert.Equal("Ammo Longrange Missile A", second.DisplayName);
            Assert.Equal(500, second.Quantity);
            Assert.Single(second.Items);

            var child = second.Items[0];
            Assert.Equal(50003L, child.Eid);
            Assert.Equal("Custom Laser", child.DisplayName);
            Assert.Equal(2, child.Quantity);
            Assert.False(child.IsRepackaged);
            Assert.Equal(98.5, child.Health);
        }

        [Fact]
        public void StorageItemInfo_FormatDefinitionName_handles_various_formats()
        {
            Assert.Equal("Robot Arkhe", StorageItemInfo.FormatDefinitionName("def_robot_arkhe"));
            Assert.Equal("Ammo Longrange Missile A", StorageItemInfo.FormatDefinitionName("def_ammo_longrange_missile_a"));
            Assert.Equal("Laser Small", StorageItemInfo.FormatDefinitionName("laser_small"));
            Assert.Equal(string.Empty, StorageItemInfo.FormatDefinitionName(""));
        }

        [Fact]
        public void CommandLineOptions_parses_storage_flags()
        {
            var opt1 = CommandLineOptions.Parse(new[] { "--storage" });
            Assert.True(opt1.ListStorage);

            var opt2 = CommandLineOptions.Parse(new[] { "-s" });
            Assert.True(opt2.ListStorage);

            var opt3 = CommandLineOptions.Parse(new[] { "--hangar" });
            Assert.True(opt3.ListStorage);

            var opt4 = CommandLineOptions.Parse(new[] { "--inventory" });
            Assert.True(opt4.ListStorage);
        }
    }
}
