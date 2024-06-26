using CSharpTools.TestRunner;
using Newtonsoft.Json;
using Shared;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTestGenerator;

public interface IUnitTestFixer
{
    Task<UnitTestGenerationResult> Fix(FixContext context, string testFilePath, string uutPath);
}

public class FixContext
{
    public int Attempt { get; set; }
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

    public async Task<UnitTestGenerationResult> Fix(FixContext context, string testFilePath, string uutPath)
    {
        var uutContent = File.ReadAllText(uutPath);
        var currentTests = File.ReadAllBytes(testFilePath);

        string userPrompt = BuildUserPrompt(context);

        _output($"[TestFixer] - Prompting OpenAI with the following prompt:\n{userPrompt}");

        _api.SystemPrompt = BuildSystemPrompt();
        var response = await _api.Prompt(context.LastGenerationResult.ChatSession, userPrompt);

        var json = Util.ExtractJsonFromCompletion(response);
        var testDto = JsonConvert.DeserializeObject<UnitTestAIResponse>(json);

        _output($"[TestFixer] - Got response from OpenAI:\n{JsonConvert.SerializeObject(testDto, Formatting.Indented)}");

        if (string.IsNullOrEmpty(testDto.TestFileContent))
        {
            throw new InvalidOperationException("Got empty/null test content data from API");
        }

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
You can only fix the UNIT TEST code.  If you suggest fixing any other code you are WRONG and this is not acceptable.  
If it is impossible to fix indicate this with the canFix flag as false, and provide a reason in the reason field.  
If you CAN fix it, set that flag to true and provide the reason for the failure in the reason field.
NEVER try to suggest fixing anything other than the test code, if an error is in non test code you must fix or remove the failing test.  
We CANNOT change access modifiers/etc and can only test around such limitations.
Be very sure you have imported all the correct usings.  
Pay close attention to access modifiers!!  Only call/access public members and properties.

If there's a more general compilation error fix, indicate that in the 'generalFix' field then fix all relevant portions of the tests that have the issue

If you are trying the same things as a previous attempt this means you are not learning from your mistakes.  You should try something different.  
If you can't fix test(s) after 3 consecutive attempts, consider them unfixable and remove it or comment it out.  This is IMPORTANT

Make sure the namespace for the test precisely matches that of the unit under test's.  Use modern single line namespaces to avoid nesting the whole class.
Do not include any explanatory comments for any fixes you made that do not otherwise improve the code quality.

Pay close attention when trying to moq method calls with optional parameters.  If this happens, you must just add the appropriate It.IsAny<> calls to the method call.  Do not try to remove the optional parameters from the method call.
NEVER test any private or protected methods or properties!!!  Only public ones.  If you think you need to test a private method, you are wrong.  You need to test the public method that calls it or invoke it through events.
NEVER USE REFLECTION or any clever tricks in your tests.
Use nunit, nsubstitute, Assert.That, and Assert/Act/Arrange with comments indicating Assert/Act/Arrange poritons.  You can do Act/Assert/Act/Assert after if it makes sense to, but only do 1 arrange.

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
            var context = GetCompilationErrorContext(error);
            if (!string.IsNullOrEmpty(context))
                sb.AppendLine($"Build Error\n---------\n{error}\nCode with issue:\n{context}---------");
            else
                sb.AppendLine($"Build Error\n---------\n{error}\n---------");
        }

        foreach (var issue in testRun.FailedTests)
        {
            sb.AppendLine($"Test Failure\n---------");
            sb.AppendLine($"Test: {issue.FullName}");
            sb.AppendLine($"Result: {issue.Result}");
            sb.AppendLine($"Message:\n{issue.Message}");
            sb.AppendLine($"StackTrace:\n{issue.StackTrace}\n---------");
            var context = GetStackContextCode(issue.StackTrace);
            if (!string.IsNullOrEmpty(context))
                sb.AppendLine($"Code with issue:\n{context}\n---------");
        }

        if (!testRun.Success && testRun.Errors.Count > 0 && testRun.FailedTests.Count == 0 && testRun.BuildErrors.Count == 0)
        {
            foreach (var error in testRun.Errors)
            {
                var context = GetDotNetTestErrorContext(error);
                if (!string.IsNullOrEmpty(context))
                    sb.AppendLine($"Analyzer Error\n---------\n{error}\nCode with issue:\n{context}---------");
                else
                    sb.AppendLine($"Analyzer Error\n---------\n{error}\n---------");
            }
        }

        return sb.ToString();
    }

    private string GetStackContextCode(string stackTrace)
    {
        return GetContextFromFile(stackTrace, @"in (.*):line (\d+)");
    }

    private string GetCompilationErrorContext(string errorMessage)
    {
        return GetContextFromFile(errorMessage, @"(.*)\((\d+),\d+\): error CS\d+: .*");
    }

    private string GetDotNetTestErrorContext(string errorMessage)
    {
        return GetContextFromFile(errorMessage, @"^(.*)\((\d+),\d+\): error .*");
    }

    private string GetContextFromFile(string message, string pattern)
    {
        try
        {
            var match = Regex.Match(message, pattern);
            if (match.Success)
            {
                var filePath = match.Groups[1].Value;
                var lineNumber = int.Parse(match.Groups[2].Value);
                return GetFileLines(filePath, lineNumber);
            }
        }
        catch (Exception ex)
        {
            _output($"[TestFixer] - Error processing context from message: {ex.Message}");
        }
        return null;
    }

    private string GetFileLines(string filePath, int lineNumber)
    {
        try
        {
            var fileLines = File.ReadAllLines(filePath).ToList();
            var startLine = Math.Max(lineNumber - 6, 0);
            var endLine = Math.Min(lineNumber + 4, fileLines.Count - 1);
            StringBuilder sb = new();

            _output($"[TestFixer] - Fetching lines {startLine + 1} to {endLine + 1} from {filePath}:");
            for (int i = startLine; i <= endLine; i++)
            {
                if (i == lineNumber - 1)
                {
                    fileLines[i] += " //  <-- Issue here";
                }
                sb.AppendLine(fileLines[i]);
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _output($"[TestFixer] - Error processing file {filePath}: {ex.Message}");
        }
        return null;
    }
}
