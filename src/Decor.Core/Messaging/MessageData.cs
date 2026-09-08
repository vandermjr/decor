namespace Decor.Core.Messaging;

public class MessageData(MessageResult result, string content)
{
    public MessageResult Result { get; } = result;
    public string Content { get; } = content;
}
