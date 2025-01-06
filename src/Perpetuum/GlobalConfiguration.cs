using Newtonsoft.Json;
using System.ComponentModel;

namespace Perpetuum
{
    public class GlobalConfiguration
    {
        public required string ListenerIP { get; set; }
        public int ListenerPort { get; set; }

        public required string GameRoot { get; set; }
        public required string WebServiceIP { get; set; }
        public required string PersonalConfig { get; set; }
        public required string ConnectionString { get; set; }
        public string RelayName => "relay";

        public bool EnableUpnp { get; set; }

        public int SteamAppID { get; set; }
        public required byte[] SteamKey { get; set; }

        public required string ResourceServerURL { get; set; }

        public bool EnableDev { get; set; }

        public required CorporationConfiguration Corporation { get; set; }

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
    }
}
