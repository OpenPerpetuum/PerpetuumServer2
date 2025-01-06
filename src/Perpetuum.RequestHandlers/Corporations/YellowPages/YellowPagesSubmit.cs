using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;
using Perpetuum.Units.DockingBases;
using Perpetuum.Zones;

namespace Perpetuum.RequestHandlers.Corporations.YellowPages
{
    public class YellowPagesSubmit : IRequestHandler
    {
        private readonly ICorporationManager _corporationManager;
        private readonly IZoneManager _zoneManager;
        private readonly DockingBaseHelper _dockingBaseHelper;

        public YellowPagesSubmit(ICorporationManager corporationManager, IZoneManager zoneManager, DockingBaseHelper dockingBaseHelper)
        {
            _corporationManager = corporationManager;
            _zoneManager = zoneManager;
            _dockingBaseHelper = dockingBaseHelper;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;

            int primaryactivity = request.Data.GetOrDefault(k.primaryActivity, -1);
            int primaryzone = request.Data.GetOrDefault(k.zoneID, -1);
            long primarybase = request.Data.GetOrDefault(k.baseEID, (long)-1);
            int orientation = request.Data.GetOrDefault(k.orientation, -1);
            int lookingfor = request.Data.GetOrDefault(k.lookingFor, -1);
            int preferredfaction = request.Data.GetOrDefault(k.preferredFaction, -1);
            int providesinsurance = request.Data.GetOrDefault(k.providesInsurance, -1);
            int timezone = request.Data.GetOrDefault(k.timeZone, -1);
            int requiredactivity = request.Data.GetOrDefault(k.requiredActivity, -1);
            int communication = request.Data.GetOrDefault(k.communication, -1);
            int services = request.Data.GetOrDefault(k.services, -1);

            List<string> updateList = new();
            Dictionary<string, object> insertDict = new();

            long corporationeid = character.CorporationEid;

            DefaultCorporationDataCache.IsCorporationDefault(corporationeid).ThrowIfTrue(ErrorCodes.CharacterMustBeInPrivateCorporation);

            CorporationRole role = Corporation.GetRoleFromSql(character);
            role.IsAnyRole(CorporationRole.CEO, CorporationRole.DeputyCEO, CorporationRole.HRManager, CorporationRole.PRManager).ThrowIfFalse(ErrorCodes.InsufficientPrivileges);

            bool update = false;

            DbQuery query = Db.Query();

            int id = query.CommandText("select id from yellowpages where corporationeid=@corporationeid")
                                  .SetParameter("@corporationeid", corporationeid)
                                  .ExecuteScalar<int>();

            if (id > 0)
            {
                update = true;
            }

            insertDict.Add("corporationeid", corporationeid);

            if (primaryactivity >= 0)
            {
                insertDict.Add("primaryactivity", primaryactivity);
                updateList.Add("primaryactivity=@primaryactivity ");
                query.SetParameter("@primaryactivity", primaryactivity);
            }

            if (primaryzone >= 0)
            {
                _zoneManager.ContainsZone(primaryzone).ThrowIfFalse(ErrorCodes.ZoneNotFound);
                insertDict.Add("zoneID", primaryzone);
                updateList.Add("zoneID = @primaryzone ");
                query.SetParameter("@primaryzone", primaryzone);
            }

            //set explicitely on the client
            if (request.Data.ContainsKey(k.zoneID) && primaryzone == -1)
            {
                updateList.Add("zoneID = NULL ");
            }

            //na ezert nem lehet pbs base-t a yellow pagesbe beallitani
            if (primarybase > 0)
            {
                DockingBase dockingBase = _dockingBaseHelper.GetDockingBase(primarybase);
                insertDict.Add("baseEID", dockingBase.Eid);
                updateList.Add("baseEID = @primarybase ");
                query.SetParameter("@primarybase", primarybase);
            }

            if (request.Data.ContainsKey(k.baseEID) && primarybase == -1)
            {
                updateList.Add("baseEID = NULL ");
            }

            if (orientation >= 0)
            {
                insertDict.Add("orientation", orientation);
                updateList.Add("orientation=@orientation ");
                query.SetParameter("@orientation", orientation);
            }

            if (lookingfor >= 0)
            {
                insertDict.Add("lookingfor", lookingfor);
                updateList.Add("lookingfor=@lookingfor ");
                query.SetParameter("@lookingfor", lookingfor);
            }

            if (preferredfaction >= 0)
            {
                insertDict.Add("preferredfaction", preferredfaction);
                updateList.Add("preferredfaction = @preferredfaction ");
                query.SetParameter("@preferredfaction", preferredfaction);
            }

            if (request.Data.ContainsKey(k.preferredFaction) && preferredfaction == -1)
            {
                updateList.Add("preferredfaction = NULL ");
            }

            if (providesinsurance >= 0)
            {
                insertDict.Add("providesinsurance", providesinsurance);
                updateList.Add("providesinsurance=@providesinsurance ");
                query.SetParameter("@providesinsurance", providesinsurance);
            }

            if (timezone >= 0)
            {
                insertDict.Add("timezone", timezone);
                updateList.Add("timezone=@timezone ");
                query.SetParameter("@timezone", timezone);
            }

            if (requiredactivity >= 0)
            {
                insertDict.Add("requiredactivity", requiredactivity);
                updateList.Add("requiredactivity=@requiredactivity ");
                query.SetParameter("@requiredactivity", requiredactivity);
            }

            if (communication >= 0)
            {
                insertDict.Add("communication", communication);
                updateList.Add("communication=@communication ");
                query.SetParameter("@communication", communication);
            }

            if (services >= 0)
            {
                insertDict.Add("services", services);
                updateList.Add("services=@services");
                query.SetParameter("@services", services);
            }

            updateList.Count.ThrowIfEqual(0, ErrorCodes.NothingDefined);

            string cmdStr;

            if (update)
            {
                cmdStr = "update yellowpages set " + updateList.ArrayToString() + " where id=@id";
                query.CommandText(cmdStr).SetParameter("@id", id);
            }
            else
            {
                cmdStr = insertDict.ToInsertString("yellowpages", "nothing");
                query.CommandText(cmdStr);
            }

            query.ExecuteNonQuery().ThrowIfNotEqual(1, ErrorCodes.SQLExecutionError);

            IDictionary<string, object> entry = _corporationManager.GetYellowPages(corporationeid);
            Dictionary<string, object> result = new()
            { { k.data, entry } };
            Message.Builder.FromRequest(request).WithData(result).Send();
            CorporationData.RemoveFromCache(corporationeid);

            scope.Complete();
        }
    }
}