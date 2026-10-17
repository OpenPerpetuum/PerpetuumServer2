using Perpetuum.Data;
using Perpetuum.Threading.Process;
using System.Transactions;

namespace Perpetuum.Services.EventServices
{
    /// <summary>
    /// Caches the database-authoritative aggregate server-wide EP bonus.
    /// All persisted timestamps are real UTC instants; game day/night presentation is unrelated.
    /// </summary>
    public class EPBonusEventService : Process
    {
        public static readonly TimeSpan MaxDuration = TimeSpan.FromDays(14);
        public const int MaxBonus = 25;

        private readonly IServerWideEpBonusRepository _repository;
        private readonly object _sync = new();
        private ServerWideEpBonusState _state = ServerWideEpBonusState.None;

        public EPBonusEventService(IServerWideEpBonusRepository repository)
        {
            _repository = repository;
        }

        public int GetBonus()
        {
            var now = DateTime.UtcNow;
            lock (_sync)
            {
                return _state.IsActive(now) ? _state.Bonus : 0;
            }
        }

        public TimeSpan GetTimeRemained()
        {
            var now = DateTime.UtcNow;
            lock (_sync)
            {
                return _state.GetRemaining(now);
            }
        }

        /// <summary>
        /// Must run inside the transaction that consumes the booster item. The repository row lock
        /// serializes concurrent activations across server processes.
        /// </summary>
        public void Activate(int bonusAmount, TimeSpan duration)
        {
            var now = DateTime.UtcNow;
            var nextState = CalculateNextState(_repository.GetForUpdate(), bonusAmount, duration, now);
            _repository.Save(nextState, now);

            if (Transaction.Current == null)
            {
                PublishCommittedState(nextState);
                return;
            }

            Transaction.Current.OnCommited(() => PublishCommittedState(nextState));
        }

        public static ServerWideEpBonusState CalculateNextState(ServerWideEpBonusState persistedState, int bonusAmount, TimeSpan duration, DateTime utcNow)
        {
            var activeState = persistedState.IsActive(utcNow) ? persistedState : ServerWideEpBonusState.None;
            var resultingBonus = activeState.Bonus + bonusAmount;
            var resultingDuration = activeState.GetRemaining(utcNow) + duration;

            (resultingBonus is >= 0 and <= MaxBonus && resultingDuration <= MaxDuration)
                .ThrowIfFalse(ErrorCodes.InputTooHigh);

            return resultingBonus == 0 || resultingDuration <= TimeSpan.Zero
                ? ServerWideEpBonusState.None
                : new ServerWideEpBonusState(resultingBonus, utcNow + resultingDuration);
        }

        public override void Start()
        {
            RestoreFromDatabase();
            base.Start();
        }

        public override void Update(TimeSpan time)
        {
            var now = DateTime.UtcNow;
            var expired = false;

            lock (_sync)
            {
                if (!_state.IsActive(now) && _state.Bonus > 0)
                {
                    _state = ServerWideEpBonusState.None;
                    expired = true;
                }
            }

            if (expired)
            {
                // The predicate prevents an older expiry pass from clearing a newer extension.
                _repository.ClearExpired(now);
            }
        }

        private void RestoreFromDatabase()
        {
            var now = DateTime.UtcNow;
            var persistedState = _repository.Get();
            if (!persistedState.IsActive(now))
            {
                _repository.ClearExpired(now);
                persistedState = ServerWideEpBonusState.None;
            }

            lock (_sync)
            {
                _state = persistedState;
            }
        }

        private void PublishCommittedState(ServerWideEpBonusState state)
        {
            lock (_sync)
            {
                // A delayed completion callback must not overwrite a later committed extension.
                if (_state.ExpiresAtUtc != null && state.ExpiresAtUtc != null && state.ExpiresAtUtc < _state.ExpiresAtUtc)
                {
                    return;
                }

                _state = state;
            }
        }
    }
}
