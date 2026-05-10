using Microsoft.Data.SqlClient;
using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Services.Seasons;
using Perpetuum.Log;
using Perpetuum.Services.EventServices;
using Perpetuum.Services.ExtensionService;
using System.Transactions;

namespace Perpetuum.Accounting
{
    public class AccountManager : IAccountManager
    {
        private const double BOOSTMULTIPLIERMAX = 25;
        private const double SERVER_DESIRED_EP_LEVEL = 1000000;
        private const double GAURANTEED_BOOST_MAX_THRESH = 45000;
        private readonly AccountTransactionLogger transactionLogger;
        private readonly AccountWalletFactory walletFactory;
        private readonly EpForActivityLogger epForActivityLogger;
        private readonly EPBonusEventService epBonusEventService;

        public AccountManager(IAccountRepository accountRepository,
            AccountTransactionLogger transactionLogger,
            AccountWalletFactory walletFactory,
            EpForActivityLogger epForActivityLogger,
            EPBonusEventService epBonusEventService)
        {
            Repository = accountRepository;
            this.transactionLogger = transactionLogger;
            this.walletFactory = walletFactory;
            this.epForActivityLogger = epForActivityLogger;
            this.epBonusEventService = epBonusEventService;
        }

        public IAccountRepository Repository { get; }

        public IAccountWallet GetWallet(Account account, AccountTransactionType transactionType)
        {
            return walletFactory(account, transactionType);
        }

        public void LogTransaction(AccountTransactionLogEvent e)
        {
            transactionLogger.Log(e);
        }

        public IEnumerable<AccountTransactionLogEvent> GetTransactionHistory(Account account, TimeSpan offset, TimeSpan length)
        {
            DateTime later = DateTime.Now - offset;
            DateTime earlier = later - length;

            return Db.Query()
                .CommandText("select * from accounttransactionlog where accountId = @accountId and created between @earlier and @later and creditchange != 0")
                .SetParameter("@accountId", account.Id)
                .SetParameter("@earlier", earlier)
                .SetParameter("@later", later)
                .Execute().Select(r =>
                {
                    AccountTransactionType transactionType = (AccountTransactionType)r.GetValue<int>("transactionType");

                    return new AccountTransactionLogEvent(account, transactionType)
                    {
                        Definition = r.GetValue<int?>("definition"),
                        Quantity = r.GetValue<int?>("quantity"),
                        Credit = r.GetValue<int>("credit"),
                        CreditChange = r.GetValue<int>("creditChange"),
                        Created = r.GetValue<DateTime>("created")
                    };
                }).ToArray();
        }


        public int GetActiveCharactersCount(Account account)
        {
            return Db.Query()
                .CommandText("select count(*) from characters where accountid=@accountID and active=1")
                .SetParameter("@accountID", account.Id)
                .ExecuteScalar<int>();
        }

        public Character[] GetDeletedCharacters(Account account)
        {
            return Db.Query()
                .CommandText("select characterid from characters where accountid=@accountID and deletedat is not null and active=0")
                .SetParameter("@accountID", account.Id)
                .Execute()
                .Select(r => Character.Get(r.GetValue<int>(0))).ToArray();
        }

        private int GetLockedEpByCharacters(Account account, IList<Character> characters)
        {
            if (characters.Count <= 0)
            {
                return 0;
            }

            int lockedEp = Db.Query()
                .CommandText($"SELECT COALESCE(SUM(points),0) FROM dbo.accountextensionspent WHERE characterid IN ( {characters.Select(c => c.Id).ArrayToString()} ) AND accountid=@accountID")
                .SetParameter("@accountID", account.Id)
                .ExecuteScalar<int>();

            return lockedEp;
        }

        public int GetLockedEpByAccount(Account account)
        {
            Character[] deletedCharacters = GetDeletedCharacters(account);

            if (deletedCharacters.Length <= 0)
            {
                return 0;
            }

            int lockedEp = GetLockedEpByCharacters(account, deletedCharacters);

            return lockedEp;
        }

