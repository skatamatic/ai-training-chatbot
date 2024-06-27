using CSharpTools.DefinitionAnalyzer;
using CSharpTools.ReferenceFinder;
using Newtonsoft.Json;
using Shared;
using System.Text;
using UnitTestGenerator.Interface;
using UnitTestGenerator.Internal;
using UnitTestGenerator.Model;

namespace UnitTestGenerator.Services;

public abstract class BaseUnitTestGenerator : IUnitTestGenerator, IOutputter
{
    protected GenerationConfig _config;
    protected IOpenAIAPI _api;
    protected ReferenceFinderService _refFinder;
    protected DefinitionAnalyzerService _analyzer;

    public event EventHandler<string> OnOutput;

    public BaseUnitTestGenerator(GenerationConfig config, IOpenAIAPI api)
    {
        _config = config;
        _api = api;
        _refFinder = new ReferenceFinderService(x => OnOutput?.Invoke(this, x));
        _analyzer = new DefinitionAnalyzerService(_refFinder, x => OnOutput?.Invoke(this, x));
    }

    protected void EmitOutput(string message) => OnOutput?.Invoke(this, message);

    public abstract Task<UnitTestGenerationResult> Generate(string fileToTest);

    public async Task<UnitTestGenerationResult> AnalyzeOnly(string fileToTest)
    {
        var uutContent = File.ReadAllText(fileToTest);
        var definitions = await _refFinder.FindDefinitions(fileToTest, _config.ContextSearchDepth);
        var analysis = _analyzer.Analyze(definitions, fileToTest).Result;

        var fakeAiResponse = new UnitTestAIResponse()
        {
            TestFileName = Path.GetFileName(fileToTest),
            TestFileContent = File.ReadAllText(fileToTest)
        };

        return new UnitTestGenerationResult() { AIResponse = fakeAiResponse, Analysis = analysis, ChatSession = Guid.NewGuid().ToString() };
    }

    protected string BuildUserPrompt(string uutContent, string context)
    {
        return $@"
Here's the file to test:
-----------------
{uutContent}
-----------------

Here's all the context:
-----------------
{context}
-----------------
";
    }

    protected string BuildSystemPrompt(string uutContent, string role) => BuildGenerationPrompt(
        role: role,
        language: "c#",
        supplemental: GetSupplementalSystemPrompt(uutContent));

    private string BuildGenerationPrompt(string role, string language = "c#", string additionalGuidelines = "", string supplemental = "")
    {
        string prompt = @$"
You are a {role}.  
You are to take this {language} code as well as all the accompanying context and generate excellent quality and VERY COMPREHENSIVE unit tests for it.  Write as many tests as you can covering functionality and edge cases.

{_config.StylePrompt}
Make sure the namespace for the test precisely matches that of the unit under test's.  Use modern single line namespaces to avoid nesting the whole class.

{CommonPrompts.CommonTestGuidelines}
{additionalGuidelines}

Answer with the following json format.  Be mindful to escape it properly:
{{{{
   ""test_file_name"": ""<name of the test file>"",
   ""test_file_content"": ""<content of the test file>"",
   ""notes"": ""<any notes you want to include>""
}}}}

{supplemental}
""";
        return prompt;
    }
    protected static string BuildContext(AnalysisResult analysis)
    {
        StringBuilder context = new();
        foreach (var definition in analysis.Definitions)
        {
            context.AppendLine($"Symbol: {definition.FullName}");

            if (definition.Supplement != null)
            {
                context.AppendLine($"Supplement Object: {definition.Supplement.Definition.FullName}");
                context.AppendLine($"Reason for supplement: {definition.Supplement.ReasonForSupplementing}");
                context.AppendLine($"--START OF SUPPLEMENT CODE--\n{definition.Supplement.Definition.Code}\n--END OF CODE--");
            }

            context.AppendLine($"--START OF DEFINITION CODE--\n{definition.Code}\n--END OF CODE--\n");
            context.AppendLine();
        }

        return context.ToString();
    }

    protected async Task<UnitTestGenerationResult> GenerateInternal(string fileToTest, string role)
    {
        var uutContent = File.ReadAllText(fileToTest);
        var definitions = await _refFinder.FindDefinitions(fileToTest, _config.ContextSearchDepth);
        var analysis = _analyzer.Analyze(definitions, fileToTest).Result;

        string systemPrompt = BuildSystemPrompt(uutContent, role);
        string userPrompt = BuildUserPrompt(uutContent, BuildContext(analysis));

        EmitOutput($"Prompting OpenAI with the following prompt:\n{userPrompt}");

        _api.SystemPrompt = systemPrompt;
        string session = Guid.NewGuid().ToString();
        var response = await _api.Prompt(session, userPrompt);

        var json = Util.ExtractJsonFromCompletion(response);
        var testDto = JsonConvert.DeserializeObject<UnitTestAIResponse>(json);

        EmitOutput($"Got response from OpenAI:\n{response}");

        return new UnitTestGenerationResult() { Analysis = analysis, AIResponse = testDto, ChatSession = session };
    }

    protected abstract string GetSupplementalSystemPrompt(string uut);
}
