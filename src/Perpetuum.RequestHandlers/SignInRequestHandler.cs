using System;
using Perpetuum.Accounting;
using Perpetuum.Host.Requests;
using Perpetuum.Log;
using Perpetuum.Services.Relay;
using Perpetuum.Services.Sessions;

namespace Perpetuum.RequestHandlers
{
    public abstract class SignInRequestHandler : IRequestHandler
    {
        private readonly IRelayStateService _relayStateService;
        private readonly ISessionManager _sessionManager;
        private readonly IAccountRepository _accountRepository;
        private readonly ILoginQueueService _loginQueueService;

        protected SignInRequestHandler(IRelayStateService relayStateService,ISessionManager sessionManager,IAccountRepository accountRepository,ILoginQueueService loginQueueService)
        {
            _relayStateService = relayStateService;
            _sessionManager = sessionManager;
            _accountRepository = accountRepository;
            _loginQueueService = loginQueueService;
        }

        public void HandleRequest(IRequest request)
        {
            var account = LoadAccount(request);
            if (account == null)
                throw new PerpetuumException(ErrorCodes.NoSuchUser);

            if (_relayStateService.State.Equals(RelayState.OpenForAdminsOnly) && !account.AccessLevel.IsAdminOrGm())
            {
                throw new PerpetuumException(ErrorCodes.RelayIsClosedForPublic);
            }

            // ignored in standalone
            //account.EmailConfirmed.ThrowIfFalse(ErrorCodes.EmailNotConfirmed);
            request.Session.SteamBuild = request.Data.GetOrDefault<int>(k.steambuildid);
            request.Session.ClientVersion = request.Data.GetOrDefault<string>(k.clientver);


            var isLoggedIn = account.IsLoggedIn;
            if (isLoggedIn)
            {
                var session = _sessionManager.GetByAccount(account);

                // This is the only place a ghost announces itself, and the two shapes of it need
                // different fixes. A session still held means the peer vanished without closing and
                // nothing noticed, which is the missing idle timeout. No session behind the flag
                // means the sign out ran and rolled back, leaving the row saying logged in. The one
                // line this replaced said "a logged in account was found" for both, so a live log
                // could not tell them apart.
                Logger.Info(session == null
                    ? SessionDiagnostics.DescribeStaleLogin(account.Id)
                    : SessionDiagnostics.DescribeStaleLogin(
                        account.Id,
                        session.Id,
                        session.RemoteEndPoint,
                        session.Activity.SilentFor(DateTime.Now),
                        session.Activity.LongestGap));

                session?.ForceQuit(ErrorCodes.NoSimultaneousLoginsAllowed);

                account.IsLoggedIn = false;
                _accountRepository.Update(account);
                throw new PerpetuumException(ErrorCodes.AccountHasBeenDisconnected);
            }

            //account.IsActive.ThrowIfFalse(ErrorCodes.AccountNotPurchased);

            if (account.State.HasFlag(AccountState.banned))
            {
                account.BanTime?.Add(account.BanLength).ThrowIfGreater(DateTime.Now,ErrorCodes.AccountBanned,gex => gex.SetData("banNote",account.BanNote)
                    .SetData("banTime",account.BanTime)
                    .SetData("banLength",(int)account.BanLength.TotalSeconds));

                //auto remove ban if period expired
                account.State &= AccountState.banned;
                _accountRepository.Update(account);
                Logger.Info("ban removed from account: " + account.Id + " email:" + account.Email);
            }

            var hwHash = request.Data.GetOrDefault<string>(k.hash);
            var language = request.Data.GetOrDefault<int>(k.language, 0);
            _loginQueueService.EnqueueAccount(request.Session, account.Id, hwHash, language);
        }

        protected abstract Account LoadAccount(IRequest request);
    }
}