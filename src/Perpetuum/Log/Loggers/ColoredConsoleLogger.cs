namespace Perpetuum.Log.Loggers
{
    public class ColoredConsoleLogger : ConsoleLogger<LogEvent>
    {
        private readonly ConsoleColor _defaultColor = Console.ForegroundColor;
        private readonly object _lock = new object();

        public ColoredConsoleLogger(ILogEventFormatter<LogEvent, string> formatter) : base(formatter)
        {
        }

        public override void Log(LogEvent logEvent)
        {
            var message = _formatter.Format(logEvent);

            lock (_lock)
            {
                try
                {
                    switch (logEvent.LogType)
                    {
                        case LogType.Warning:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            break;
                        case LogType.Error:
                            Console.ForegroundColor = ConsoleColor.Red;
                            break;
                    }
                    Console.Out.WriteLine(message);
                }
                finally
                {
                    Console.ForegroundColor = _defaultColor;
                }
            }
        }
    }
}
