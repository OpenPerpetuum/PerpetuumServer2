using Perpetuum.Accounting;
using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.ExtensionService;

namespace Perpetuum.RequestHandlers.Extensions
{
    /// <summary>
    /// Reset character extension for account credit ... not used yet
    /// </summary>
    public class ExtensionResetCharacter : IRequestHandler
    {
        private readonly IAccountManager accountManager;

        public ExtensionResetCharacter(IAccountManager accountManager)
        {
            this.accountManager = accountManager;
        }

        public void HandleRequest(IRequest request)
        {
            using (var scope = Db.CreateTransaction())
            {
                var account = accountManager.Repository.Get(request.Session.AccountId).ThrowIfNull(ErrorCodes.AccountNotFound);

                var character = Character.Get(request.Data.GetOrDefault<int>(k.characterID));

                if (character == Character.None)
                    throw new PerpetuumException(ErrorCodes.CharacterNotFound);

                //only characters that belong to the issuers account
                if (character.AccountId != account.Id)
                {
                    throw new PerpetuumException(ErrorCodes.AccessDenied);
                }

                (character.IsDocked).ThrowIfFalse(ErrorCodes.CharacterHasToBeDocked);

                //current extensions
                var extensionCollection = character.GetExtensions();

                //default extensions
                var defaultExtensionHandler = new CharacterDefaultExtensionHelper(character);

                foreach (var extension in extensionCollection)
                {
                    //returns 0 if the extension is not starter extension
                    //returns the minimum level if the extension is starter
                    int newLevel;
                    defaultExtensionHandler.IsStartingExtension(extension, out newLevel);

                    var resultExtension = new Extension(extension.id, newLevel);

                    character.SetExtension(resultExtension);
                }

                character.DeleteAllSpentPoints();

                Message.Builder.FromRequest(request).WithOk().Send();

                scope.Complete();
            }
        }
    }
}