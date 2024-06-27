namespace UnitTestGenerator.Model;

public enum SorcererMode
{
    DotNet,
    Unity
}

public class UnitTestSorcererConfig
{
    public int MaxFixAttempts { get; set; } = 3;
    public int EnhancementPasses { get; set; } = 2;
    public string FileToTest { get; set; }
    public bool SkipToEnhanceIfTestsExist { get; set; } = false;
    public SorcererMode Mode { get; set; } = SorcererMode.DotNet;
}
