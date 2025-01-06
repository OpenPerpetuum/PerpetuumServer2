using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Groups.Corporations.Applications;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationApply : IRequestHandler
    {
        private readonly ICorporationManager _corporationManager;

        public CorporationApply(ICorporationManager corporationManager)
        {
            _corporationManager = corporationManager;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;

            character.IsInTraining().ThrowIfTrue(ErrorCodes.TrainingCharacterInvolved);

            long targetCorporationEID = request.Data.GetOrDefault<long>(k.corporationEID);
            string motivation = request.Data.GetOrDefault<string>(k.note);

            //from private corp?
            character.GetCorporation().ThrowIfNotType<DefaultCorporation>(ErrorCodes.CharacterMustBeInDefaultCorporation);

            //check corp hopping
            _corporationManager.IsJoinAllowed(character).ThrowIfFalse(ErrorCodes.CorporationChangeTooOften);

            //is valid target corp?
            PrivateCorporation targetCorporation = PrivateCorporation.Get(targetCorporationEID).ThrowIfNull(ErrorCodes.OnlyPrivateCorporationAcceptsApplication);

            CorporationApplication[] applications = character.GetCorporationApplications().ToArray();
            applications.Length.ThrowIfGreater(5, ErrorCodes.CorporationApplicationsNumberExceeded);
            applications.Any(a => a.CorporationEID == targetCorporation.Eid).ThrowIfTrue(ErrorCodes.OneApplicationAllowedPerCorporation);

            //add appliaction
            CorporationApplication newApplication = new(targetCorporation)
            {
                Character = character,
                Motivation = motivation
            };

            newApplication.InsertToDb();

            IDictionary<string, object> result = character.GetCorporationApplications().ToDictionary();
            Message.Builder.FromRequest(request).WithData(result).Send();

            scope.Complete();
        }
    }
}