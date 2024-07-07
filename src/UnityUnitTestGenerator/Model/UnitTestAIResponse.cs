using Newtonsoft.Json;
using System.Text;

namespace UnitTestGenerator.Model;

public class UnitTestAIResponse
{
    [JsonProperty("test_file_name")]
    public string TestFileName { get; set; }

    [JsonProperty("test_file_content")]
    public string TestFileContent { get; set; }

    [JsonProperty("notes")]
    public string Notes { get; set; }

    [JsonProperty("general_fix")]
    public string GeneralFix { get; set; }

    [JsonProperty("test_fixes")]
    public UnitTestAIFix[] TestFixes { get; set; } = Array.Empty<UnitTestAIFix>();

    [JsonProperty("improvements")]
    public string[] Improvements { get; set; } = Array.Empty<string>();

    public string ToDisplayText()
    {
        StringBuilder sb = new();

        if (!string.IsNullOrEmpty(TestFileName))
        {
            sb.AppendLine("File Name: " + TestFileName);
        }

        if (!string.IsNullOrEmpty(TestFileContent))
        {
            sb.AppendLine("---START OF TEST CODE---\n" + TestFileContent + "\n---END OF TEST CODE---");
        }

        if (!string.IsNullOrEmpty(Notes))
        {
            sb.AppendLine("Notes: " + Notes);
        }

        if (!string.IsNullOrEmpty(GeneralFix))
        {
            sb.AppendLine("General Fix: " + GeneralFix);
        }

        if (Improvements != null && Improvements.Length > 0)
        {
            sb.AppendLine("Improvements: - " + Improvements.First());

            if (Improvements.Length > 1)
            {
                foreach (var improvement in Improvements.Skip(1))
                {
                    sb.AppendLine("              - " + improvement);
                }
            }
        }

        if (TestFixes != null && TestFixes.Length > 0)
        {
            sb.AppendLine("Test Fixes:");

            foreach (var fix in TestFixes)
            {
                sb.AppendLine("  Test Name: " + fix.TestName);
                sb.AppendLine("    Reason for failure: " + fix.Reason);
                sb.AppendLine("    Can fix: " + fix.CanFix.ToString());
                if (!string.IsNullOrEmpty(fix.Fix))
                {
                    sb.AppendLine("    Fix: " + fix.Fix);
                }
            }
        }

        return sb.ToString();
    }
}
