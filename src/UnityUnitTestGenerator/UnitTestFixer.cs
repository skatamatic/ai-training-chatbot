using CSharpTools.TestRunner;
using Newtonsoft.Json;
using Shared;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTestGenerator;

public interface IUnitTestFixer
{
    Task<UnitTestGenerationResult> Fix(FixContext context, string testFilePath);
}

public class FixContext
{
    public int Attempt { get; set; }
    public GenerationConfig InitialConfig { get; set; }
    public TestRunResult LastTestRunResults { get; set; }
    public UnitTestGenerationResult LastGenerationResult { get; set; }
}

public class UnitTestFixer : IUnitTestFixer
{
    IOpenAIAPI _api;

    Action<string> _output;

    public UnitTestFixer(IOpenAIAPI api, Action<string> output)
    {
        _api = api;
        _output = output;
    }

    public async Task<UnitTestGenerationResult> Fix(FixContext context, string testFilePath)
    {
        var uutContent = File.ReadAllText(context.InitialConfig.FileToTest);
        var currentTests = File.ReadAllBytes(testFilePath);

        string userPrompt = BuildUserPrompt(context);

        _output($"[TestFixer] - Prompting OpenAI with the following prompt:\n{userPrompt}");

        _api.SystemPrompt = BuildSystemPrompt();
        var response = await _api.Prompt(context.LastGenerationResult.ChatSession, userPrompt);

        var json = ExtractJson(response);
        var testDto = JsonConvert.DeserializeObject<UnitTestAIResponse>(json);

        _output($"Got response from OpenAI:\n{JsonConvert.SerializeObject(testDto, Formatting.Indented)}");

        return new UnitTestGenerationResult() { Analysis = context.LastGenerationResult.Analysis, AIResponse = testDto, ChatSession = context.LastGenerationResult.ChatSession };
    }

    private string BuildUserPrompt(FixContext context)
    {
        string userPrompt = $@"
I'll need you to fix those tests.  Here's the result of running them
-----START OF ISSUES-----
{BuildIssuesPrompt(context.LastTestRunResults)}
-----END OF ISSUES-----
";

        return userPrompt;
    }

    private string BuildSystemPrompt()
    {
        string systemPrompt = @$"
You are a unit test fixing bot.  
You need to fix the provided unit tests.  There could be compilation errors or test failures.  
For each test failure provide a suspected reason for the failure, how you will fix it in the provided json format.  Then try to fix them all in one pass, and provide the full file in the provided format.
You can only fix the test code.  Never try to fix the unit under test.  If it is impossible indicate this with the canFix flag as false, and provide a reason in the reason field.  If you CAN fix it, set that flag to true and provide the reason for the failure in the reason field.
If there's a more general compilation error fix, indicate that in the 'generalFix' field then fix all relevant portions of the tests that have the issue

If you are trying the same things as a previous attempt this means you are not learning from your mistakes.  You should try something different.  If you can't think of anything else to try, mark it as unfixable and provide a reason.

NEVER test any private or protected methods or properties!!!  Only public ones.  If you think you need to test a private method, you are wrong.  You need to test the public method that calls it or invoke it through events.
NEVER USE REFLECTION or any clever tricks in your tests.
Sometimes context is supplemented with mocks we use.  Be sure to use them if present!  eg MockTickProvider instead of using NSubstitute for ITickProvider.  It is available in the context!  Do NOT use nsubstitute ITickProvider, and do not try to call Tick() methods in the unit under test directly (they are never public).  You must use the mock, it will fire the tick handler.
Use nunit, nsubstitute, Assert.That, and Assert/Act/Arrange with comments indicating Assert/Act/Arrange poritons.  You can do Act/Assert/Act/Assert after if it makes sense to, but only do 1 arrange.
Often it will be hard to test methods directly, you will need to mock raising events to execute the code.  Make sure to do this and not use reflection.
Ensure you are using best practices and excellent code quality.  Aim for at least 9 tests for large classes if possible
Name tests like Action_WhenCondition_ExpectResult.
Do NOT try to substitute any concrete classes.  That does not work.  You will need to create real ones (eg Unity things like Transforms and Cameras).  Be sure to clean these up!
Do not ask for any permissions or responses or use any non json output.

Answer with the following json format.  Be mindful to escape it properly:
{{{{
   ""test_file_name"": ""<name of the test file>"",
   ""general_fix"": ""<general fix for compilation errors>"",
   ""test_fixes"": [ {{{{ ""test_name"": ""<name of test>"", ""can_fix"": <true if you tried to fix it or false if it seems impossible or too difficult>, ""reason"": ""<reason for failure>"", ""fix"": ""<how you will fix it>"" }}}} ],
   ""test_file_content"": ""<the full file contents with the tests fixed>""
}}}}""
";
        return systemPrompt;
    }


    private string BuildIssuesPrompt(TestRunResult testRun)
    {
        StringBuilder sb = new();
        
        foreach (var error in testRun.BuildErrors)
        {
            sb.AppendLine($"Build Error\n---------\n{error}\n---------");
        }

        foreach (var issue in testRun.FailedTests)
        {
            sb.AppendLine($"Test Failure\n---------");
            sb.AppendLine($"Test: {issue.FullName}");
            sb.AppendLine($"Result: {issue.Result}");
            sb.AppendLine($"Message: {issue.Message}");
            sb.AppendLine($"StackTrace: {issue.StackTrace}\n---------");
        }

        return sb.ToString();
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
