namespace CSharpTools
{
    public partial class ReferenceFinder
    {
        public class ReferenceResult
        {
            public string File { get; set; }
            public List<ReferenceSymbol> Symbols { get; set; } = new();
        }
    }
}
