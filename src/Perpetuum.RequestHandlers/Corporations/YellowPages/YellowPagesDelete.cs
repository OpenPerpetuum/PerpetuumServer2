using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations.YellowPages
{
    public class YellowPagesDelete : IRequestHandler
    {
        private readonly ICorporationManager _corporationManager;

        public YellowPagesDelete(ICorporationManager corporationManager)
        {
            _corporationManager = corporationManager;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;

            long corporationeid = character.CorporationEid;
            DefaultCorporationDataCache.IsCorporationDefault(corporationeid).ThrowIfTrue(ErrorCodes.CharacterMustBeInPrivateCorporation);

            CorporationRole role = Corporation.GetRoleFromSql(character);
            role.IsAnyRole(CorporationRole.CEO, CorporationRole.DeputyCEO, CorporationRole.HRManager, CorporationRole.PRManager).ThrowIfFalse(ErrorCodes.InsufficientPrivileges);

            //do the work
            _corporationManager.DeleteYellowPages(corporationeid);

            IDictionary<string, object> entry = _corporationManager.GetYellowPages(corporationeid);
            Dictionary<string, object> result = new()
            { { k.data, entry } };
            Message.Builder.FromRequest(request).WithData(result).Send();
            CorporationData.RemoveFromCache(corporationeid);

            scope.Complete();
        }
    }
}