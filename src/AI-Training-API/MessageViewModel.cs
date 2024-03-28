using GalaSoft.MvvmLight;

namespace AI_Training_API;

public enum ChatMessageType
{
    User,
    Bot,
    Error,
    Status,
    FunctionCall,
    FunctionResult
}

public class MessageViewModel : ObservableObject
{
    public ChatMessageType Type { get; }

    private string _message = "";
    public string Message { get => _message; set => Set(ref _message, value); }

    private bool _isExpanded = false;
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }


    public MessageViewModel(ChatMessageType type)
    {
        Type = type;
    }

    public MessageViewModel(ChatMessageType type, string message)
    {
        Type = type;
        Message = message;
    }
}
