using CSharpTools.TestRunner;
using Newtonsoft.Json;
using Shared;
using Sorcerer.Interface;
using Sorcerer.Internal;
using Sorcerer.Model;
using System.Text;
using System.Text.RegularExpressions;

namespace Sorcerer.Services;

public class UnitTestFixer : IUnitTestFixer, IOutputter
{
    IOpenAIAPI _api;
    private readonly GenerationConfig _config;

    public event EventHandler<string> OnOutput;

    public UnitTestFixer(GenerationConfig genConfig, IOpenAIAPI api)
    {
        _api = api;
        _config = genConfig;
    }

    public async Task<UnitTestGenerationResult> Fix(FixContext context, string testFilePath, string uutPath)
    {
        var uutContent = File.ReadAllText(uutPath);
        var currentTests = File.ReadAllBytes(testFilePath);

        string userPrompt = BuildUserPrompt(context);

        OnOutput?.Invoke(this, $"Fixing tests with prompt:\n{userPrompt}");

        _api.SystemPrompt = BuildSystemPrompt();
        var response = await _api.Prompt(context.LastGenerationResult.ChatSession, userPrompt);

        var json = Util.ExtractJsonFromCompletion(response);
        var testDto = JsonConvert.DeserializeObject<UnitTestAIResponse>(json);

        OnOutput?.Invoke(this, $"Got response from OpenAI:\n\n{testDto.ToDisplayText()}");

        if (string.IsNullOrEmpty(testDto.TestFileContent))
        {
            throw new InvalidOperationException("Got empty/null test content data from API");
        }

        return new UnitTestGenerationResult() { Analysis = context.LastGenerationResult.Analysis, AIResponse = testDto, ChatSession = context.LastGenerationResult.ChatSession };
    }

    private string BuildUserPrompt(FixContext context)
    {
        string preamble = context.Attempt > 0 ? $"This is attempt {context.Attempt + 1}.  Remember to remove tests you can't fix after a few attempts.  If your fix is to change the implementation to suit the test, remove the test.  If you've tried the same thing or flip flopped between 2 identical fixes, remove the test or revert it to the last known working one.  If there are many other useful asserts but one particular one is failing sometimes it is ok to just remove that assert." : "";
        string userPrompt = $@"
{preamble}Here's the remaining issues in the tests:
-----START OF ISSUES-----
{BuildIssuesPrompt(context.ProjectRootPath, context.LastTestRunResults)}
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
If you can't fix test(s) after 3 consecutive attempts, consider them unfixable and remove it or comment it out.  Do not use Assert.Ignore or similar attributes.

Make sure the namespace for the test precisely matches that of the unit under test's.
Do not include any explanatory comments for any fixes you made that do not otherwise improve the code quality.

{CommonPrompts.CommonTestGuidelines}
{_config.StylePrompt}

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


    private string BuildIssuesPrompt(string rootPath, TestRunResult testRun)
    {
        StringBuilder sb = new();

        foreach (var error in testRun.BuildErrors)
        {
            var context = GetCompilationErrorContext(rootPath, error);
            if (!string.IsNullOrEmpty(context))
                sb.AppendLine($"Build Error\n---------\n{error}\n---Start of code with issue---\n{context}\n---End of code with issue---");
            else
                sb.AppendLine($"Build Error\n---------\n{error}\n---------");
        }

        foreach (var issue in testRun.FailedTests)
        {
            sb.AppendLine($"Test Failure\n---------");
            sb.AppendLine($"Test: {issue.FullName}");
            sb.AppendLine($"Result: {issue.Result}");
            sb.AppendLine($"Message: {issue.Message?.Trim()}");

            if (!string.IsNullOrEmpty(issue.StackTrace))
                sb.AppendLine($"StackTrace: {issue.StackTrace?.Trim()}\n---------");

            var context = GetStackContextCode(rootPath, issue.StackTrace);

            if (!string.IsNullOrEmpty(context))
                sb.AppendLine($"---Start of code with issue---\n{context}\n---End of code with issue---");
        }

        if (!testRun.Success && testRun.Errors.Count > 0 && testRun.FailedTests.Count == 0 && testRun.BuildErrors.Count == 0)
        {
            foreach (var error in testRun.Errors)
            {
                var context = GetDotNetTestErrorContext(rootPath, error);
                if (!string.IsNullOrEmpty(context))
                    sb.AppendLine($"Analyzer Error\n---------\n{error}\n---Start of code with issue---\n{context}\n---End of code with issue---");
                else
                    sb.AppendLine($"Analyzer Error\n---------\n{error}\n---------");
            }
        }

        return sb.ToString();
    }

    private string GetStackContextCode(string rootPath, string stackTrace)
    {
        return GetContextFromFile(rootPath, stackTrace, @"in (.*):[^0-9]*(\d+)");
    }

    private string GetCompilationErrorContext(string rootPath, string errorMessage)
    {
        return GetContextFromFile(rootPath, errorMessage, @"(.*)\((\d+),\d+\):[^0-9]*\d+:.*");
    }

    private string GetDotNetTestErrorContext(string rootPath, string errorMessage)
    {
        return GetContextFromFile(rootPath, errorMessage, @"^.*:.*in (.*):[^0-9]*(\d+).*");
    }

    private string GetContextFromFile(string rootPath, string message, string pattern)
    {
        try
        {
            if (string.IsNullOrEmpty(message))
                return null;

            var matches = Regex.Matches(message, pattern);
            foreach (var match in matches.OfType<Match>())
            {
                try
                {
                    var filePath = match.Groups[1].Value;
                    var lineNumber = int.Parse(match.Groups[2].Value);
                    return GetFileLines(rootPath, filePath, lineNumber);
                }
                catch
                {
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            OnOutput?.Invoke(this, $"Error processing context from message: {ex.Message}");
        }
        return null;
    }

    private string GetFileLines(string rootPath, string filePath, int lineNumber)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                filePath = Path.Combine(rootPath, filePath);
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileLines = File.ReadAllLines(filePath).ToList();
            var startLine = Math.Max(lineNumber - _config.IssueContextLineCount, 0);
            var endLine = Math.Min(lineNumber + _config.IssueContextLineCount, fileLines.Count - 1);
            StringBuilder sb = new();

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
            OnOutput?.Invoke(this, $"Error processing file {filePath}: {ex.Message}");
        }
        return null;
    }
}
