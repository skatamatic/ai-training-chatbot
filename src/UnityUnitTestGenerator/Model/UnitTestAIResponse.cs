using Newtonsoft.Json;

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
}
