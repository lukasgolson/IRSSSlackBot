using SlackNet;
using SlackNet.Events;
using ILogger = JavaJotter.Interfaces.ILogger;

namespace JavaJotter.EventHandlers;

internal class MessageHandler : IEventHandler<MessageEvent>
{
    private readonly ILogger _logger;
    private readonly ISlackApiClient _slack;

    public MessageHandler(ISlackApiClient slack, ILogger logger)
    {
        _slack = slack;
        _logger = logger;
    }

    public async Task Handle(MessageEvent slackEvent)
    {
        _logger.Log(slackEvent);
    }
}