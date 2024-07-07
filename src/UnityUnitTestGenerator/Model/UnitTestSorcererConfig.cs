using UnitTestGenerator.Interface;

namespace UnitTestGenerator.Model;

public enum SorcererMode
{
    DotNet,
    Unity
}

public class UnitTestSorcererConfig
{
    public bool PresentationMode { get; set; } = false;
    public bool Beautify { get; set; } = true;
    public int MaxFixAttempts { get; set; } = 3;
    public string FileToTest { get; set; }
    public bool SkipToEnhanceIfTestsExist { get; set; } = false;
    public SorcererMode Mode { get; set; } = SorcererMode.DotNet;
    public EnhancementType[] Enhancements { get; set; } = 
    {
        EnhancementType.General, 
        EnhancementType.Coverage, 
        EnhancementType.Verify, 
        EnhancementType.Refactor, 
        EnhancementType.Coverage, 
        EnhancementType.Verify, 
        EnhancementType.Document, 
        EnhancementType.SquashBugs, 
        EnhancementType.Clean, 
        EnhancementType.Verify 
    };
}
