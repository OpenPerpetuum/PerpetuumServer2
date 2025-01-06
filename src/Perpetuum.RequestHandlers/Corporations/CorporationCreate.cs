using Perpetuum.Common.Loggers.Transaction;
using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using Perpetuum.Groups.Corporations;
using Perpetuum.Groups.Corporations.Applications;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Channels;
using System.Transactions;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationCreate : IRequestHandler
    {
        private readonly ICorporationManager _corporationManager;
        private readonly IChannelManager _channelManager;
        private readonly IEntityServices _entityServices;

        public CorporationCreate(ICorporationManager corporationManager, IChannelManager channelManager, IEntityServices entityServices)
        {
            _corporationManager = corporationManager;
            _channelManager = channelManager;
            _entityServices = entityServices;
        }

        public void HandleRequest(IRequest request)
        {
            using TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            string corpName = request.Data.GetOrDefault<string>(k.name).Trim();
            int taxRate = request.Data.GetOrDefault<int>(k.taxRate);
            Dictionary<string, object> publicProfile = request.Data.GetOrDefault<Dictionary<string, object>>(k.publicProfile);
            string nick = request.Data.GetOrDefault<string>(k.nick).Trim();


            character.IsInTraining().ThrowIfTrue(ErrorCodes.TrainingCharacterInvolved);

            nick.Length.ThrowIfGreater(6, ErrorCodes.CorporationNickTooLong);
            nick.Length.ThrowIfLess(2, ErrorCodes.CorporationNickTooShort);
            nick.AllowExtras().ThrowIfFalse(ErrorCodes.CorporationNickNotAllowedCharacters);
            string.IsNullOrEmpty(nick).ThrowIfTrue(ErrorCodes.CorporationNickNotDefined);

            corpName.Length.ThrowIfGreater(128, ErrorCodes.CorporationNameTooLong);
            corpName.Length.ThrowIfLessOrEqual(3, ErrorCodes.CorporationNameTooShort);
            corpName.AllowExtras().ThrowIfFalse(ErrorCodes.CorporationNameNotAllowedCharacters);
            string.IsNullOrEmpty(corpName).ThrowIfTrue(ErrorCodes.CorporationNameNotDefined);

            Corporation.IsNameOrNickTaken(corpName, nick).ThrowIfTrue(ErrorCodes.NameTaken);

            //get the corporation eid the submitter is a member of
            DefaultCorporation oldCorporation = character.GetCorporation().ThrowIfNotType<DefaultCorporation>(ErrorCodes.CharacterMustBeInDefaultCorporation);
            //member has role in the current corporation so he/she can't be transferred
            oldCorporation.GetMemberRole(character).ThrowIfNotEqual(CorporationRole.NotDefined, ErrorCodes.MemberHasRolesError);

            //check enabler extension
            EntityDefault privateCorp = _entityServices.Defaults.GetByName(DefinitionNames.PRIVATE_CORPORATION);

            foreach (Extension enablerExtension in privateCorp.EnablerExtensions.Keys)
            {
                int extensionLevelSummary = character.GetExtensionLevelSummaryByName(ExtensionNames.CORPORATION_FOUNDING_BASIC, ExtensionNames.CORPORATION_FOUNDING_ADVANCED, ExtensionNames.CORPORATION_FOUNDING_EXPERT);
                enablerExtension.level.ThrowIfGreater(extensionLevelSummary, ErrorCodes.ExtensionLevelMismatch);
            }

            character.SubtractFromWallet(TransactionType.corporationCreate, _corporationManager.Settings.FoundingPrice);

            CorporationDescription corporationDescription = new()
            {
                name = corpName,
                nick = nick,
                taxRate = taxRate,
                publicProfile = publicProfile,
                privateProfile = new Dictionary<string, object>(),
                isDefault = false,
                founder = character.Id
            };

            PrivateCorporation newCorporation = PrivateCorporation.Create(corporationDescription);

            CorporationRole combinedRole = _corporationManager.GetAllRoles();

            newCorporation.AddMember(character, combinedRole, oldCorporation);
            oldCorporation.RemoveMember(character);

            _channelManager.LeaveChannel(oldCorporation.ChannelName, character);
            _channelManager.CreateAndJoinChannel(ChannelType.Corporation, newCorporation.ChannelName, character);

            character.GetCorporationApplications().DeleteAll();

            //get resulting info
            IDictionary<string, object> corpInfo = newCorporation.GetInfoDictionaryForMember(character);
            Message.Builder.FromRequest(request).WithData(corpInfo).Send();

            _corporationManager.InformCorporationMemberTransferred(oldCorporation, newCorporation, character);

            Transaction.Current.OnCommited(() =>
            {
                character.GetPlayerRobotFromZone()?.UpdateCorporationOnZone(newCorporation.Eid);
            });

            scope.Complete();
        }
    }
}