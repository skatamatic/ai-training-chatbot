using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using System.Text.RegularExpressions;

namespace CSharpTools
{
    /// <summary>
    /// Class responsible for finding references and definitions in C# code.
    /// </summary>
    public class ReferenceFinder : IDisposable
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

        public ReferenceFinder(Action<string> output)
        {
            _output = output;
        }

        Dictionary<string, (Solution solution, MSBuildWorkspace workspace)> solutions = new();
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

        private async Task FindReferencesAsync(Solution solution, Document document, int depth, Dictionary<string, ReferenceResult> results, int currentDepth = 0, HashSet<DocumentId> visitedDocuments = null)
        {
            if (currentDepth > depth) return;

            visitedDocuments ??= new HashSet<DocumentId>();
            if (visitedDocuments.Contains(document.Id)) return;

            visitedDocuments.Add(document.Id);

            var semanticModel = await document.GetSemanticModelAsync();
            var root = await semanticModel.SyntaxTree.GetRootAsync();
            var nodes = root.DescendantNodes().Where(node => semanticModel.GetSymbolInfo(node).Symbol != null);

            foreach (var node in nodes)
            {
                var symbol = semanticModel.GetSymbolInfo(node).Symbol;
                if (symbol != null && IsIncludedReferenceSymbol(symbol))
                {
                    _output($"Finding references for symbol: {symbol.Name} Kind: {symbol.Kind}");
                    var symbolReferences = await SymbolFinder.FindReferencesAsync(symbol, solution);
                    foreach (var symbolReference in symbolReferences)
                    {
                        foreach (var location in symbolReference.Locations)
                        {
                            if (!results.TryGetValue(location.Document.FilePath, out var referenceResult))
                            {
                                referenceResult = new ReferenceResult() { File = location.Document.FilePath };
                                results[location.Document.FilePath] = referenceResult;
                            }

                            referenceResult.Symbols.Add(new ReferenceSymbol() { Kind = symbol.Kind.ToString(), Name = symbol.Name });
                            var refDocument = solution.GetDocument(location.Document.Id);
                            if (refDocument != null)
                            {
                                await FindReferencesAsync(solution, refDocument, depth, results, currentDepth + 1, visitedDocuments);
                            }
                        }
                    }
                }
            }
        }

        private async Task FindDefinitionsAsync(Solution solution, Document document, int depth, Dictionary<string, DefinitionResult> results, int currentDepth = 0, HashSet<DocumentId> visitedDocuments = null)
        {
            if (currentDepth > depth) return;

            visitedDocuments ??= new HashSet<DocumentId>();
            if (visitedDocuments.Contains(document.Id)) return;

            visitedDocuments.Add(document.Id);

            var semanticModel = await document.GetSemanticModelAsync();
            var root = await semanticModel.SyntaxTree.GetRootAsync();
            var nodes = root.DescendantNodes().Where(node => semanticModel.GetSymbolInfo(node).Symbol != null);

            await ProcessNodesForDefinitionsAsync(solution, nodes, document.FilePath, depth, results, currentDepth, visitedDocuments);
        }

        private async Task ProcessNodesForDefinitionsAsync(Solution solution, IEnumerable<SyntaxNode> nodes, string filePath, int depth, Dictionary<string, DefinitionResult> results, int currentDepth, HashSet<DocumentId> visitedDocuments)
        {
            foreach (var node in nodes)
            {
                var semanticModel = await solution.GetDocument(node.SyntaxTree).GetSemanticModelAsync();
                var symbol = semanticModel.GetSymbolInfo(node).Symbol;

                if (symbol is not INamedTypeSymbol namedTypeSymbol || !IsIncludedDefinitionSymbol(namedTypeSymbol))
                {
                    continue;
                }

                _output($"Symbol found that needs definition: {namedTypeSymbol.Name} Kind: {namedTypeSymbol.Kind} Namespace: {namedTypeSymbol.ContainingNamespace}");

                if (!results.TryGetValue(filePath, out var definitionResult))
                {
                    definitionResult = new DefinitionResult() { File = filePath };
                    results[filePath] = definitionResult;
                }

                var definition = await SymbolFinder.FindSourceDefinitionAsync(namedTypeSymbol, solution);
                if (definition == null) continue;

                foreach (var location in definition.Locations.Where(x => x.SourceTree != null))
                {
                    var relevantNode = await ExtractRelevantNodeAsync(location);
                    if (relevantNode == null) continue;

                    var definitionSymbol = CreateDefinition(namedTypeSymbol, relevantNode);
                    if (!definitionResult.Definitions.ContainsKey(definitionSymbol.FullName))
                    {
                        definitionResult.Definitions[definitionSymbol.FullName] = definitionSymbol;
                        await FindNestedDefinitionsAsync(solution, relevantNode, depth, results, currentDepth + 1, visitedDocuments);
                    }
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

        private async Task FindNestedDefinitionsAsync(Solution solution, SyntaxNode relevantNode, int depth, Dictionary<string, DefinitionResult> results, int currentDepth, HashSet<DocumentId> visitedDocuments)
        {
            if (currentDepth > depth) return;

            var nestedNodes = relevantNode.DescendantNodes();
            await ProcessNodesForDefinitionsAsync(solution, nestedNodes, relevantNode.SyntaxTree.FilePath, depth, results, currentDepth, visitedDocuments);
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

        public void Dispose()
        {
            foreach (var item in solutions)
            {
                item.Value.workspace.Dispose();
            }
        }

        public class ReferenceResult
        {
            public string File { get; set; }
            public List<ReferenceSymbol> Symbols { get; set; } = new();
        }

        public class ReferenceSymbol
        {
            public string Name { get; set; }
            public string Kind { get; set; }
        }

        public class DefinitionResult
        {
            public string File { get; set; }
            public Dictionary<string, Definition> Definitions { get; set; } = new();
        }

        public class Definition
        {
            public string Symbol { get; set; }
            public string Code { get; set; }
            public string Namespace { get; set; }
            public string FullName => $"{Namespace}.{Symbol}";

            public DefinitionSupplement Supplement { get; set; }
        }

        public class DefinitionSupplement
        {
            public string ReasonForSupplementing { get; set; }
            public Definition Definition { get; set; }
        }

        public class DefinitionCompararer : IEqualityComparer<Definition>
        {
            public bool Equals(Definition x, Definition y)
            {
                return x.FullName == y.FullName;
            }

            public int GetHashCode(Definition obj)
            {
                return obj.FullName.GetHashCode();
            }
        }

        private class Progress : IProgress<ProjectLoadProgress>, IFindReferencesProgress
        {
            private readonly Action<string> _output;

            public Progress(Action<string> output)
            {
                _output = output;
            }

            public void OnCompleted()
            {
                _output("Completed");
            }

            public void OnDefinitionFound(ISymbol symbol)
            {
                _output("Definition found: " + symbol.Name);
            }

            public void OnFindInDocumentCompleted(Document document)
            {
                _output("Find in document completed: " + document.Name);
            }

            public void OnFindInDocumentStarted(Document document)
            {
                _output("Find in document started: " + document.Name);
            }

            public void OnReferenceFound(ISymbol symbol, ReferenceLocation location)
            {
                _output("Reference found: " + symbol.Name + " at " + location.Location);
            }

            public void OnStarted()
            {
                _output("Started");
            }

            public void Report(ProjectLoadProgress value)
            {
                _output(value.Operation + " " + value.FilePath);
            }

            public void ReportProgress(int current, int maximum)
            {
                _output($"Progress: {current}/{maximum}");
            }
        }
    }
}
