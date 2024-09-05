using CSharpTools.DefinitionAnalyzer;
using Newtonsoft.Json;
using Shared;
using Sorcerer.Interface;
using Sorcerer.Internal;
using Sorcerer.Model;
using System.Text;

namespace Sorcerer.Services;

public class UnitTestEnhancer : IUnitTestEnhancer, IOutputter
{
    static readonly Dictionary<EnhancementType, string> typeSystemPrompts = new()
    {
        { EnhancementType.General, @$"You need to analyze these unit tests then improve upon them and implement those improvements in the test code.  
For each test I want you to analyze the unit under test and relevant context then determine a way to make the test better.  
This could mean improving coverage, adding tests, adding test cases/values attributes, merging tests, logic, using more modern/concise tests, or fixing bugs." },
        { EnhancementType.Coverage,
@$"You need to analyze these unit tests then increase the test coverage.  
Look for edge cases, [TestCases], [Values] and general functionality that is not being tested." },
        { EnhancementType.Refactor,
@$"You need to look for opportunities to refactor the code to make it better.  
This can be making things more concise, adhering to best practices more, improving clarity, or improving SOLID/DRY principles.  
Do not modify any behavior or add any documentation - this is purely a refactoring job.  
Look for opportunities to extract helper functions that are repeated in multiple tests." },
        { EnhancementType.Document,
@$"You need to add excellent comments and documentation to this code.  
Do not over-explain or add redundant comments.  
Only include valuable comments in tricky sections, complex test cases, etc.  
Add some nice XML comments as appropritate.  
Do not simply repeat a method or variable name in sentence form as a comment - this type of comment must be excluded.  
If an improved variable or method name is better than a comment, just do that instead." },
        { EnhancementType.SquashBugs,
@$"Analyze this code and find any bugs, misused features, or badly designed tests and fix them.  
This includes bad logic, useless tests or misleading tests, poorly named variables, tests, assertion messages, comments, etc." },
        { EnhancementType.Clean,
@$"Clean up this code to make it more excellent.  
Remove any redundant test cases, fix any inconsistencies in naming/style/etc, remove any unused usings, add any missing arrange/act/assert comments, etc; this is a polish pass to tidy everything up." },
        { EnhancementType.Assess,
@$"Analyze these tests and assess their quality, coverage, adherance to best practices, etc.  
Give a list of improvements you would like to see in the code and a category of the improvement - but DO NOT actually modify anything.  
Categories can be General, Coverage, Refactor, Document, SquashBugs, or Cleanup.  Format the improvement line like 'Category - Improvement'.
Leave test_file_content blank." },
    };

    private const int MaxRetries = 3;
    private readonly IOpenAIAPI _api;

    public EnhancementType ActiveMode { get; private set; }

    public event EventHandler<string> OnOutput;

    public UnitTestEnhancer(IOpenAIAPI api)
    {
        _api = api;
    }

    public async Task<UnitTestGenerationResult> Enhance(AnalysisResult analysis, string uutPath, string testFilePath, EnhancementType type)
    {
        ActiveMode = type;
        OnOutput?.Invoke(this, $"Enhancing {Path.GetFileNameWithoutExtension(testFilePath)} in {type} mode");

        var uutContent = File.ReadAllText(uutPath);
        var currentTests = File.ReadAllText(testFilePath);

        string analysisPrompt = BuildContext(analysis);
        string userPrompt = BuildUserPrompt(analysisPrompt, uutContent, currentTests);

        _api.SystemPrompt = BuildSystemPrompt(type);
        var session = Guid.NewGuid().ToString();
        var response = await _api.Prompt(session, userPrompt);

        var json = Util.ExtractJsonFromCompletion(response);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var testDto = JsonConvert.DeserializeObject<UnitTestAIResponse>(json);
                OnOutput?.Invoke(this, $"Got response from OpenAI:\n\n{testDto.ToDisplayText()}");
                var result = new UnitTestGenerationResult() { Analysis = analysis, AIResponse = testDto, ChatSession = session };

                if (type == EnhancementType.Assess)
                    result.AIResponse.TestFileContent = currentTests; // No test file content for assessment.  Set to original in case the AI tried to do any changes

                return result;
            }
            catch (Exception ex)
            {
                if (attempt < MaxRetries)
                {
                    userPrompt = BuildRetryPrompt(userPrompt, ex.Message);
                    response = await _api.Prompt(session, userPrompt);
                    json = Util.ExtractJsonFromCompletion(response);
                }
                else
                {
                    throw;
                }
            }
        }

        throw new InvalidOperationException("Failed to deserialize the response from OpenAI after maximum retries.");
    }

    private string BuildUserPrompt(string analysis, string uutContent, string currentTests)
    {
        string userPrompt = $@"
Please enhance these tests:
----START OF TEST CODE----
{currentTests}
----END OF TEST CODE----
For this unit under test:
----START OF UNIT UNDER TEST CODE----
{uutContent}
----END OF UNIT UNDER TEST CODE----
Using this context:
{analysis}
";

        return userPrompt;
    }

    private string BuildRetryPrompt(string originalPrompt, string errorMessage)
    {
        string retryPrompt = $@"
The previous attempt to enhance the tests resulted in a deserialization error: {errorMessage}.
Please fix the issue in the provided JSON and enhance the tests again.
Original Prompt:
{originalPrompt}
";
        return retryPrompt;
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
                context.AppendLine($"--START OF SUPPLEMENT CODE--\n{definition.Supplement.Definition.Code}\n--(END OF CODE)--");
            }

            context.AppendLine($"---START OF DEFINITION CODE---:\n{definition.Code}\n--(END OF CODE)--\n");
            context.AppendLine();
        }

        return context.ToString();
    }

    private string BuildSystemPrompt(EnhancementType type)
    {
        string nonAssessInstructions = @"
Then you will also determine the complete file contents with all improvements applied and store it in the json too. 
Make sure the namespace for the test precisely matches that of the unit under test's. 
Do not feel obligated to make any enhancements if there are none to be made.  
Only make improvements that you are confident in and that objectively improve the code - not just changes for the sake of changes.
Always be sure to use the provided supplements instead of implementing your own mock classes if possible and when appropriate.";

        string systemPrompt = @$"
You are a unit test enhancing bot.  
{typeSystemPrompts[type]}

First include a list of improvements that you intend to implement.  You will store it in output json - include a comma-separated list of the tests the improvements apply to.  
{(type != EnhancementType.Assess ? nonAssessInstructions : "")}
{CommonPrompts.CommonTestGuidelines}

Answer with the following JSON format.  Be mindful to escape it properly.  Be sure to come up with the improvements before you try to actually improve the code so you have a game plan.
{{{{
   ""improvements"": [ ""<list of strings describing the improvements you made in human readable form including the affected test names>"", ... ],
   ""test_file_name"": ""<name of the test file>"",
   ""test_file_content"": ""<the full file contents with the tests fixed>""
}}}}""
";
        return systemPrompt;
    }
}
