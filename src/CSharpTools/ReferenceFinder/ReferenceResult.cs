namespace CSharpTools.ReferenceFinder;

public class ReferenceResult
{
    public string File { get; set; }
    public List<ReferenceSymbol> Symbols { get; set; } = new();
}