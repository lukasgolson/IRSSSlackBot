using JavaJotter.Types;

namespace JavaJotter.Interfaces;

public interface IDatabaseConnection
{
    public Task InsertRoll(Roll roll);

    public Task InsertMessage(Message message);

    public Task UpdateUsername(Username username);
    public Task UpdateChannel(Channel username);

    public Task<List<Username>> GetNullUsernames();
    public Task<List<Channel>> GetNullChannels();

    public Task<Roll?> GetLatestRoll();

    public Task<Roll?> GetEarliestRoll();
}