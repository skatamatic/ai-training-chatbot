using Shared;
using System.Text.RegularExpressions;
using UnitTestGenerator.Model;

namespace UnitTestGenerator.Services;

public class UnityTestGenerator : BaseUnitTestGenerator
{
    public UnityTestGenerator(GenerationConfig config, IOpenAIAPI api) : base(config, api)
    {
    }

    public override Task<UnitTestGenerationResult> Generate(string fileToTest)
        => GenerateInternal(fileToTest, "Unity nunit test generation bot");

    protected override string GetSupplementalSystemPrompt(string uut)
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
}
