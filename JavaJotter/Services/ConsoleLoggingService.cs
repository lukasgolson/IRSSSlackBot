using JavaJotter.Interfaces;

namespace JavaJotter.Services;

public class ConsoleLoggingService : ILogger
{
    public void Log(string? message, ILogger.LogSeverity logSeverity = ILogger.LogSeverity.Info)
    {
        Console.ForegroundColor = SeverityToColor(logSeverity);
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static ConsoleColor SeverityToColor(ILogger.LogSeverity logSeverity)
    {
        ConsoleColor color;
        switch (logSeverity)
        {
            case ILogger.LogSeverity.Warning:
                color = ConsoleColor.Yellow;
                break;
            case ILogger.LogSeverity.Error:
                color = ConsoleColor.Red;
                break;
            case ILogger.LogSeverity.Info:
            default:
                color = ConsoleColor.White;
                break;
        }

        return color;
    }
}