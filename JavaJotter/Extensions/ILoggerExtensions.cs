using JavaJotter.Interfaces;
using SlackNet.Events;

namespace JavaJotter.Extensions;

public static class LoggerExtensions
{
    public static void Log(this ILogger logger, MessageEventBase messageEvent)
    {
        var message = ConstructMessageString(messageEvent);
        logger.Log(message);
    }


    public static void LogWarning(this ILogger logger, MessageEventBase messageEvent)
    {
        var message = ConstructMessageString(messageEvent);
        logger.LogWarning(message);
    }

    public static void LogError(this ILogger logger, MessageEventBase messageEvent)
    {
        var message = ConstructMessageString(messageEvent);
        logger.LogError(message);
    }

    private static string ConstructMessageString(MessageEventBase messageEvent)
    {
        var message = messageEvent.Attachments?.FirstOrDefault()?.Text ?? messageEvent.Text;

        string user;
        if (messageEvent.User != null && !string.IsNullOrEmpty(messageEvent.User))
            user = $"[{messageEvent.User}]";
        else
            user = "[system]";

        return $"{messageEvent.Timestamp} {user}: {message}";
    }
}