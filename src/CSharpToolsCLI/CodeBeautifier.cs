using Spectre.Console;
using System.Text.RegularExpressions;

public class SyntaxHighlighter
{
    private static readonly (string Pattern, string Color)[] Patterns = new[]
    {
        // Comments
        (@"//.*", "darkgreen"),
        // Strings
        (@"""([^""\\]|\\.)*""", "darkorange3"),
        // Attributes
        (@"(\[\[.*?\]\])", "springgreen4"),
        // Interfaces
        (@"\b(I[A-Z]\w*)\b", "lightgoldenrod2"),
        // Keywords
        (@"\b(public|private|protected|internal|static|void|class|struct|enum|interface|event|delegate|abstract|async|await|namespace|using|partial|readonly|volatile|extern|unsafe|override|virtual|sealed|const|new|get|set|this)\b", "dodgerblue3"),
        // Primitive types
        (@"\b(int|long|double|float|decimal|bool|char|byte|sbyte|short|ushort|uint|ulong|object|string)\b", "dodgerblue3"),
        // Control statements
        (@"\b(if|else|for|foreach|while|do|switch|case|default|break|continue|return|throw|try|catch|finally|goto|new|yield|var)\b", "mediumpurple2"),
        // Constants
        (@"\b(true|false|null)\b", "blue1"),
        // Object type names (capitalized words not matched by other patterns and not followed by an opening parenthesis, not part of a declaration or method call)
        (@"\b[A-Z][a-zA-Z0-9]*(?=\s+[a-zA-Z_])\b", "springgreen4"),
        // Generic types and nullable types
        (@"\b[A-Z][a-zA-Z0-9]*(?=\s*[\?<>])", "springgreen4")
    };

    public static void HighlightAndPrint(string code)
    {
        var lines = code.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        foreach (var line in lines.Select(x=>EscapeMarkup(x)))
        {
            var highlightedLine = HighlightLine(line);
            try
            {
                AnsiConsole.MarkupLine(highlightedLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine(line);
            }
        }
    }

    private static string HighlightLine(string line)
    {
        //Comment only lines ignore other formatting
        if (line.Trim().StartsWith("//"))
        {
            return HighlightComments(line);
        }

        // First, highlight comments to ensure other patterns do not affect them
        line = HighlightComments(line);

        // Then, apply all other patterns
        foreach (var (pattern, color) in Patterns)
        {
            line = Regex.Replace(line, pattern, $"[{color}]$0[/]", RegexOptions.Compiled);
        }
        return line;
    }

    private static string HighlightComments(string line)
    {
        return Regex.Replace(line, @"//.*", "[grey]$0[/]", RegexOptions.Compiled);
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }
}