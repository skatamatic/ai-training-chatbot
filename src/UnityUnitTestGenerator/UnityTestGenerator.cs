using Shared;
using CSharpTools;
using System.Text;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using CSharpTools.DefinitionAnalyzer;
using CSharpTools.ReferenceFinder;

namespace UnitTestGenerator;

public class GenerationConfig
{
    public int ContextSearchDepth { get; set; }
}

public class UnitTestGenerationResult
{
    public UnitTestAIResponse AIResponse { get; set; }
    public AnalysisResult Analysis { get; set; }
    public string ChatSession { get; set; }
}

public class UnitTestAIResponse
{
    [JsonProperty("test_file_name")]
    public string TestFileName { get; set; }

    [JsonProperty("test_file_content")]
    public string TestFileContent { get; set; }

    [JsonProperty("notes")]
    public string Notes { get; set; }

    [JsonProperty("general_fix")]
    public string GeneralFix { get; set; }

    [JsonProperty("test_fixes")]
    public UnitTestAIFix[] TestFixes { get; set; } = Array.Empty<UnitTestAIFix>();

    [JsonProperty("improvements")]
    public string[] Improvements { get; set; } = Array.Empty<string>();
}

public class UnitTestAIFix
{
    [JsonProperty("test_name")]
    public string TestName { get; set; }

    [JsonProperty("can_fix")]
    public bool CanFix { get; set; } = true;

    [JsonProperty("reason")]
    public string Reason { get; set; }

    [JsonProperty("fix")]
    public string Fix { get; set; }
}

public interface IUnitTestGenerator
{
    Task<UnitTestGenerationResult> AnalyzeOnly(string fileToTest);
    Task<UnitTestGenerationResult> Generate(string fileToTest);
}

public class UnityTestGenerator : IUnitTestGenerator
{
    GenerationConfig _config;
    IOpenAIAPI _api;
    ReferenceFinderService _refFinder;
    DefinitionAnalyzerService _analyzer;

    Action<string> _output;

    public UnityTestGenerator(GenerationConfig config, IOpenAIAPI api, Action<string> output)
    {
        _config = config;
        _api = api;
        _refFinder = new ReferenceFinderService(output);
        _analyzer = new DefinitionAnalyzerService(_refFinder, output);
        _output = output;
    }

    public async Task<UnitTestGenerationResult> Generate(string fileToTest)
    {
        var uutContent = File.ReadAllText(fileToTest);
        var definitions = await _refFinder.FindDefinitions(fileToTest, _config.ContextSearchDepth);
        var analysis = _analyzer.Analyze(definitions, fileToTest).Result;

        string systemPrompt = BuildSystemPrompt(uutContent);
        string userPrompt = BuildUserPrompt(uutContent, BuildContext(analysis));
        
        _output($"Prompting OpenAI with the following prompt:\n{userPrompt}");

        _api.SystemPrompt = systemPrompt;
        string session = Guid.NewGuid().ToString();
        var response = await _api.Prompt(session, userPrompt);

        var json = ExtractJson(response);
        var testDto = JsonConvert.DeserializeObject<UnitTestAIResponse>(json);

        _output($"Got response from OpenAI:\n{response}");

        return new UnitTestGenerationResult() { AIResponse = testDto, Analysis = analysis, ChatSession = session };
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
You are a Unity unit test generation bot.  
You are to take this csharp code as well as all the accompanying context and generate excellent quality and VERY COMPREHENSIVE unit tests for it.  Write as many tests as you can covering functionality and edge cases (but keep in mind your token limit of 4000 output tokens).

NEVER test any private or protected methods or properties!!!  Only public ones.  If you think you need to test a private method, you are wrong.  You need to test the public method that calls it or invoke it through events.
NEVER USE REFLECTION or any clever tricks in your tests.
Sometimes context is supplemented with mocks we use.  Be sure to use them if present!  eg MockTickProvider instead of using NSubstitute for ITickProvider.  It is available in the context!  Do NOT use nsubstitute ITickProvider, and do not try to call Tick() methods in the unit under test directly (they are never public).  You must use the mock, it will fire the tick handler.
Use nunit, nsubstitute, Assert.That, and Assert/Act/Arrange with comments indicating Assert/Act/Arrange poritons.  You can do Act/Assert/Act/Assert after if it makes sense to, but only do 1 arrange.
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
        string pattern = @".*public class .*Coordinator.*";
        Match match = Regex.Match(uut, pattern);

        if (match.Success)
        {
            return @"
Use this pattern to get a viewmodel out of the coordinator to test things like button presses etc.  We use mvvm so we intercept the binding context set to get a viewmodel.  Coordinators always set this context in their ctor:

public class Parameters : IDisposable //Be sure to use IDisposable if we need to destroy game objects after tests
{
    public ISomeDependency Dependency;
    public IUIControlFactory UIControlFactory;
    public ISomeView View;
    public MockTickProvider TickProvider; //Make sure we use a this tick provider, not a substitute
    public Transform parentTransform;

    public Parameters()
    {
        Dependency = Substitute.For<ISomeDependency>();
        UIControlFactory = Substitute.For<IUIControlFactory>();
        TickProvider = new MockTickProvider();
        //Concretes cannot be mocked (especially Unity things)!  Use real ones and clean them up in Dispose
        parentTransform = new GameObject().transform;
    }

    //If we have concretes, make sure to clean them up in Dispose of the parameters
    public void Dispose()
    {
        GameObject.DestroyImmediate(someTransform.gameObject);
    }
}

private SomeCoordinatorUnderTest CreateCoordinator(Parameters parameters, out TheCoordinatorsViewModelType viewModel)
{
    var view = Substitute.For<ISomeViewTypeTheCoordinatorOwns>();
    TheCoordinatorsViewModelType vm = null;

    //Fun way to not expose the vm but still access it for tests (since its events drive most behaviour in the coordinator)
    parameters.UIControlFactory.BuildControl<ISomeViewTypeTheCoordinatorOwns>(Arg.Any<Transform>()).Returns(view);
    view.When(x => x.SetBindingContext(Arg.Any<TheCoordinatorsViewModelType>())).Do(x => vm = (TheCoordinatorsViewModelType)x.Args()[0]);

    var coordinator = new SomeCoordinatorUnderTest(parameters.Dependency, parameters.UIControlFactory, paramters.parentTransform, parameters.TickProvider);
    viewModel = vm;

    return coordinator;
}

//An actual test:
[Test]
public void ViewModel_ButtonClicked_SetsAString()
{
    //If the parameters are disposable, be sure to do a using here so they get disposed
    using var parameters = new Parameters();  
    var coordinator = CreateCoordinator(parameters, out TheCoordinatorsViewModelType viewModel);

    viewModel.SomeButtonClicked();

    Assert.That(viewModel.SomeString, Is.EqualTo(""SomeExpectedValue""));
}";
        }

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

    public Task<UnitTestGenerationResult> AnalyzeOnly(string fileToTest)
    {
        throw new NotImplementedException();
    }
}