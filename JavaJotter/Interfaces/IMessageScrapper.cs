using JavaJotter.Types;
using SlackNet.Events;

namespace JavaJotter.Interfaces;

public interface IMessageScrapper
{
    public IAsyncEnumerable<Message> Scrape(DateTime? oldestMessageDate, DateTime? latestMessageDate);
}