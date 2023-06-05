using JavaJotter.Helpers;
using JavaJotter.Interfaces;
using JavaJotter.Types;

namespace JavaJotter.Services;

public class MockUsernameService : IUsernameService
{
    public async Task<List<Username>> GetAllUsers()
    {
        var names = MockDataHelper.GetUsernames();
        var ids = MockDataHelper.GetUserIds();

        return names.Select((t, i) => new Username(ids[i], t)).ToList();
    }

    public async Task<Username?> GetUsername(string id)
    {
        var names = MockDataHelper.GetUsernames();
        var ids = MockDataHelper.GetUserIds();

        var index = ids.IndexOf(id);

        return index == -1 ? null : new Username(ids[index], names[index]);
    }
}