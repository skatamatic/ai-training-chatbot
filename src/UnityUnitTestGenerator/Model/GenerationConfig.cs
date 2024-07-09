namespace Sorcerer.Model;

public class GenerationConfig
{
    public int IssueContextLineCount { get; set; } = 8;
    public int ContextSearchDepth { get; set; }
    public string StylePrompt { get; set; }
}
