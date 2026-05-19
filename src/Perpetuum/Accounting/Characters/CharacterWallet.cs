using Perpetuum.Common.Loggers.Transaction;
using Perpetuum.Services.Seasons;
using Perpetuum.Wallets;

namespace Perpetuum.Accounting.Characters
{
    public class CharacterWallet : Wallet<double>, ICharacterWallet
    {
        public CharacterWallet(Character character, ICharacterCreditService creditService, TransactionType transactionType)
        {
            this.character = character;
            this.creditService = creditService;
            this.transactionType = transactionType;
        }

        private readonly Character character;
        private readonly ICharacterCreditService creditService;
        private readonly TransactionType transactionType;

        protected override double GetBalance()
        {
            return creditService.GetCredit(character.Id);
        }

        protected override void SetBalance(double value)
        {
            creditService.SetCredit(character.Id, value);
        }

        protected override void OnBalanceUpdating(double currentCredit, double desiredCredit)
        {
            desiredCredit.ThrowIfLess(0, ErrorCodes.CharacterNotEnoughMoney);
        }

        protected override void OnCommited(double startBalance)
        {
            double currentCredit = GetBalance();
            double change = currentCredit - startBalance;

            Dictionary<string, object> info = new Dictionary<string, object>
            {
                { k.credit, (long)currentCredit},
                { k.amount,change},
                { k.transactionType, (int)transactionType },
            };

            Message.Builder
                .SetCommand(Commands.CharacterUpdateBalance)
                .WithData(info)
                .ToCharacter(character)
                .Send();

            switch (transactionType)
            {
                case TransactionType.hangarRent:
                case TransactionType.marketBuy:
                case TransactionType.hangarRentAuto:
                case TransactionType.marketFee:
                case TransactionType.buyOrderDeposit:
                case TransactionType.corporationCreate:
                case TransactionType.extensionLearn:
                case TransactionType.ItemRepair:
                case TransactionType.ProductionManufacture:
                case TransactionType.ProductionResearch:
                case TransactionType.ProductionMultiItemRepair:
                case TransactionType.ProductionPrototype:
                case TransactionType.ProductionMassProduction:
                case TransactionType.InsuranceFee:
                case TransactionType.BoxRequest:
                case TransactionType.MarketTax:
                case TransactionType.ModifyMarketOrder:
                case TransactionType.SparkUnlock:
                case TransactionType.SparkActivation:
                case TransactionType.ResearchKitMerge:
                case TransactionType.ProductionCPRGForge:
                case TransactionType.ItemShopBuy:
                case TransactionType.TransportAssignmentSubmit:
                case TransactionType.ItemShopCreditTake:
                    SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.NicSpent, new ActivityEvent((long)Math.Abs(change)));

                    break;
                case TransactionType.marketSell:
                case TransactionType.buyOrderPayBack:
                case TransactionType.missionPayOut:
                case TransactionType.refund:
                case TransactionType.InsurancePayOut:
                case TransactionType.GoodiePackCredit:
                    SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.NicEarned, new ActivityEvent((long)change));

                    break;
                default:
                    break;
            }
        }
    }
}