using Perpetuum.AdminTool.Settings;

namespace Perpetuum.AdminTool.Economy;

public interface IEconomyDashboardRepositoryFactory
{
    IEconomyMoneySupplyRepository CreateMoneySupply(ConnectionSettings connection);
    IEconomyMarketHealthRepository CreateMarketHealth(ConnectionSettings connection);
    IEconomySinkRepository CreateSink(ConnectionSettings connection);
    IEconomyInsuranceRepository CreateInsurance(ConnectionSettings connection);
}

public sealed class EconomyDashboardRepositoryFactory : IEconomyDashboardRepositoryFactory
{
    public IEconomyMoneySupplyRepository CreateMoneySupply(ConnectionSettings connection) =>
        new EconomyMoneySupplyRepository(connection);
    public IEconomyMarketHealthRepository CreateMarketHealth(ConnectionSettings connection) =>
        new EconomyMarketHealthRepository(connection);
    public IEconomySinkRepository CreateSink(ConnectionSettings connection) =>
        new EconomySinkRepository(connection);
    public IEconomyInsuranceRepository CreateInsurance(ConnectionSettings connection) =>
        new EconomyInsuranceRepository(connection);
}
