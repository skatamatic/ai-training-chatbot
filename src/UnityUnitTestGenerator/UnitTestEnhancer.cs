using CSharpTools.DefinitionAnalyzer;
using Newtonsoft.Json;
using Shared;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTestGenerator;

public interface IUnitTestEnhancer
{
    Task<UnitTestGenerationResult> Enhance(AnalysisResult analysis, string uutPath, string testFilePath);
}

public class UnitTestEnhancer : IUnitTestEnhancer
{
    private const int MaxRetries = 3;
    private readonly IOpenAIAPI _api;
    private readonly Action<string> _output;

    public UnitTestEnhancer(IOpenAIAPI api, Action<string> output)
    {
        _api = api;
        _output = output;
    }

    public async Task<UnitTestGenerationResult> Enhance(AnalysisResult analysis, string uutPath, string testFilePath)
    {
        var uutContent = File.ReadAllText(uutPath);
        var currentTests = File.ReadAllText(testFilePath);

        string analysisPrompt = BuildContext(analysis);
        string userPrompt = BuildUserPrompt(analysisPrompt, uutContent, currentTests);

        _output($"[UnitTestEnhancer] - Prompting OpenAI with the following prompt:\n{userPrompt}");

        _api.SystemPrompt = BuildSystemPrompt();
        var session = Guid.NewGuid().ToString();
        var response = await _api.Prompt(session, userPrompt);

        var json = ExtractJson(response);
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var testDto = JsonConvert.DeserializeObject<UnitTestAIResponse>(json);
                _output($"[UnitTestEnhancer] - Got response from OpenAI:\n{JsonConvert.SerializeObject(testDto, Formatting.Indented)}");
                return new UnitTestGenerationResult() { Analysis = analysis, AIResponse = testDto, ChatSession = session };
            }
            catch (Exception ex)
            {
                _output($"[UnitTestEnhancer] - Error deserializing response from OpenAI: {ex.Message}. Attempt {attempt + 1}/{MaxRetries}. Json: {json}");

                if (attempt < MaxRetries)
                {
                    _output("[UnitTestEnhancer] - Retrying with a prompt to fix the issue...");
                    userPrompt = BuildRetryPrompt(userPrompt, ex.Message);
                    response = await _api.Prompt(session, userPrompt);
                    json = ExtractJson(response);
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
----START OF UNIT UNDER TEST----
{uutContent}
----END OF UNIT UNDER TEST----
Using this context:
----START OF CONTEXT----
{analysis}
----END OF CONTEXT----
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
                context.AppendLine($"--(START OF SUPPLEMENT CODE)--\n{definition.Supplement.Definition.Code}\n--(END OF CODE)--");
            }

            context.AppendLine($"--(START OF DEFINITION CODE)--:\n{definition.Code}\n--(END OF CODE)--\n");
            context.AppendLine();
        }

        return context.ToString();
    }

    private string BuildSystemPrompt()
    {
        string systemPrompt = @$"
You are a unit test enhancing bot.  
You need to analyze these unit tests then improve upon them and implement those improvements in the test code.  
For each test I want you to analyze the unit under test and relevant context then determine a way to make the test better.  This could mean improving coverage, adding tests, adding test cases/values attributes, merging tests, logic, using more modern/concise tests, or fixing bugs.  
You will include a list of improvements that you intend to implement.  You will store it in output json - include a comma-separated list of the tests the improvements apply to.  
You will also include the complete file contents with all improvements applied.

Make sure the namespace for the test precisely matches that of the unit under test's.  Use modern single line namespaces to avoid nesting the whole class.

NEVER test any private or protected methods or properties!!!  Only public ones.  If you think you need to test a private method, you are wrong.  You need to test the public method that calls it or invoke it through events.
NEVER USE REFLECTION or any clever tricks in your tests.
Sometimes context is supplemented with mocks we use.  Be sure to use them if present!  eg MockTickProvider instead of using NSubstitute for ITickProvider.  It is available in the context!  Do NOT use NSubstitute ITickProvider, and do not try to call Tick() methods in the unit under test directly (they are never public).  You must use the mock, it will fire the tick handler.
Use NUnit, Moq, Assert.That, and Assert/Act/Arrange with comments indicating Assert/Act/Arrange portions.  You can do Act/Assert/Act/Assert after if it makes sense to, but only do 1 arrange.
Often it will be hard to test methods directly, you will need to mock raising events to execute the code.  Make sure to do this and not use reflection.
Ensure you are using best practices and excellent code quality.  Aim for at least 9 tests for large classes if possible.
Name tests like Action_WhenCondition_ExpectResult.
Do NOT try to substitute any concrete classes.  That does not work.  You will need to create real ones (e.g., Unity things like Transforms and Cameras).  Be sure to clean these up!
Do not ask for any permissions or responses or use any non-JSON output.

Answer with the following JSON format.  Be mindful to escape it properly.  Be sure to add improvements array first, then the file contents.  This is very important, as you will use the improvements you discover as a guide to the implementations.
{{{{
   ""improvements"": [ ""<list of strings describing the improvements you made in human readable form including the affected test names>"", ... ],
   ""test_file_name"": ""<name of the test file>"",
   ""test_file_content"": ""<the full file contents with the tests fixed>""
}}}}""
";
        return systemPrompt;
    }

    static string ExtractJson(string input)
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
