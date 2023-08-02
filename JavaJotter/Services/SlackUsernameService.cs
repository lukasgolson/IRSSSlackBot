﻿using System.Globalization;
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


        return members.Select(member => new Username(member.Id,
                CreateUsername(member)))
            .ToList();
    }

    public async Task<Username?> GetUsername(string id)
    {
        var user = await _slackClient.Users.Info(id);

        return new Username(id, CreateUsername(user));
    }

    private static string CreateUsername(User user, int maxLength = 7)
    {
        var lengthPerName = maxLength / 2;
        string username;

        if (!string.IsNullOrEmpty(user.Profile.FirstName) && !string.IsNullOrEmpty(user.Profile.LastName))
        {
            var firstName = user.Profile.FirstName.Length > lengthPerName
                ? user.Profile.FirstName[..lengthPerName]
                : user.Profile.FirstName;
            var lastName = user.Profile.LastName.Length > lengthPerName
                ? user.Profile.LastName[..lengthPerName]
                : user.Profile.LastName;

            username = $"{firstName} {lastName}";
        }
        else if (!string.IsNullOrEmpty(user.Profile.DisplayName))
        {
            username = user.Profile.DisplayName.Length > maxLength
                ? user.Profile.DisplayName[..maxLength]
                : user.Profile.DisplayName;
        }
        else
        {
            username = user.Id.Length > maxLength ? user.Id[..maxLength] : user.Id;
        }

        if (username.Length > maxLength)
        {
            username = username[..maxLength];
        }

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(username.ToLower());
    }
}