using JavaJotter.Interfaces;
using JavaJotter.Types;
using SlackNet;
using ILogger = JavaJotter.Interfaces.ILogger;

namespace JavaJotter.Services;

public class SlackUsernameService : IUsernameService
{
    private readonly ILogger _logger;
    private readonly ISlackApiClient _slackClient;

    public SlackUsernameService(ISlackServiceProvider slackServiceProvider, ILogger logger)
    {
        _slackClient = slackServiceProvider.GetApiClient();
        _logger = logger;
    }

    public async Task<List<Username>> GetAllUsers()
    {
        var userListResponse = await _slackClient.Users.List();

        var members = userListResponse.Members;

        _logger.Log($"Found {members.Count} users");

        return members.Select(member
            => new Username(member.Id, member.Name)).ToList();
    }

    public async Task<Username?> GetUsername(string id)
    {
        var user = await _slackClient.Users.Info(id);
        return user is null ? null : new Username(user.Id, user.Name);
    }
}