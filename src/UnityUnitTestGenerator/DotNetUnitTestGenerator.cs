using CSharpTools;
using CSharpTools.DefinitionAnalyzer;
using CSharpTools.ReferenceFinder;
using Newtonsoft.Json;
using Shared;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTestGenerator;

public class DotNetUnitTestGenerator : IUnitTestGenerator
{
    GenerationConfig _config;
    IOpenAIAPI _api;
    ReferenceFinderService _refFinder;
    DefinitionAnalyzerService _analyzer;

    Action<string> _output;

    public DotNetUnitTestGenerator(GenerationConfig config, IOpenAIAPI api, Action<string> output)
    {
        _config = config;
        _api = api;
        _refFinder = new ReferenceFinderService(output);
        _analyzer = new DefinitionAnalyzerService(_refFinder, output);
        _output = output;
    }

    public async Task<UnitTestGenerationResult> Generate()
    {
        var uutContent = File.ReadAllText(_config.FileToTest);
        var definitions = await _refFinder.FindDefinitions(_config.FileToTest, _config.ContextSearchDepth);
        var analysis = _analyzer.Analyze(definitions, _config.FileToTest).Result;

        string systemPrompt = BuildSystemPrompt(uutContent);
        string userPrompt = BuildUserPrompt(uutContent, BuildContext(analysis));

        _output($"Prompting OpenAI with the following prompt:\n{userPrompt}");

        _api.SystemPrompt = systemPrompt;
        string session = Guid.NewGuid().ToString();
        var response = await _api.Prompt(session, userPrompt);

        var json = ExtractJson(response);
        var testDto = JsonConvert.DeserializeObject<UnitTestAIResponse>(json);

        _output($"Got response from OpenAI:\n{response}");

        return new UnitTestGenerationResult() { Analysis = analysis, AIResponse = testDto, ChatSession = session, Config = _config };
    }

    private string BuildUserPrompt(string uutContent, string context)
    {
        string userPrompt = $@"
Here's the file to test:
-----------------
{uutContent}
-----------------

Here's all the context:
-----------------
{context}
-----------------
";

        return userPrompt;
    }

    private string BuildSystemPrompt(string uutContent)
    {
        string supplementalSystemPrompt = GetSupplementalSystemPrompt(uutContent);
        string systemPrompt = @$"
You are an nunit unit test generation bot.  
You are to take this csharp code as well as all the accompanying context and generate excellent quality and VERY COMPREHENSIVE unit tests for it.  Write as many tests as you can covering functionality and edge cases (but keep in mind your token limit of 4000 output tokens).

NEVER test any private or protected methods or properties!!!  Only public ones.  If you think you need to test a private method, you are wrong.  You need to test the public method that calls it or invoke it through events.
NEVER USE REFLECTION or any clever tricks in your tests.
Sometimes context is supplemented with mocks we use.  Be sure to use them if present!
You cannot moq optional parameters.  Instead, just do an It.IsAny<type>() when you encounter them in an interface.
Use nunit, moq, Assert.That, and Assert/Act/Arrange with comments indicating Assert/Act/Arrange poritons.  You can do Act/Assert/Act/Assert after if it makes sense to, but only do 1 arrange.
Often it will be hard to test methods directly, you will need to mock raising events to execute the code.  Make sure to do this and not use reflection.
Ensure you are using best practices and excellent code quality.  Aim for at least 9 tests for large classes if possible
Name tests like Action_WhenCondition_ExpectResult.
Use comments as appropriate.
Use [TestCases] and [Values] as appropriate to cover multiple inputs.  Favor this over multiple tests.
Do NOT try to substitute any concrete classes.  That does not work.  You will need to create real ones (eg Unity things like Transforms and Cameras).  Be sure to clean these up!
Do not ask for any permissions or responses or use any non json output.

Answer with the following json format.  Be mindful to escape it properly:
{{{{
   ""test_file_name"": ""<name of the test file>"",
   ""test_file_content"": ""<content of the test file>"",
   ""notes"": ""<any notes you want to include>""
}}}}""

{supplementalSystemPrompt}";

        return systemPrompt;
    }

    private static string BuildContext(AnalysisResult analysis)
    {
        StringBuilder context = new();
        foreach (var definition in analysis.Definitions)
        {
            context.AppendLine($"Symbol: {definition.FullName}");

            if (definition.Supplement != null)
            {
                context.AppendLine($"Supplement Object: {definition.Supplement.Definition.FullName}");
                context.AppendLine($"Reason for supplement: {definition.Supplement.ReasonForSupplementing}");
                context.AppendLine($"--(START OF SUPPLEMENT CODE)--\n{definition.Supplement.Definition.Code}\n--(END OF CODE)--");
            }

            context.AppendLine($"--(START OF DEFINITION CODE)--:\n{definition.Code}\n--(END OF CODE)--\n");
            context.AppendLine();
        }

        return context.ToString();
    }

    private string GetSupplementalSystemPrompt(string uut)
    {
        string pattern = @".*public class .*SomePattern.*";
        Match match = Regex.Match(uut, pattern);

        return string.Empty;
    }

    public static string ExtractJson(string input)
    {
        string pattern = @"\{(?:[^{}]|(?<Open>\{)|(?<-Open>\}))+(?(Open)(?!))\}";

        Match match = Regex.Match(input, pattern);

        if (match.Success)
        {
            return match.Value;
        }

        return null;
    }
}
