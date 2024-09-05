using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;

namespace CSharpTools.TestRunner;

public partial class NUnitTestRunner
{
    private class Progress : IProgress<ProjectLoadProgress>, ILogger
    {
        private readonly Action<string> _output;

        public Progress(Action<string> output)
        {
            _output = output;
        }

        public LoggerVerbosity Verbosity { get; set; } = LoggerVerbosity.Normal;
        public string Parameters { get; set; } = "";

        public void Initialize(IEventSource eventSource)
        {
            eventSource.AnyEventRaised += EventSource_AnyEventRaised;
        }

        private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
        {
            _output(e.Message);
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

        public void Shutdown()
        {
        }
    }
}
