using CSharpTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OpenAIAPI_Rystem.Functions;

public interface ICSharpService
{
    event EventHandler<string> OnOutput;
    Task<CSharpDefinitionsResponse> GetDefinitions(CSharpDefinitionsRequest request);
}

public class CSharpService : ICSharpService
{
    readonly DefinitionAnalyzer _analyzer;
    readonly ReferenceFinder _finder;

    public event EventHandler<string> OnOutput;

    public CSharpService()
    {
        _finder = new ReferenceFinder(Emit);
        _analyzer = new DefinitionAnalyzer(_finder, Emit);
    }

    private void Emit(string output)
    {
        OnOutput?.Invoke(this, output);
    }

    public async Task<CSharpDefinitionsResponse> GetDefinitions(CSharpDefinitionsRequest request)
    {
        try
        {
            List<DefinitionAnalyzer.AnalysisResult> results = new();

            foreach (var filepath in request.Filepaths)
            {
                var result = await _finder.FindDefinitions(filepath, request.MaxDepth);
                var analysis = await _analyzer.Analyze(result,filepath);
                results.Add(analysis);
            }

            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };

            return new CSharpDefinitionsResponse() { Content = JsonConvert.SerializeObject(results, settings) };
        }
        catch (Exception ex)
        {
            return new CSharpDefinitionsResponse() { Error = ex.Message };
        }
    }
}

public class CSharpDefinitionsRequest
{
    public string[] Filepaths { get; set; }
    public int MaxDepth { get; set; }
}

public class CSharpDefinitionsResponse
{
    public string Error { get; set; }
    public string Content { get; set; }
}