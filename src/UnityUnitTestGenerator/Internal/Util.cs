using System.Text.RegularExpressions;

namespace UnitTestGenerator.Internal;

internal static class Util
{
    public static string ExtractJsonFromCompletion(string completion)
    {
        string pattern = @"\{(?:[^{}]|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!))\}";

        Match match = Regex.Match(completion, pattern);

        if (match.Success)
        {
            return match.Value;
        }

        return null;
    }
}
