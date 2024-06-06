using GalaSoft.MvvmLight;
using System;
using System.Text.RegularExpressions;

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

    public string MarkdownMessage 
    {
        get
        {
            string openingTag1 = "```markdown";
            string openingTag2 = "```";
            int length = openingTag1.Length;

            string closingTag = "```";

            int startIndex = Message.IndexOf(openingTag1);

            if (startIndex == -1)
            {
                startIndex = Message.IndexOf(openingTag2);
                length = openingTag2.Length;
            }

            if (startIndex != -1)
            {
                startIndex += length;
                int endIndex = Message.LastIndexOf(closingTag);

                if (endIndex != -1 && endIndex > startIndex)
                {
                    string markdownContent = Message.Substring(startIndex, endIndex - startIndex).Trim();
                    return markdownContent;
                }
                else
                {
                    return Message;
                }
            }
            else
            {
                return Message;
            }
        }
    }


    private bool _isExpanded = false;
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    private bool _isMarkdownRendering = false;
    public bool IsMarkdownRendering { get => _isMarkdownRendering; set => Set(ref _isMarkdownRendering, value); }

    private bool _showMarkdownToggle = false;
    public bool ShowMarkdownToggle { get => _showMarkdownToggle; set => Set(ref _showMarkdownToggle, value); }

    public MessageViewModel(ChatMessageType type) : this(type, "")
    { }

    public MessageViewModel(ChatMessageType type, string message)
    {
        Type = type;
        Message = message;
        ShowMarkdownToggle = type is ChatMessageType.Bot;
        IsMarkdownRendering = ContainsMarkdown(MarkdownMessage);
    }

    static bool ContainsMarkdown(string text)
    {
        // Regex pattern to match common Markdown elements
        string pattern = @"(^|\s)(#{1,6}\s+|\*\s+|-\s+|\d+\.\s+|\!\[.*?\]\(.*?\)|\[.*?\]\(.*?\)|```.*?```|`[^`]*`|>\s+|\|.*?\|.*?\|)";

        return Regex.IsMatch(text, pattern, RegexOptions.Multiline);
    }
}
