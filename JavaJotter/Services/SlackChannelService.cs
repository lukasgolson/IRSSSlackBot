using JavaJotter.Interfaces;
using SlackNet;
using Channel = JavaJotter.Types.Channel;

namespace JavaJotter.Services;

public class SlackChannelService : IChannelService
{
    private readonly ISlackApiClient _slackClient;

    public SlackChannelService(ISlackServiceProvider slackServiceProvider)
    {
        _slackClient = slackServiceProvider.GetApiClient();
    }

    public async Task<Channel> GetChannel(string id)
    {
        var conversation = await _slackClient.Conversations.Info(id);
        return conversation is null ? null : new Channel(id, conversation.Name);
    }
}