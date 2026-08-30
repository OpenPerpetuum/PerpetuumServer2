using Perpetuum.Log;

namespace Perpetuum.Tests.Fakes
{
    /// <summary>
    /// Captures every log event so tests can assert on what the code under test reported.
    /// Thread-safe: production code logs from timer and task threads.
    /// </summary>
    public sealed class RecordingLogger : ILogger<LogEvent>
    {
        private readonly List<LogEvent> _events = [];
        private readonly object _gate = new();

        public void Log(LogEvent logEvent)
        {
            lock (_gate)
            {
                _events.Add(logEvent);
            }
        }

        public IReadOnlyList<LogEvent> Events
        {
            get { lock (_gate) { return [.. _events]; } }
        }

        // LogEvent names the property ThrownException, not Exception — see
        // src/Perpetuum/Log/LogEvent.cs:20. Logger.Exception(ex) sets LogType.Error and
        // ThrownException together (Logger.cs:62-71), so either condition alone would do; both
        // are kept so a future logging path that sets one without the other is excluded.
        // Logger.Error(string) is exactly such a path: it writes LogType.Error with no
        // exception, and this filter excludes it. No test relies on that today.
        public IReadOnlyList<LogEvent> Exceptions
        {
            get { lock (_gate) { return [.. _events.Where(e => e.LogType == LogType.Error && e.ThrownException != null)]; } }
        }

        public void Clear()
        {
            lock (_gate) { _events.Clear(); }
        }
    }
}
