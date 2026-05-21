namespace Perpetuum.Services.Seasons
{
    public record ActivityEvent(long Amount, int? DefinitionId = null, int? CounterpartyAccountId = null);
}
