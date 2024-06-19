using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.RegularExpressions;

namespace CSharpTools;

/// <summary>
/// Class responsible for finding references and definitions in C# code.
/// </summary>
public partial class ReferenceFinder : IDisposable
{
    private static readonly string[] ExcludedNamespaces = new[]
    {
        "System",
        "Microsoft",
        "Unity",
        "UnityEngine",
        "Newtonsoft",
        "NSubstitute",
        "Moq",
        "NUnit"
    };

    private static readonly string[] PrioritySingleClassDefinitionProjects = new[]
    {
        "Assembly-CSharp",
        "Assembly-CSharp-firstpass",
        "Assembly-CSharp-Editor",
        "Assembly-CSharp-Editor-firstpass",
        "Scope.Common",
        "ScopeAR.Core",
        "ScopeAR.RemoteAR.UI",
        "Scope.BundleLoader",
        "ScopePlayer",
        "ARTrackingPlatformServices-ASM",
        "Automation",
        "ScenarioSessions.Events",
        "Scope.WebModels",
        "ScenarioSessions",
        "ARTrackingServiceLoactor-ASM",
        "Scope.Style",
        "Scope.Requests",
        "SessionPlayback-asm",
        "ScopeMSMixedRealityService-ASM",
        "IntelligentPluginVersioning",
        "Scope.Cache",
        "DocumentViewer",
        "UIState",
        "ScenarioLoading",
        "VoiceCommands",
        "SessionPersistence-asm",
        "Scope.Core.Input",
        "ScopeARKit-ASM",
        "Scope.Endpoints",
        "Scope.Style.Editor",
        "Scope.Build"
    };

    private static readonly SymbolKind[] IncludeReferenceSymbolKinds = new[]
    {
        SymbolKind.NamedType,
        SymbolKind.Property,
        SymbolKind.Field,
        SymbolKind.Method,
        SymbolKind.Event,
        SymbolKind.FunctionPointerType,
        SymbolKind.TypeParameter
    };

    private static readonly Regex ExcludedNamespaceRegex = new Regex(
        $"^({string.Join("|", ExcludedNamespaces)})",
        RegexOptions.Compiled);

    private readonly Action<string> _output;
    private readonly Dictionary<string, (Solution solution, MSBuildWorkspace workspace)> solutions = new();

    public ReferenceFinder(Action<string> output)
    {
        _output = output;
    }

