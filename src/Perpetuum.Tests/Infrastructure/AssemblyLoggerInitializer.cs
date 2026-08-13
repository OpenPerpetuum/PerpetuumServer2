using Perpetuum.Log;
using Perpetuum.Tests.Fakes;

namespace Perpetuum.Tests.Infrastructure
{
    /// <summary>
    /// Logger.Current is declared as { private get; set; }, so its previous value cannot be
    /// read and therefore cannot be restored. It is assigned once for the whole assembly.
    /// </summary>
    public static class AssemblyLoggerInitializer
    {
        public static RecordingLogger Instance { get; } = new RecordingLogger();

        private static bool _installed;
        private static readonly object Gate = new();

        public static void Install()
        {
            lock (Gate)
            {
                if (_installed) return;
                Logger.Current = Instance;
                _installed = true;
            }
        }
    }
}