        public IDictionary<string, object> GetEPData(Account account, Character character)
        {
            int availablePoints = CalculateCurrentEp(account);
            int totalEPSpentByCharacter = GetSumExtensionPointsSpentByCharacter(account, character);
            int spentEPPerAccount = GetSumExtensionPointsSpent(account);
            int lockedEPPerAccount = GetLockedEpByAccount(account);
            int epCollected = GetExtensionPointsCollected(account);
            double boostFactor = GetExperienceBoostingFactor(epCollected, SERVER_DESIRED_EP_LEVEL);
            int onePointRate =
                CalculateBoostedExtensionPoint(1, GetBoostMultiplier(boostFactor, GetEpBonusFromEvent(), GetEpBonusFromSubscription(account)));
            onePointRate = account.IsDailyEpBoosted ? onePointRate * 2 : onePointRate;

            Dictionary<string, object> result = new()
            {
                {k.points, availablePoints},
                {"spentEpPerCharacter", totalEPSpentByCharacter},
                {"spentEpPerAccount", spentEPPerAccount},
                {k.lockedEp, lockedEPPerAccount},
                {"epCollected", epCollected}, // ep ever gained
                {k.boostFactor, boostFactor}, // 0 ... 1
                {"serverBoostLevel", (int)SERVER_DESIRED_EP_LEVEL }, // server level
                {"onePointRate", onePointRate } // int 1:onePointRate display
            };

            return result;
        }

        public int CalculateCurrentEp(Account account)
        {
            int dailySum = GetDailyPointsSum(account);
            int penaltySum = GetPenaltyPointsSum(account);
            int ingameSpent = GetSumExtensionPointsSpent(account);
            int resultEp = dailySum - penaltySum - ingameSpent;

            return resultEp;
        }

        /// <summary>
        /// Daily EP batches sum for an account
        /// </summary>
        /// <returns></returns>
        public int GetDailyPointsSum(Account account)
        {
            int? result = Db.Query()
                .CommandText("SELECT SUM(points) FROM extensionpoints WHERE accountid=@accountID")
                .SetParameter("@accountID", account.Id)
                .ExecuteScalar<int?>();

            return result ?? 0;
        }

        /// <summary>
        /// Reutns the sum of penalty points. This number should be SUBTRACTED from the nominal EP
        /// </summary>
        /// <returns></returns>
        public int GetPenaltyPointsSum(Account account)
        {
            int? result = Db.Query()
                .CommandText("select sum(points) from extensionpointpenalty where accountid=@accountID")
                .SetParameter("@accountID", account.Id)
                .ExecuteScalar<int?>();

            return result ?? 0;
        }

        /// <summary>
        /// sum of the extension points the account spent on this character 
        /// </summary>
        public int GetSumExtensionPointsSpentByCharacter(Account account, Character character)
        {
            return Db.Query()
                .CommandText("select sum(points) from accountextensionspent WHERE accountID=@accountID and characterid=@characterID")
                .SetParameter("@accountID", account.Id)
                .SetParameter("@characterID", character.Id)
                .ExecuteScalar<int>();
        }

        /// <summary>
        /// sum of the extension points the account has ever spent
        /// </summary>
        /// <returns></returns>
        public int GetSumExtensionPointsSpent(Account account)
        {
            int spentSum = Db.Query()
                .CommandText("SELECT SUM(points) FROM accountextensionspent WHERE accountID=@accountID")
                .SetParameter("@accountID", account.Id)
                .ExecuteScalar<int>();

            return spentSum;
        }

        public int GetExtensionPointsCollected(Account account)
        {
            int collectedEp = Db.Query().CommandText("SELECT dbo.extensionPointsCollected(@accountID)")
                .SetParameter("@accountID", account.Id)
                .ExecuteScalar<int>();

            return collectedEp;
        }

