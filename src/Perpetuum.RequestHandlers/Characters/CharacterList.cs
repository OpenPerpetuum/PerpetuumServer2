using Perpetuum.Accounting;
using Perpetuum.Data;
using Perpetuum.GenXY;
using Perpetuum.Host.Requests;
using Perpetuum.Units.DockingBases;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterList : IRequestHandler
    {
        private readonly IAccountManager _accountManager;
        private readonly DockingBaseHelper _dockingBaseHelper;

        public CharacterList(IAccountManager accountManager, DockingBaseHelper dockingBaseHelper)
        {
            _accountManager = accountManager;
            _dockingBaseHelper = dockingBaseHelper;
        }

        public void HandleRequest(IRequest request)
        {
            Dictionary<string, object> result = new();
            Dictionary<string, object> charactersDict = new();

            int count = 0;

            List<System.Data.IDataRecord> records = Db.Query().CommandText(@"select
c.characterid as characterid,
c.rootEID as rooteid,
c.moodMessage as moodmessage,
c.lastUsed as lastused,
c.creation as creation,
c.credit as credit,
c.nick as nick,
c.inUse as inuse,
c.avatar as avatar,
c.docked as docked,
c.activechassis as activechassis,
c.zoneID as zoneid,
c.baseEID as baseeid,
c.homebaseEID as homebaseeid,
c.offensivenick as offensivenick,
e.ename as currentbasename,
h.ename as homebasename
from characters c JOIN entities e on e.eid=c.baseEID 
LEFT JOIN entities h ON c.homebaseEID=h.eid 
where accountID = @accountID and active = 1").SetParameter("@accountID", request.Session.AccountId)
                .Execute();

            foreach (System.Data.IDataRecord record in records)
            {
                int characterID = record.GetValue<int>("characterid");
                bool isDocked = record.GetValue<bool>("docked");
                int? zoneId = record.GetValue<int?>("zoneid");
                long currentBaseEID = record.GetValue<long>("baseeid");
                long homeBaseEID = record.GetValue<long?>("homebaseeid") ?? 0L;
                bool offensiveNick = record.GetValue<bool>("offensivenick");
                string currentBaseName = record.GetValue<string>("currentbasename");
                string homeBaseName = record.GetValue<string>("homebasename");
                string moodMessage = record.GetValue<string>("moodmessage");
                DateTime? lastUsed = record.GetValue<DateTime?>("lastused");
                DateTime creation = record.GetValue<DateTime>("creation");
                double credit = record.GetValue<double>("credit");
                string nick = record.GetValue<string>("nick");
                bool inUse = record.GetValue<bool>("inuse");
                string avatar = record.GetValue<string>("avatar");
                long rootEid = record.GetValue<long>("rooteid");

                DockingBase currentDockingBase = _dockingBaseHelper.GetDockingBase(currentBaseEID);
                DockingBase homeDockingBase = _dockingBaseHelper.GetDockingBase(homeBaseEID);

                Dictionary<string, object> dict = new()
                {
                    {k.characterID, characterID},
                    {k.rootEID, rootEid},
                    {k.moodMessage, moodMessage},
                    {k.lastUsed, lastUsed},
                    {k.creation, creation},
                    {k.credit, (long) credit},
                    {k.nick, nick},
                    {k.inUse, inUse ? 1 : 0},
                    {k.avatar, (GenxyString) avatar},
                    {k.docked, isDocked},
                    {k.zoneID, zoneId},
                    {k.baseEID, currentBaseEID},
                    {k.homeBaseEID, homeBaseEID},
                    {k.baseName, currentBaseName},
                    {k.homeBaseName, homeBaseName},
                    {k.offensiveNick, offensiveNick},
                    {k.currentBaseZone, currentDockingBase?.Zone.Id},
                    {k.homeBaseZone, homeDockingBase?.Zone.Id},
                    {k.baseDefinition, currentDockingBase?.Definition},
                    {k.homeBaseDefinition, homeDockingBase?.Definition},
                    {k.dockingBaseInfo, currentDockingBase?.GetDockingBaseDetails()}
                };

                charactersDict.Add("c" + count++, dict);
            }

            result.Add("characters", charactersDict);

            Account account = _accountManager.Repository.Get(request.Session.AccountId);
            int ep = _accountManager.CalculateCurrentEp(account);
            result.Add("extensionPoints", ep);

            Message.Builder.FromRequest(request).WithData(result).WrapToResult().WithEmpty().Send();
        }
    }
}