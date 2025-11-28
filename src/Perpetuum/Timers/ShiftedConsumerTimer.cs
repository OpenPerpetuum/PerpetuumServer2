using System;

namespace Perpetuum.Timers
{
    /// <summary>
    /// Shifted consumer timer class
    /// </summary>
    public class ShiftedConsumerTimer
    {
        private readonly TimeSpan _interval;
        private TimeSpan _elapsed;

        /// <summary>
        /// Timer interval
        /// </summary>
        public TimeSpan Interval => _interval;
        
        /// <summary>
        /// Time accumulator
        /// </summary>
        public TimeSpan Elapsed => _elapsed;

        public ShiftedConsumerTimer(int interval) : this(TimeSpan.FromMilliseconds(interval))
        {
            
        }

        public ShiftedConsumerTimer(TimeSpan interval)
        {
            _interval = interval;
            // Random start shift
            _elapsed = TimeSpan.FromMilliseconds(FastRandom.NextDouble()*interval.TotalMilliseconds);
        }

        /// <summary>
        /// Checking that the specified time has passed.
        /// </summary>
        public bool Passed
        {
            get { return _elapsed >= _interval; }
        }

        /// <summary>
        /// Reset the countdown to a new random value.
        /// </summary>
        public void Reset()
        {
            _elapsed = TimeSpan.FromMilliseconds(FastRandom.NextDouble() * Interval.TotalMilliseconds);
        }

        /// <summary>
        /// We are adding a new portion of time to the accumulator.
        /// </summary>
        /// <param name="time">Portion of time</param>
        /// <returns>Return ourselves for the chain</returns>
        public ShiftedConsumerTimer Update(TimeSpan time)
        {
            _elapsed += time;
            return this;
        }

        /// <summary>
        /// If time has passed, perform the action.
        /// </summary>
        /// <param name="action">Action to perfom.</param>
        public void IsPassed(Action<TimeSpan> action)
        {
            if (!Passed)
                return;

            try
            {
                action(_interval);
            }
            finally
            {
                _elapsed -= _interval;
            }
        }
    }
}
