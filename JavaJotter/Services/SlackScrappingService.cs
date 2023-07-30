using JavaJotter.Interfaces;
using JavaJotter.Types;
using SlackNet;
using SlackNet.Events;
using ILogger = JavaJotter.Interfaces.ILogger;

namespace JavaJotter.Services;

public class SlackScrappingService : IMessageScrapper
{
    private readonly ILogger _logger;
    private readonly ISlackApiClient _slackClient;

    public SlackScrappingService(ISlackServiceProvider slackServiceProvider, ILogger logger)
    {
        _slackClient = slackServiceProvider.GetApiClient();
        _logger = logger;
    }


    public async IAsyncEnumerable<Message> Scrape(DateTime? oldestMessageDate)
    {
        var conversationListResponse = await _slackClient.Conversations.List();


        foreach (var channel in conversationListResponse.Channels)
        {
            if (!channel.IsMember) continue;

            _logger.Log($"Scraping channel: {channel.Name}...");

            var messageEvents = await GetMessages(channel, oldestMessageDate);


            foreach (var messageEvent in messageEvents)
            {
                var attachmentTexts = new string[messageEvent.Attachments.Count];
                for (var index = 0; index < messageEvent.Attachments.Count; index++)
                {
                    attachmentTexts[index] = messageEvent.Attachments[index].Text;
                }


                yield return new Message(messageEvent.Channel, messageEvent.Timestamp, messageEvent.ClientMsgId,
                    messageEvent.User, messageEvent.Text, attachmentTexts);
            }
        }
    }

    private async Task<List<MessageEvent>> GetMessages(Conversation conversation, DateTime? oldestMessage = null)
    {
        var oldestTs = "";

        oldestTs = oldestMessage != null
            ? oldestMessage.Value.ToTimestamp()
            : DateTime.Today.AddYears(-1).ToTimestamp();

        var messageEvents = new List<MessageEvent>();

        var latestTs = "";
        var hasMore = true;

        while (hasMore)
        {
            var history = await _slackClient.Conversations.History(conversation.Id, latestTs, oldestTs);
            messageEvents.AddRange(history.Messages);
            latestTs = history.Messages.LastOrDefault()?.Ts;
            hasMore = history.HasMore;
        }


        foreach (var message in messageEvents)
            message.Channel = conversation.Id;
        _logger.Log($"Scraped {messageEvents.Count} messages from {conversation.Name}.");
        return messageEvents;
    }
}