using JavaJotter.Types;

namespace JavaJotter.Interfaces;

public interface IChannelService
{
    public Task<Channel?> GetChannel(string id);
}