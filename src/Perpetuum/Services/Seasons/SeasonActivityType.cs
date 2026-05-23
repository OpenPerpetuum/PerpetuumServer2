namespace Perpetuum.Services.Seasons
{
    public enum SeasonActivityType
    {
        NpcKill              = 1,
        PvpKill              = 2,
        MissionComplete      = 3,
        MineralMined         = 4,
        EpSpent              = 5,
        NicEarned            = 6,
        NicSpent             = 7,
        IntrusionPoint       = 8,

        // Phase 1 — non-combat
        Prototyping          = 9,
        ReverseEngineering   = 10,
        Production           = 11,
        ArtifactFound        = 12,
        EpEarned             = 13,

        // Phase 2 — combat
        DamageDone           = 14,
        DamageReceived       = 15,
        ArmorRestored        = 16,
        EnergyDrainDealt     = 17,
        EnergyDrainReceived  = 18,
        EnergyTransferDealt  = 19,
        EnergyTransferReceived = 20,
        PlantHarvested       = 21,
    }
}
