using Perpetuum.Accounting;
using Perpetuum.Host.Requests;
using Perpetuum.Services.Relay;

namespace Perpetuum.RequestHandlers.AdminTools
{
    public class AccountOpenCreate : IRequestHandler
    {
        private readonly IAccountRepository _accountRepository;
        private readonly IServerInfoManager _serverInfoManager;

        public AccountOpenCreate(IAccountRepository accountRepository, IServerInfoManager serverInfoManager)
        {
            _accountRepository = accountRepository;
            _serverInfoManager = serverInfoManager;
        }

        public void HandleRequest(IRequest request)
        {
            string email = request.Data.GetOrDefault<string>(k.email);
            string password = request.Data.GetOrDefault<string>(k.password);

            //is the server open?
            ServerInfo si = _serverInfoManager.GetServerInfo();
            if (!si.IsOpen)
            {
                throw new PerpetuumException(ErrorCodes.InviteOnlyServer);
            }

            // if an account was already created using this session, reject this creation attempt.
            if (request.Session.AccountCreatedInSession)
            {
                throw new PerpetuumException(ErrorCodes.MaxIterationsExceeded);
            }

            //If email is not a well-formed email, reject this creation attempt
            try
            {
                System.Net.Mail.MailAddress addr = new(email);
            }
            catch
            {
                //TODO: Client Fixme - Client needs good error code for bad-email input
                throw new PerpetuumException(ErrorCodes.InvalidAttribute);
            }

            Account account = new()
            {
                Email = email,
                Password = password,
                AccessLevel = AccessLevel.normal,
                CampaignId = "{\"host\":\"opencreate\"}"
            };

            //If email exists - throw error
            if (_accountRepository.Get(account.Email, account.Password) != null)
            {
                Message.Builder.FromRequest(request).WithError(ErrorCodes.AccountAlreadyExists).Send();
                return;
            }

            _accountRepository.Insert(account);

            // if we get this far, make sure we can't sit here and make accounts.
            request.Session.AccountCreatedInSession = true;

            Message.Builder.FromRequest(request).SetData(k.account, account.ToDictionary()).Send();
        }
    }
}
