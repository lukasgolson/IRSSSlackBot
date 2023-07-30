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


        return members.Select(member => Username(member.Id, member.Profile.FirstName, member.Profile.LastName))
            .ToList();
    }

    public async Task<Username?> GetUsername(string id)
    {
        var user = await _slackClient.Users.Info(id);
        var firstName = user.Profile.FirstName;
        var lastName = user.Profile.LastName;

        return Username(user.Id, firstName, lastName);
    }

    private static Username Username(string id, string firstName, string lastName, int maxLength = 7)
    {
        var lengthPerName = (maxLength - 1) / 2;

        var shortFirstName = firstName.Length > lengthPerName ? firstName[..lengthPerName] : firstName;
        var shortLastName = lastName.Length > lengthPerName ? lastName[..lengthPerName] : lastName;

        var username = shortFirstName;

        if (!string.IsNullOrEmpty(lastName))
        {
            username += $" {shortLastName}";
        }
        else
        {
            // When the last name is not available, take as many characters as allowed from the first name
            username = firstName.Length > maxLength ? firstName[..maxLength] : firstName;
        }

        return new Username(id, username);
    }


}