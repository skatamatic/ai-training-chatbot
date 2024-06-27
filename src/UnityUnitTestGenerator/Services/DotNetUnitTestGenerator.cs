using Newtonsoft.Json;
using Shared;
using System.Text.RegularExpressions;
using UnitTestGenerator.Internal;
using UnitTestGenerator.Model;

namespace UnitTestGenerator.Services;

public class DotNetUnitTestGenerator : BaseUnitTestGenerator
{
    public DotNetUnitTestGenerator(GenerationConfig config, IOpenAIAPI api) : base(config, api)
    {
    }

    public override Task<UnitTestGenerationResult> Generate(string fileToTest)
        => GenerateInternal(fileToTest, "nunit test generation bot");

    protected override string GetSupplementalSystemPrompt(string uut)
    {
        string pattern = @".*public class .*SomePattern.*";
        Match match = Regex.Match(uut, pattern);

        return string.Empty;
    }
}
