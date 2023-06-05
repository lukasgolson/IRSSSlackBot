namespace JavaJotter.Interfaces;

public interface ILogger
{
    public enum LogSeverity
    {
        Info,
        Warning,
        Error
    }

    public void Log(string message, LogSeverity logSeverity = LogSeverity.Info);

    public void Log(object message, LogSeverity logSeverity = LogSeverity.Info)
    {
        Log(message.ToString() ?? message.GetType().ToString(), logSeverity);
    }

    public void LogWarning(string message)
    {
        Log(message, LogSeverity.Warning);
    }

    public void LogWarning(object message)
    {
        Log(message, LogSeverity.Warning);
    }

    public void LogError(string message)
    {
        Log(message, LogSeverity.Error);
    }

    public void LogError(object message)
    {
        Log(message, LogSeverity.Error);
    }
}