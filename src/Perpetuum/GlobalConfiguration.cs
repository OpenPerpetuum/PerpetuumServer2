using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Perpetuum.Zones.NpcSystem.Presences.PathFinders;
using System.Collections.Generic;
using System.ComponentModel;

namespace Perpetuum
{
    public class GlobalConfiguration
    {
        public string ListenerIP { get; set; }
        public int ListenerPort { get; set; }

        public string GameRoot { get; set; }
        public string WebServiceIP { get; set; }
        public string PersonalConfig { get; set; }
        public string ConnectionString { get; set; }
        public string RelayName => "relay";

        public bool EnableUpnp { get; set; }

        public int SteamAppID { get; set; }
        public byte[] SteamKey { get; set; }

        public string ResourceServerURL { get; set; }

        public bool EnableDev { get; set; }

        public CorporationConfiguration Corporation { get; set; }

        public bool StartServerInAdminOnlyMode { get; set; }

        // Default NIC value for new player.
        [DefaultValue(500000), JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int StartCredit { get; set; }

        // Default NIC per level value for new player.
        [DefaultValue(125000), JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int LevelCredit { get; set; }

        // Default EP value for new player.
        [DefaultValue(40000), JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int StartEP { get; set; }

        // Default camouflage bonus value.
        [DefaultValue(5), JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int CamouflageBonus { get; set; }

        public string WebHookId { get; set; }

        public string WebHookOAuth { get; set; }

        public string DiscordBotToken { get; set; }

        public string OpHelpChannelId { get; set; }

        public bool EnableSparky { get; set; }

        public bool SparkyQuestionCaptureEnabled { get; set; }

        public int SparkyQuestionCaptureDurationHours { get; set; } = 48;

        public List<ulong> SparkyQuestionCaptureDiscordChannelIds { get; set; } = new();

        public string SparkyQuestionCapturePath { get; set; }

        public string SparkyApiUrl { get; set; } = "https://127.0.0.1:5080/v1/answer";

        public string SparkyTraceSpoolPath { get; set; } = "logs/sparky-trace-spool.jsonl";

        public int SparkyTraceSpoolMaxBytes { get; set; } = 32 * 1024 * 1024;

        public string SparkyProtectedSettingsPath { get; set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Sparky",
            "vdm-client-settings.json");

        public int SparkyRequestTimeoutSeconds { get; set; } = 90;

        public int SparkyMaxQuestionCharacters { get; set; } = 1000;

        public int SparkyMaxAnswerCharacters { get; set; } = 2000;

        public int SparkyQueueCapacity { get; set; } = 64;

        public int SparkyWorkerCount { get; set; } = 2;

        public int SparkyPendingPerCallerLimit { get; set; } = 1;

        public int SparkyContextLifetimeMinutes { get; set; } = 15;

        public int SparkyContextMaxTurns { get; set; } = 8;

        public int SparkyContextMaxCharacters { get; set; } = 6000;

        public int SparkyContextCapacity { get; set; } = 2048;

        public string SparkySenderCharacterNick { get; set; } = "[OPP] Sparky - The Syndicate Welcome Agent";

        public string SparkyDiscordOfficerRoleIds { get; set; } = string.Empty;

        public string SparkyDiscordCeoRoleIds { get; set; } = string.Empty;

        public string SparkyDiscordCommandDirectorRoleIds { get; set; } = string.Empty;

        // Default roaming mode.
        [DefaultValue(RoamingState.RoamingMode.Default)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        [JsonConverter(typeof(StringEnumConverter))]
        public RoamingState.RoamingMode RoamingMode { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of concurrent pathfinding operations allowed.
        /// </summary>
        [DefaultValue(0), JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int ConcurrentPathFinds { get; set; }
    }
}
