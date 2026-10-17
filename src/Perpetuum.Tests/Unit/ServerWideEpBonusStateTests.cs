using Perpetuum.Services.EventServices;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class ServerWideEpBonusStateTests
    {
        private static readonly DateTime Now = new(2049, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        [Fact]
        public void Five_plus_five_activations_stack_to_plus_twenty_five_for_one_hundred_twenty_hours()
        {
            var state = ServerWideEpBonusState.None;

            for (var i = 0; i < 5; i++)
            {
                state = EPBonusEventService.CalculateNextState(state, 5, TimeSpan.FromHours(24), Now);
            }

            Assert.Equal(25, state.Bonus);
            Assert.Equal(Now.AddHours(120), state.ExpiresAtUtc);
        }

        [Fact]
        public void Activation_adds_duration_to_remaining_time()
        {
            var existing = new ServerWideEpBonusState(20, Now.AddHours(90));

            var state = EPBonusEventService.CalculateNextState(existing, 5, TimeSpan.FromHours(24), Now);

            Assert.Equal(25, state.Bonus);
            Assert.Equal(Now.AddHours(114), state.ExpiresAtUtc);
        }

        [Fact]
        public void Expired_state_is_not_reactivated_or_extended()
        {
            var expired = new ServerWideEpBonusState(25, Now.AddTicks(-1));

            var state = EPBonusEventService.CalculateNextState(expired, 5, TimeSpan.FromHours(24), Now);

            Assert.Equal(5, state.Bonus);
            Assert.Equal(Now.AddHours(24), state.ExpiresAtUtc);
        }

        [Fact]
        public void Bonus_above_twenty_five_is_rejected()
        {
            var existing = new ServerWideEpBonusState(25, Now.AddHours(24));

            Assert.Throws<PerpetuumException>(() => EPBonusEventService.CalculateNextState(existing, 5, TimeSpan.FromHours(24), Now));
        }

        [Fact]
        public void Duration_above_fourteen_days_is_rejected()
        {
            var existing = new ServerWideEpBonusState(5, Now.AddHours(336));

            Assert.Throws<PerpetuumException>(() => EPBonusEventService.CalculateNextState(existing, 5, TimeSpan.FromHours(1), Now));
        }
    }
}
