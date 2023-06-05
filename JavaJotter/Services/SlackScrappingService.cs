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


    public async Task<List<Message>> Scrape(DateTime? date)
    {
        var conversationListResponse = await _slackClient.Conversations.List();

        var messages = new List<Message>();

        foreach (var channel in conversationListResponse.Channels)
        {
            if (!channel.IsMember) continue;

            _logger.Log($"Scraping channel: {channel.Name}...");

            var messageEvents = await GetMessages(channel, date);


            foreach (var messageEvent in messageEvents)
            {
                var attachmentTexts = new string[messageEvent.Attachments.Count];
                for (var index = 0; index < messageEvent.Attachments.Count; index++)
                {
                    attachmentTexts[index] = messageEvent.Attachments[index].Text;
                }


                messages.Add(new Message(messageEvent.Channel, messageEvent.Timestamp, messageEvent.ClientMsgId,
                    messageEvent.User, messageEvent.Text, attachmentTexts));
            }
        }

        return messages;
    }

    private async Task<List<MessageEvent>> GetMessages(Conversation conversation, DateTime? oldest = null)
    {
        var oldestTs = "";

        oldestTs = oldest != null ? oldest.Value.ToTimestamp() : DateTime.Today.AddYears(-1).ToTimestamp();

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