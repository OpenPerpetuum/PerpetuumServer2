using System;
using Perpetuum.Network;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    // ConnectionActivity is the measurement half of ISSUE-041. It answers two questions about a
    // connection: how long it has been silent, and what the longest silence was over its lifetime.
    // The second is the one that matters first — the client sends zero-length keepalive packets and
    // nobody here knows their interval, so an idle timeout cannot be given a threshold until the
    // real interval has been observed on a live server.
    //
    // Every test injects "now" rather than sleeping. A test that sleeps to observe a timeout is
    // slow and flaky, and this type exists precisely so that the time-dependent decision is
    // separable from the socket.
    public class ConnectionActivityTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void A_new_connection_has_not_been_silent()
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            Assert.Equal(TimeSpan.Zero, activity.SilentFor(T0));
        }

        [Fact]
        public void Silence_is_measured_from_the_last_received_data()
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            Assert.Equal(TimeSpan.FromSeconds(30), activity.SilentFor(T0.AddSeconds(30)));
        }

        [Fact]
        public void Receiving_data_resets_the_silence()
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            activity.Touch(T0.AddSeconds(25));

            Assert.Equal(TimeSpan.FromSeconds(5), activity.SilentFor(T0.AddSeconds(30)));
        }

        [Fact]
        public void A_connection_that_never_received_anything_reports_no_longest_gap()
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            Assert.Equal(TimeSpan.Zero, activity.LongestGap);
        }

        [Fact]
        public void The_longest_gap_is_the_widest_interval_between_two_receives()
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            activity.Touch(T0.AddSeconds(10));
            activity.Touch(T0.AddSeconds(55));
            activity.Touch(T0.AddSeconds(60));

            Assert.Equal(TimeSpan.FromSeconds(45), activity.LongestGap);
        }

        [Fact]
        public void The_longest_gap_survives_shorter_gaps_that_follow_it()
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            activity.Touch(T0.AddSeconds(40));
            activity.Touch(T0.AddSeconds(41));
            activity.Touch(T0.AddSeconds(42));

            Assert.Equal(TimeSpan.FromSeconds(40), activity.LongestGap);
        }

        // The gap that is still open is deliberately not counted. A connection that dropped an hour
        // ago would otherwise report a one-hour keepalive interval and poison the measurement this
        // type exists to collect.
        [Fact]
        public void The_gap_still_in_progress_does_not_count_towards_the_longest()
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            activity.Touch(T0.AddSeconds(5));
            _ = activity.SilentFor(T0.AddHours(2));

            Assert.Equal(TimeSpan.FromSeconds(5), activity.LongestGap);
        }

        [Theory]
        [InlineData(29, 30, false)]
        [InlineData(30, 30, true)]
        [InlineData(31, 30, true)]
        public void Silence_is_reported_against_a_threshold(int silentSeconds, int thresholdSeconds, bool expected)
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            bool actual = activity.IsSilentForLongerThan(
                TimeSpan.FromSeconds(thresholdSeconds),
                T0.AddSeconds(silentSeconds));

            Assert.Equal(expected, actual);
        }

        // Touches arrive on socket IO threads while a sweep would read from another. This does not
        // prove thread safety — no test does — but a lost update under contention shows up here.
        [Fact]
        public void Concurrent_receives_do_not_lose_the_longest_gap()
        {
            ConnectionActivity activity = new ConnectionActivity(T0);

            System.Threading.Tasks.Parallel.For(0, 1000, i =>
            {
                activity.Touch(T0.AddSeconds(i + 1));
                _ = activity.LongestGap;
            });

            Assert.True(activity.LongestGap > TimeSpan.Zero);
        }
    }
}
