using JavaJotter.Types;
using SlackNet.Events;

namespace JavaJotter.Interfaces;

public interface IMessageScrapper
{
    public Task<List<Message>> Scrape(DateTime? date);


    public Task<List<Message>> Scrape()
    {
        return Scrape(null);
    }
}