using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.EventServices;

namespace Perpetuum.RequestHandlers
{
    public class EPBonusEvent : IRequestHandler
    {
        private readonly EPBonusEventService _eventService;
        public EPBonusEvent(EPBonusEventService eventService)
        {
            _eventService = eventService;
        }

        public void HandleRequest(IRequest request)
        {
            using System.Transactions.TransactionScope scope = Db.CreateTransaction();
            int bonusAmount = request.Data.GetOrDefault<int>(k.bonus);
            int durationHours = request.Data.GetOrDefault<int>(k.duration);

            _eventService.Activate(bonusAmount, TimeSpan.FromHours(durationHours));

            scope.Complete();
        }
    }
}
