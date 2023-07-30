﻿using JavaJotter.Interfaces;
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


    public async IAsyncEnumerable<Message> Scrape(DateTime? oldestMessageDate, DateTime? latestMessageDate)
    {
        var conversationListResponse = await _slackClient.Conversations.List();


        foreach (var channel in conversationListResponse.Channels)
        {
            if (!channel.IsMember) continue;

            _logger.Log($"Scraping channel: {channel.Name}...");

            await foreach (var messageEvent in GetMessages(channel, oldestMessageDate))
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

    private async IAsyncEnumerable<MessageEvent> GetMessages(Conversation conversation, DateTime? oldestMessage = null, DateTime? latestMessage = null)
    {
        var latestTs = DateTime.Now.ToTimestamp();

        var oldestTs = oldestMessage == null
            ? DateTime.Now.AddYears(-1).ToTimestamp()
            : oldestMessage.Value.ToTimestamp();

        IList<MessageEvent> historyMessages;

        do
        {
            var history = await _slackClient.Conversations.History(conversation.Id, latestTs, oldestTs);

            historyMessages = history.Messages;

            foreach (var messageEvent in historyMessages)
            {
                messageEvent.Channel = conversation.Id;
                yield return messageEvent;
            }

            if (historyMessages.Any())
            {
                latestTs = historyMessages.Last().Ts;
            }
        } while (historyMessages.Any());
    }
}