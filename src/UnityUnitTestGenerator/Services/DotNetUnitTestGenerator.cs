using Shared;
using Sorcerer.Model;
using System.Text.RegularExpressions;

namespace Sorcerer.Services;

public class DotNetUnitTestGenerator : BaseUnitTestGenerator
{
    public DotNetUnitTestGenerator(GenerationConfig config, IOpenAIAPI api) : base(config, api)
    {
    }

    public override Task<UnitTestGenerationResult> Generate(string fileToTest)
        => GenerateInternal(fileToTest, "nunit test generation bot", "Use file scoped single line namespaces to avoid nesting.");

    protected override string GetSupplementalSystemPrompt(string uut)
    {
        string pattern = @".*public class .*SomePattern.*";
        Match match = Regex.Match(uut, pattern);

        return string.Empty;
    }
}
