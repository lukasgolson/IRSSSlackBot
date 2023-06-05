namespace JavaJotter.Types;

public record class Message
{
    public string Channel;
    public DateTime Timestamp;
    public Guid ClientMsgId;
    public string User;
    public string Text;
    
    public string[] AttachmentTexts;

    public Message(string channel, DateTime timestamp, Guid clientMsgId, string user, string text, string[] attachmentTexts)
    {
        Channel = channel;
        Timestamp = timestamp;
        ClientMsgId = clientMsgId;
        User = user;
        Text = text;
        AttachmentTexts = attachmentTexts;
    }
}