    /// <summary>
    /// Finds all references in the given file up to the specified depth.
    /// </summary>
    public async Task<IEnumerable<ReferenceResult>> FindAllReferences(string filePath, int depth)
    {
        var solution = await GetSolution(filePath);

        var document = solution.Projects.SelectMany(p => p.Documents).FirstOrDefault(d => d.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (document == null)
        {
            throw new FileNotFoundException($"Could not find file {filePath}");
        }

        _output($"Found file {filePath}. Locating all references...");
        var results = new Dictionary<string, ReferenceResult>();
        await FindReferencesAsync(solution, document, depth, results);

        return results.Values;
    }

    /// <summary>
    /// Finds all definitions in the given file up to the specified depth.
    /// </summary>
    public async Task<IEnumerable<DefinitionResult>> FindDefinitions(string filePath, int depth)
    {
        var solution = await GetSolution(filePath);

        var document = solution.Projects.SelectMany(p => p.Documents).FirstOrDefault(d => d.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (document == null)
        {
            throw new FileNotFoundException($"Could not find file {filePath}");
        }

        _output($"Found file {filePath}. Locating all definitions...");
        var results = new Dictionary<string, DefinitionResult>();
        await FindDefinitionsAsync(solution, document, depth, results);

        return results.Values;
    }

    /// <summary>
    /// Finds the definition of a single class by its name in the specified file path.
    /// </summary>
    public async Task<DefinitionResult> FindSingleClassDefinitionAsync(string filePath, string className)
    {
        var solution = await GetSolution(filePath);

        var symbol = await FindClassSymbolAsync(solution, className);
        if (symbol == null)
        {
            throw new FileNotFoundException($"Could not find class {className} in solution {filePath}");
        }

        foreach (var location in symbol.Locations.Where(x => x.SourceTree != null))
        {
            var relevantNode = await ExtractRelevantNodeAsync(location);
            if (relevantNode == null) continue;

            var result = new DefinitionResult() { File = location.SourceTree.FilePath };
            result.Definitions.Add(filePath, CreateDefinition(symbol, relevantNode));
            return result;
        }

        return null;
    }

    /// <summary>
    /// Crawls up the directory tree to find the solution file for the specified file path
    /// </summary>
    public static string FindSolutionFile(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(directory))
        {
            var solutionFile = Directory.GetFiles(directory, "*.sln", SearchOption.TopDirectoryOnly);
            if (solutionFile.Length > 0)
            {
                return solutionFile[0];
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        return null;
    }

    private async Task<Solution> GetSolution(string filepath)
    {
        var path = FindSolutionFile(filepath);

        if (solutions.TryGetValue(path, out var entry))
        {
            return entry.solution;
        }

        var workspace = MSBuildWorkspace.Create();
        _output($"Loading solution {path}");

        var progress = new Progress(_output);
        workspace.WorkspaceFailed += (sender, e) => _output(e.Diagnostic.Message);

        var solution = await workspace.OpenSolutionAsync(path, progress);

        solutions[path] = (solution, workspace);

        return solution;
    }

    private async Task<INamedTypeSymbol> FindClassSymbolAsync(Solution solution, string className)
    {
        var projects = solution.Projects
            .OrderBy(p =>
            {
                int index = Array.IndexOf(PrioritySingleClassDefinitionProjects, p.Name);
                return index == -1 ? int.MaxValue : index;
            })
            .ThenBy(p => p.Name)
            .ToList();

        foreach (var project in projects)
        {
            var classSymbol = await FindClassSymbolInProjectAsync(project, className);
            if (classSymbol != null)
            {
                return classSymbol;
            }
        }

        return null;
    }

    private async Task<INamedTypeSymbol> FindClassSymbolInProjectAsync(Project project, string className)
    {
        var classSymbols = await SymbolFinder.FindDeclarationsAsync(project, className, ignoreCase: false, filter: SymbolFilter.Type);
        return classSymbols.OfType<INamedTypeSymbol>().FirstOrDefault(symbol => symbol.Name.Equals(className, StringComparison.Ordinal));
    }

    private async Task FindReferencesAsync(
        Solution solution,
        Document document,
        int depth,
        Dictionary<string, ReferenceResult> results,
        int currentDepth = 0,
        HashSet<DocumentId> visitedDocuments = null)
    {
        if (currentDepth > depth) return;

        visitedDocuments ??= new HashSet<DocumentId>();
        if (visitedDocuments.Contains(document.Id)) return;

        visitedDocuments.Add(document.Id);

        var semanticModel = await document.GetSemanticModelAsync();
        var root = await semanticModel.SyntaxTree.GetRootAsync();
        var nodes = GetNodesWithSymbols(semanticModel, root);

        foreach (var node in nodes)
        {
            var symbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol != null && IsIncludedReferenceSymbol(symbol))
            {
                _output($"Finding references for symbol: {symbol.Name} Kind: {symbol.Kind}");
                await FindAndProcessReferencesAsync(solution, symbol, depth, results, currentDepth, visitedDocuments);
            }
        }
    }

    private IEnumerable<SyntaxNode> GetNodesWithSymbols(SemanticModel semanticModel, SyntaxNode root)
    {
        return root.DescendantNodes().Where(node => semanticModel.GetSymbolInfo(node).Symbol != null);
    }

    private async Task FindAndProcessReferencesAsync(
        Solution solution,
        ISymbol symbol,
        int depth,
        Dictionary<string, ReferenceResult> results,
        int currentDepth,
        HashSet<DocumentId> visitedDocuments)
    {
        var symbolReferences = await SymbolFinder.FindReferencesAsync(symbol, solution);

        foreach (var symbolReference in symbolReferences)
        {
            foreach (var location in symbolReference.Locations)
            {
                await ProcessReferenceLocationAsync(solution, location, symbol, depth, results, currentDepth, visitedDocuments);
            }
        }
    }

    private async Task ProcessReferenceLocationAsync(
        Solution solution,
        ReferenceLocation location,
        ISymbol symbol,
        int depth,
        Dictionary<string, ReferenceResult> results,
        int currentDepth,
        HashSet<DocumentId> visitedDocuments)
    {
        if (!results.TryGetValue(location.Document.FilePath, out var referenceResult))
        {
            referenceResult = new ReferenceResult { File = location.Document.FilePath };
            results[location.Document.FilePath] = referenceResult;
        }

        referenceResult.Symbols.Add(new ReferenceSymbol
        {
            Kind = symbol.Kind.ToString(),
            Name = symbol.Name
        });

        var refDocument = solution.GetDocument(location.Document.Id);
        if (refDocument != null)
        {
            await FindReferencesAsync(solution, refDocument, depth, results, currentDepth + 1, visitedDocuments);
        }
    }

    private async Task FindDefinitionsAsync(Solution solution, Document document, int depth, Dictionary<string, DefinitionResult> results, int currentDepth = 0)
    {
        if (currentDepth > depth) return;

        var semanticModel = await document.GetSemanticModelAsync();
        var root = await semanticModel.SyntaxTree.GetRootAsync();
        var nodes = root.DescendantNodes().Where(node => semanticModel.GetSymbolInfo(node).Symbol != null);

        await ProcessNodesForDefinitionsAsync(solution, nodes, document.FilePath, depth, results, currentDepth);
    }

    private async Task ProcessNodesForDefinitionsAsync(
        Solution solution,
        IEnumerable<SyntaxNode> nodes,
        string filePath,
        int depth,
        Dictionary<string, DefinitionResult> results,
        int currentDepth)
    {
        foreach (var node in nodes)
        {
            var semanticModel = await GetSemanticModelAsync(solution, node);
            var symbol = semanticModel.GetSymbolInfo(node).Symbol;

            if (symbol is not INamedTypeSymbol namedTypeSymbol || !IsIncludedDefinitionSymbol(namedTypeSymbol))
            {
                continue;
            }

            _output($"Symbol found that needs definition: {namedTypeSymbol.Name} Kind: {namedTypeSymbol.Kind} Namespace: {namedTypeSymbol.ContainingNamespace}");
            var definitionResult = GetOrCreateDefinitionResult(filePath, results);
            var definition = await FindSourceDefinitionAsync(solution, namedTypeSymbol);

            if (definition == null) continue;

            await ProcessDefinitionLocationsAsync(solution, definition, namedTypeSymbol, depth, results, currentDepth, definitionResult);
        }
    }

    private async Task<SemanticModel> GetSemanticModelAsync(Solution solution, SyntaxNode node)
    {
        var document = solution.GetDocument(node.SyntaxTree);
        return await document.GetSemanticModelAsync();
    }

    private DefinitionResult GetOrCreateDefinitionResult(string filePath, Dictionary<string, DefinitionResult> results)
    {
        if (!results.TryGetValue(filePath, out var definitionResult))
        {
            definitionResult = new DefinitionResult { File = filePath };
            results[filePath] = definitionResult;
        }
        return definitionResult;
    }

    private async Task<INamedTypeSymbol> FindSourceDefinitionAsync(Solution solution, INamedTypeSymbol namedTypeSymbol)
    {
        return await SymbolFinder.FindSourceDefinitionAsync(namedTypeSymbol, solution) as INamedTypeSymbol;
    }

    private async Task ProcessDefinitionLocationsAsync(
        Solution solution,
        INamedTypeSymbol definition,
        INamedTypeSymbol namedTypeSymbol,
        int depth,
        Dictionary<string, DefinitionResult> results,
        int currentDepth,
        DefinitionResult definitionResult)
    {
        foreach (var location in definition.Locations.Where(loc => loc.SourceTree != null))
        {
            var relevantNode = await ExtractRelevantNodeAsync(location);
            if (relevantNode == null) continue;

            var definitionSymbol = CreateDefinition(namedTypeSymbol, relevantNode);
            string key = $"{location.SourceTree.FilePath}:{namedTypeSymbol.Name}";
            if (!definitionResult.Definitions.ContainsKey(key))
            {
                definitionResult.Definitions[key] = definitionSymbol;
                await FindNestedDefinitionsAsync(solution, relevantNode, depth, results, currentDepth + 1);
            }
        }
    }

    private async Task<SyntaxNode> ExtractRelevantNodeAsync(Location location)
    {
        if (location.SourceTree == null) return null;

        var nodeToExtract = await location.SourceTree.GetRootAsync().ConfigureAwait(false);
        return nodeToExtract.FindNode(location.SourceSpan);
    }

    private Definition CreateDefinition(INamedTypeSymbol symbol, SyntaxNode relevantNode)
    {
        var code = CleanCodeSnippet(relevantNode.ToFullString());
        var namespaces = GetNamespaceList(symbol.ContainingNamespace);

        return new Definition
        {
            Symbol = symbol.Name,
            Code = code,
            Namespace = string.Join('.', namespaces)
        };
    }

    private async Task FindNestedDefinitionsAsync(Solution solution, SyntaxNode relevantNode, int depth, Dictionary<string, DefinitionResult> results, int currentDepth)
    {
        if (currentDepth > depth) return;

        var nestedNodes = relevantNode.DescendantNodes();
        await ProcessNodesForDefinitionsAsync(solution, nestedNodes, relevantNode.SyntaxTree.FilePath, depth, results, currentDepth);
    }

    private static List<string> GetNamespaceList(INamespaceSymbol namespaceNode)
    {
        var namespaces = new List<string> { namespaceNode.Name };
        while (namespaceNode.ContainingNamespace != null)
        {
            namespaceNode = namespaceNode.ContainingNamespace;
            if (!string.IsNullOrEmpty(namespaceNode.Name))
            {
                namespaces.Add(namespaceNode.Name);
            }
        }
        namespaces.Reverse();
        return namespaces;
    }

    private static bool IsIncludedReferenceSymbol(ISymbol symbol)
    {
        if (!IncludeReferenceSymbolKinds.Contains(symbol.Kind))
            return false;

        var containingNamespace = symbol.ContainingNamespace?.ToDisplayString();
        return containingNamespace != null && !ExcludedNamespaceRegex.IsMatch(containingNamespace);
    }

    private static bool IsIncludedDefinitionSymbol(ISymbol symbol)
    {
        if (symbol.Kind != SymbolKind.NamedType)
            return false;

        var containingNamespace = symbol.ContainingNamespace?.ToDisplayString();
        return containingNamespace != null && !ExcludedNamespaceRegex.IsMatch(containingNamespace);
    }

    public static string CleanCodeSnippet(string codeSnippet)
    {
        var lines = codeSnippet.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        var nonEmptyLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

        if (!nonEmptyLines.Any())
        {
            return codeSnippet;
        }

        var minIndent = nonEmptyLines.Min(line => line.TakeWhile(char.IsWhiteSpace).Count());
        var cleanedLines = lines.Select(line => line.Length >= minIndent ? line.Substring(minIndent) : line).ToList();

        while (string.IsNullOrWhiteSpace(cleanedLines.First()))
        {
            cleanedLines.RemoveAt(0);
        }

        while (string.IsNullOrWhiteSpace(cleanedLines.Last()))
        {
            cleanedLines.RemoveAt(cleanedLines.Count - 1);
        }
        
        return string.Join(Environment.NewLine, cleanedLines);
    }

    public void Dispose()
    {
        foreach (var item in solutions)
        {
            item.Value.workspace.Dispose();
        }
    }
}
