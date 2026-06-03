using CommunityToolkit.Mvvm.ComponentModel;
using Perpetuum.AdminTool.Common;
using Perpetuum.AdminTool.Economy;
using Perpetuum.AdminTool.Editing;

namespace Perpetuum.AdminTool.ViewModels
{
    public partial class EconomyViewModel : ObservableObject
    {
        public EconomyNicFlowViewModel           NicFlow           { get; }
        public EconomyMoneySupplyViewModel       MoneySupply       { get; }
        public EconomyMarketHealthViewModel      MarketHealth      { get; }
        public EconomySinkEffectivenessViewModel SinkEffectiveness { get; }

        public EconomyViewModel(
            EconomyRepository            nicFlowRepo,
            EconomyMoneySupplyRepository  moneySupplyRepo,
            EconomyMarketHealthRepository marketHealthRepo,
            EconomySinkRepository         sinkRepo,
            ChangeQueue                   changes,
            LookupCache                   lookups)
        {
            NicFlow           = new EconomyNicFlowViewModel(nicFlowRepo);
            MoneySupply       = new EconomyMoneySupplyViewModel(moneySupplyRepo);
            MarketHealth      = new EconomyMarketHealthViewModel(marketHealthRepo, changes, lookups);
            SinkEffectiveness = new EconomySinkEffectivenessViewModel(sinkRepo);
        }
    }
}
