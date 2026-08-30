using System;
using System.Threading;

namespace Perpetuum.Network
{
    /// <summary>
    /// Tracks when a connection last received data from its peer.
    /// </summary>
    /// <remarks>
    /// The OS keepalive set in <see cref="TcpConnection"/> is a two hour backstop, so a peer that
    /// disappears without closing cleanly stays connected from this side for that long. The client
    /// sends zero-length keepalive packets of its own, which arrive as data and are far more timely,
    /// but nothing recorded when they last arrived.
    ///
    /// This type records it. <see cref="LongestGap"/> is the measurement that has to come first: no
    /// idle timeout can be given a threshold until the client's real keepalive interval has been
    /// observed on a live server, and guessing it low disconnects players who are still there.
    ///
    /// "now" is passed in rather than read here so the decision is testable without sleeping.
    /// </remarks>
    public sealed class ConnectionActivity
    {
        private long _lastReceivedTicks;
        private long _longestGapTicks;

        public ConnectionActivity(DateTime now)
        {
            _lastReceivedTicks = now.Ticks;
        }

        /// <summary>
        /// The widest interval between two receives seen so far. A gap that is still open does not
        /// count — a connection whose peer vanished an hour ago would otherwise report a one hour
        /// keepalive interval and poison the measurement.
        /// </summary>
        public TimeSpan LongestGap => TimeSpan.FromTicks(Interlocked.Read(ref _longestGapTicks));

        /// <summary>
        /// Records that data arrived. Receives are serialised per connection by the
        /// BeginReceive/EndReceive chain, so this is only contended against readers.
        /// </summary>
        public void Touch(DateTime now)
        {
            long previous = Interlocked.Exchange(ref _lastReceivedTicks, now.Ticks);
            long gap = now.Ticks - previous;

            // Non-positive means the clock moved backwards or two touches raced. Neither is a gap.
            if (gap <= 0)
                return;

            long widest = Interlocked.Read(ref _longestGapTicks);
            while (gap > widest)
            {
                long actual = Interlocked.CompareExchange(ref _longestGapTicks, gap, widest);
                if (actual == widest)
                    return;

                widest = actual;
            }
        }

        public TimeSpan SilentFor(DateTime now)
        {
            long silent = now.Ticks - Interlocked.Read(ref _lastReceivedTicks);

            return silent <= 0 ? TimeSpan.Zero : TimeSpan.FromTicks(silent);
        }

        public bool IsSilentForLongerThan(TimeSpan threshold, DateTime now)
        {
            return SilentFor(now) >= threshold;
        }
    }
}
