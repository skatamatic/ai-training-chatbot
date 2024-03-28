using System.Windows.Controls;
using System.Windows;

namespace AI_Training_API;

public class MessageTypeTemplateSelector : DataTemplateSelector
{
    public DataTemplate DefaultMessageTemplate { get; set; }
    public DataTemplate FunctionResultMessageTemplate { get; set; }

    public override DataTemplate SelectTemplate(object item, DependencyObject container)
    {
        if (item is MessageViewModel message)
        {
            return message.Type == ChatMessageType.FunctionResult ? FunctionResultMessageTemplate : DefaultMessageTemplate;
        }
        return DefaultMessageTemplate;
    }
}
