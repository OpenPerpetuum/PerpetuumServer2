using Perpetuum.Data;
using Perpetuum.Groups.Corporations;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Corporations
{
    public class CorporationDocumentCreate : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            Accounting.Characters.Character character = request.Session.Character;
            CorporationDocumentType documentType = (CorporationDocumentType)request.Data.GetOrDefault<int>(k.type);
            string body = request.Data.GetOrDefault<string>(k.body);
            int[] registered = request.Data.GetOrDefault<int[]>(k.members);
            int[] writeAccess = request.Data.GetOrDefault<int[]>(k.writeAccess);
            bool useCorporationWallet = request.Data.GetOrDefault<int>(k.useCorporationWallet) == 1;

            int[]? finalRegistered = null;
            int[]? finalWriteAccess = null;
            if (registered != null)
            {
                finalRegistered = registered.Where(d => d != character.Id).Distinct().ToArray();

                if (writeAccess != null)
                {
                    finalWriteAccess = finalRegistered.Intersect(writeAccess).Where(d => d != character.Id).ToArray();
                }

            }

            documentType.ThrowIfEqual(CorporationDocumentType.terraformProject, ErrorCodes.InvalidDocumentType);

            CorporationDocumentHelper.GetDocumentConfig(documentType, out Groups.Corporations.CorporationDocumentConfig? documentConfig).ThrowIfError();

            documentConfig.OnCreate(character, useCorporationWallet).ThrowIfError();

            DateTime? validUntil = null;
            if (documentConfig.IsRentable)
            {
                validUntil = DateTime.Now.AddDays(documentConfig.rentPeriodDays);
            }

            CorporationDocument.CreateNewToSql(character, documentType, validUntil, body, out CorporationDocument? corporationDocument).ThrowIfError();

            corporationDocument.SetRegistration(finalRegistered, finalWriteAccess);

            Dictionary<string, object> result = new()
            {
                { k.document, corporationDocument.ToDictionary() }
            };

            Message.Builder.FromRequest(request).WithData(result).Send();

            scope.Complete();
        }
    }
}