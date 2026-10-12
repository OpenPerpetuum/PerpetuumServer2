namespace Perpetuum.Services.Sparky;

internal enum SparkySource
{
    Game,
    Discord
}

internal readonly record struct SparkyRequestIdentity(SparkySource Source, ulong Id);
