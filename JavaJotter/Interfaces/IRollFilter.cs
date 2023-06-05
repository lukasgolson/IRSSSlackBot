using JavaJotter.Types;
using SlackNet.Events;

namespace JavaJotter.Interfaces;

public interface IRollFilter
{
    public Roll? ProcessMessage(Message messageEvent);
}