using Rystem.OpenAi.Chat;

namespace OpenAIAPI_Rystem;

public class ChatSession
{
    private readonly List<ChatMessage> _messages = new();

    public void AddUserPrompt(string prompt)
    {
        var message = new ChatMessage()
        {
            Content = prompt,
            Role = ChatRole.User
        };

        _messages.Add(message);
    }

    public void AddBotResult(string result)
    {
        var message = new ChatMessage()
        {
            Content = result,
            Role = ChatRole.Assistant
        };

        _messages.Add(message);
    }

    public void AddFunctionResult(string result)
    {
        var message = new ChatMessage()
        {
            Content = result,
            Role = ChatRole.Function
        };

        _messages.Add(message);
    }

    public ChatRequestBuilder BuildRequest(IOpenAiChat chat)
    {
        return chat.Request(_messages.ToArray());
    }
}