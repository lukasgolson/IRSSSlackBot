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


        return members.Select(member => new Username(member.Id, CreateUsername(member.Profile.FirstName, member.Profile.LastName, member.Profile.DisplayName)))
            .ToList();
    }

    public async Task<Username?> GetUsername(string id)
    {
        var user = await _slackClient.Users.Info(id);
        var firstName = user.Profile.FirstName;
        var lastName = user.Profile.LastName;

        var nickname = user.Profile.DisplayName;

        return new Username(id,CreateUsername(firstName, lastName, nickname));
    }

    private static string CreateUsername(string firstName, string lastName, string displayName, int maxLength = 7)
    {
        var lengthPerName = (maxLength - 1) / 2;

        var shortFirstName = firstName.Length > lengthPerName ? firstName[..lengthPerName] : firstName;
        var shortLastName = lastName.Length > lengthPerName ? lastName[..lengthPerName] : lastName;

        string username;

        if (!string.IsNullOrEmpty(lastName))
        {
            username = $"{shortFirstName} {shortLastName}";
        }
        else
        {
            // When the last name is not available, take as many characters as allowed from the first name
            username = displayName.Length > maxLength ? displayName[..maxLength] : displayName;
        }

        return username;
    }


}