using Newtonsoft.Json;

namespace UnitTestGenerator.Model;

public class UnitTestAIFix
{
    [JsonProperty("test_name")]
    public string TestName { get; set; }

    [JsonProperty("can_fix")]
    public bool CanFix { get; set; } = true;

    [JsonProperty("reason")]
    public string Reason { get; set; }

    [JsonProperty("fix")]
    public string Fix { get; set; }
}
