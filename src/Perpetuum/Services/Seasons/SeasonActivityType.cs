namespace Perpetuum.Services.Seasons
{
    public enum SeasonActivityType
    {
        NpcKill            = 1,
        PvpKill            = 2,
        MissionComplete    = 3,
        MineralMined       = 4,
        EpSpent            = 5,
        NicEarned          = 6,
        NicSpent           = 7,
        IntrusionPoint     = 8,

        // Phase 1 — non-combat
        Prototyping        = 9,
        ReverseEngineering = 10,
        Production         = 11,
        ArtifactFound      = 12,
        EpEarned           = 13,
    }
}