        public void FreeLockedEp(Account account, int amount)
        {
            List<int> deletedCharacters = GetDeletedCharacters(account).Select(c => c.Id).ToList();

            List<int> deleteIds = new();
            int pointsToDelete = amount;

            if (deletedCharacters.Count <= 0)
            {
                return;
            }

            List<KeyValuePair<int, int>> entries = Db.Query()
                .CommandText($"select id,points from accountextensionspent where accountid=@accountID and characterid in ({deletedCharacters.ArrayToString()})")
                .SetParameter("@accountID", account.Id)
                .Execute().Select(r => new KeyValuePair<int, int>(r.GetValue<int>(0), r.GetValue<int>(1)))
                .ToList();
            int allLocked = entries.Sum(l => l.Value);

            if (allLocked < amount)
            {
                throw new PerpetuumException(ErrorCodes.InputTooHigh);
            }

            foreach (KeyValuePair<int, int> keyValuePair in entries)
            {
                int id = keyValuePair.Key;
                int points = keyValuePair.Value;

                if (pointsToDelete < points)
                {
                    //update and exit loop

                    Db.Query().CommandText("update accountextensionspent set points=@newPoints where id=@id")
                        .SetParameter("@id", id)
                        .SetParameter("@newPoints", points - pointsToDelete)
                        .ExecuteNonQuery();

                    break;
                }

                //consume the entry and continue

                pointsToDelete -= points;

                //collect ids to delete
                deleteIds.Add(id);

                if (pointsToDelete <= 0)
                {
                    //required amount was found
                    break;
                }

            }

            if (deleteIds.Count > 0)
            {
                Db.Query()
                    .CommandText($"delete accountextensionspent where id in ({deleteIds.ArrayToString()})")
                    .ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Logs the extension remove actions, not used in game mechanics
        /// </summary>
        public void InsertExtensionRemoveLog(Account account, Character character, int extensionId, int extensionLevel, int points)
        {
            const string sqlInsertCommand =
                @"insert extensionremovelog 
                (accountid,characterid,extensionid,extensionlevel,points) values 
                (@accountID,@characterID,@extensionID,@extensionLevel,@points)";

            Db.Query()
                .CommandText(sqlInsertCommand)
                .SetParameter("@accountID", account.Id)
                .SetParameter("@characterID", character.Id)
                .SetParameter("@extensionID", extensionId)
                .SetParameter("@extensionLevel", extensionLevel)
                .SetParameter("@points", points)
                .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);
        }

        /// <summary>
        /// the account bought extensions ingame -> insert record
        /// </summary>
        public void AddExtensionPointsSpent(Account account, Character character, int spentPoints, int extensionID, int extensionLevel)
        {
            Db.Query()
                .CommandText("insert accountextensionspent (accountid, points, extensionID, extensionlevel, characterID) values (@accountID, @points,@extensionID,@extensionLevel,@characterID)")
                .SetParameter("@accountID", account.Id)
                .SetParameter("@points", spentPoints)
                .SetParameter("@extensionID", extensionID)
                .SetParameter("@extensionLevel", extensionLevel)
                .SetParameter("@characterID", character.Id)
                .ExecuteNonQuery().ThrowIfEqual(0, ErrorCodes.SQLInsertError);

            SeasonServiceLocator.Instance?.RecordActivity(character.Id, SeasonActivityType.EpSpent, spentPoints);
        }

        /// <summary>
        /// Starts the subscription period
        /// </summary>
        public void ExtensionSubscriptionStart(Account account, DateTime startTime, DateTime endTime, int multiplierBonus)
        {
            try
            {
                Db.Query()
                    .CommandText("opp.extensionSubscriptionStart")
                    .SetParameter("@accountID", account.Id)
                    .SetParameter("@startTime", startTime)
                    .SetParameter("@endTime", endTime)
                    .SetParameter("@multiplierBonus", multiplierBonus)
                    .ExecuteNonQuery();
            }
            catch (SqlException exc)
            {
                exc.Number.ThrowIfEqual(100000, ErrorCodes.ItemNotUsable);
            }
        }

        public void InsertPenaltyPoint(Account account, AccountExtensionPenaltyType penaltyType, int points, bool forever)
        {
            int affected =
                Db.Query()
                .CommandText("INSERT dbo.extensionpointpenalty ( accountid, points, penaltytype, forever ) VALUES  ( @accountID,@points,@penaltyType,@forever )")
                    .SetParameter("@accountID", account.Id)
                    .SetParameter("@points", points)
                    .SetParameter("@penaltyType", (int)penaltyType)
                    .SetParameter("@forever", forever)
                    .ExecuteNonQuery();


            (affected == 1).ThrowIfFalse(ErrorCodes.SQLInsertError);
        }

        public int AddExtensionPointsBoostAndLog(Account account, Character character, EpForActivityType activityType, int points)
        {
            if (points <= 0)
            {
                return 0;
            }

            double bonusIncrease = GetEpBonusFromEvent();
            int itemIncrease = GetEpBonusFromSubscription(account);
            int rawPoints = points;
            double boostFactor = GetExperienceBoostingFactor(GetExtensionPointsCollected(account), SERVER_DESIRED_EP_LEVEL);
            int boostedPoints = GetBoostedExtensionPoints(account, points, bonusIncrease, itemIncrease);

            Transaction.Current.OnCommited(() =>
            {
                LogEpForActivity(
                    account,
                    character,
                    activityType,
                    rawPoints,
                    boostedPoints,
                    boostFactor,
                    (int)BOOSTMULTIPLIERMAX,
                    (int)bonusIncrease);
            });

            AddExtensionPoints(account, boostedPoints);
            return boostedPoints;
        }

        public void AddExtensionPoints(Account account, int pointsToInject)
        {
            if (pointsToInject <= 0)
            {
                return;
            }

            InjectExtensionPoints(account, pointsToInject);

            Transaction.Current.OnCommited(() =>
            {
                ExtensionHelper.CreateExtensionPointsIncreasedMessage(pointsToInject).ToAccount(account).Send();
            });
        }

        public IEnumerable<EpForActivityLogEvent> GetEpForActivityHistory(Account account, DateTime earlier, DateTime later)
        {
            return Db.Query()
                .CommandText("select * from epforactivitylog where accountId = @accountId and eventtime between @earlier and @later")
                .SetParameter("@accountId", account.Id)
                .SetParameter("@earlier", earlier)
                .SetParameter("@later", later)
                .Execute().Select(r =>
                {
                    EpForActivityType transactionType = (EpForActivityType)r.GetValue<int>("epforactivitytype");

                    return new EpForActivityLogEvent(transactionType)
                    {
                        CharacterId = r.GetValue<int>("characterid"),
                        RawPoints = r.GetValue<int>("rawpoints"),
                        Points = r.GetValue<int>("points"),
                        BoostFactor = r.GetValue<double>("boostfactor"),
                        BoostMultiplier = r.GetValue<int>(k.multiplier),
                        Created = r.GetValue<DateTime>("eventtime")
                    };
                }).ToArray();
        }

        public void PackageGenerateAll(Account account)
        {
            // generaljunk neki josagokat
            Db.Query()
                .CommandText("accountPackageGenerateAll")
                .SetParameter("@accountId", account.Id)
                .ExecuteNonQuery();
        }


        /// <summary>
        /// Computes the final EPMultiplier for an account.
        /// </summary>
        /// <param name="boostFactor">Normalized value mapping AccountEP from 0->SERVER_DESIRED_EP_LEVEL as [1.0->0.0]</param>
        /// <param name="bonusIncrease">A boostfactor-agnostic multiplier -- usually from EPBonusEvents</param>
        /// <returns>EP Multiplier</returns>
        private static double GetBoostMultiplier(double boostFactor, double bonusIncrease, int itemIncrease)
        {
            return ((BOOSTMULTIPLIERMAX - 1) * boostFactor) + bonusIncrease + itemIncrease;
        }

        private static double GetExperienceBoostingFactor(int collectedEpSum, double epLevelThreshold)
        {
            if (collectedEpSum < GAURANTEED_BOOST_MAX_THRESH)
            {
                return 1.0;
            }

            double linearRatio = collectedEpSum / epLevelThreshold;
            double result = 1.0 - linearRatio;
            result = result.Clamp();

            return result;
        }

        /// <summary>
        /// boostMultiplier: 0 .... BOOSTMULTIPLIERMAX     ==>  usage:  inPoint * ( 1 + mult )
        /// </summary>
        /// <param name="inExtensionPoints"></param>
        /// <param name="boostMultiplier"></param>
        /// <returns></returns>
        private static int CalculateBoostedExtensionPoint(int inExtensionPoints, double boostMultiplier)
        {
            double bp = Math.Round(Math.Ceiling(inExtensionPoints * (1 + boostMultiplier)));

            return (int)bp;
        }

        private double GetEpBonusFromEvent()
        {
            return epBonusEventService.GetBonus();
        }

        private int GetEpBonusFromSubscription(Account account)
        {
            List<System.Data.IDataRecord> dataRecord = Db.Query()
                .CommandText("opp.getExtensionSubscription")
                .SetParameter("@accountID", account.Id)
                .Execute();

            if (!dataRecord.Any())
            {
                return 0;
            }

            System.Data.IDataRecord? record = dataRecord.SingleOrDefault();
            int ord = record.GetOrdinal("multiplierBonus");

            return record.GetInt32(ord);
        }

        private void InjectExtensionPoints(Account account, int points)
        {
            Db.Query()
                .CommandText("extensionPointsInject")
                .SetParameter("@accountID", account.Id)
                .SetParameter("@points", points)
                .ExecuteNonQuery();
        }

        /// <summary>
        /// Boosts EP points based on the account's EP level
        /// Returns the boosted points
        /// </summary>
        /// <returns></returns>
        private int GetBoostedExtensionPoints(Account account, int points, double bonusIncrease, int itemIncrease)
        {
            //  >>>>>>>>>>  ami ebbol kijon az injektolhato <<<<<<<<<<<
            int dailyPointsSum = GetExtensionPointsCollected(account);
            int realEpGain = AddExtensionPointWithBoosting(points, dailyPointsSum, SERVER_DESIRED_EP_LEVEL, bonusIncrease, itemIncrease);
            if (account.IsDailyEpBoosted)
            {
                realEpGain *= 2;
            }

            return realEpGain;
        }

        /// <summary>
        /// Returns final gained EP points value after the boosting process
        /// </summary>
        /// <param name="extensionPointsToAdd"></param>
        /// <param name="accoutEpSum"></param>
        /// <param name="epLevelThreshold"></param>
        /// <param name="bonusIncrease">Any multiplier increase over the BOOSTMULTIPLIERMAX</param>
        /// <returns></returns>
        private static int AddExtensionPointWithBoosting(
            int extensionPointsToAdd,
            int accoutEpSum,
            double epLevelThreshold,
            double bonusIncrease,
            int itemIncrease)
        {
            int result = 0;
            int currentEp = accoutEpSum;

            for (int i = 0; i < extensionPointsToAdd; i++)
            {
                double expBoostingFactor = GetExperienceBoostingFactor(currentEp, epLevelThreshold);
                double boostingMultiplier = GetBoostMultiplier(expBoostingFactor, bonusIncrease, itemIncrease);
                int pointsToAdd = CalculateBoostedExtensionPoint(1, boostingMultiplier);
                result += pointsToAdd;
                currentEp += pointsToAdd;
            }

            return Math.Max(result, extensionPointsToAdd);
        }

        private void LogEpForActivity(
            Account account,
            Character character,
            EpForActivityType activityType,
            int rawPoints,
            int points,
            double boostFactor,
            int bonusMultiplier,
            int additionalMultiplier)
        {
            EpForActivityLogEvent epForActivityLogEvent = new(activityType)
            {
                Account = account,
                CharacterId = character.Id,
                RawPoints = rawPoints,
                Points = points,
                BoostFactor = boostFactor,
                BoostMultiplier = bonusMultiplier,
                EventMultiplier = additionalMultiplier,
            };

            epForActivityLogger.Log(epForActivityLogEvent);
            Logger.Info($"EP4Activity:{activityType} accountId:{account.Id} characterId:{character.Id} raw:{rawPoints} pts:{points} boostFactor:{Math.Round(boostFactor, 4)}  bonusMultiplier:{bonusMultiplier} additionalMultiplier:{additionalMultiplier}");
        }

        /// <inheritdoc/>
        public void UnlockEpAndReset(Account account, Character character)
        {
            throw new NotImplementedException();
        }
    }
}