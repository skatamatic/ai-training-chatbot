using Newtonsoft.Json;
using ServiceInterface;

namespace OpenAIAPI_Rystem.Functions;

public class EnumerateFileSystemFunction : FunctionBase
{
    public const string FUNCTION_NAME = "enumerate_files";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Lists files at the provided path. Requires well-formed windows paths with a trailing backslash. Returns an error on failure. Only do recursive if explicitly asked. Same with a filter. If it is not needed, *.* will be used automatically. Has a flag for including dirs in the result too.  Filters need to be compatible with .net's Directory.EnumerateFiles method.";
    public override Type Input => typeof(EnumerateFilesRequest);

    private readonly ISystemService _fileSystemService;

    public EnumerateFileSystemFunction(ISystemService fileSystemService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (request is EnumerateFilesRequest filesRequest)
        {
            return await _fileSystemService.GetFilesAsync(filesRequest);
        }

        throw new ArgumentException("Invalid request type", nameof(request));
    }
}

public class GetFileContentsSystemFunction : FunctionBase
{
    public const string FUNCTION_NAME = "get_file_content";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Gets the contents of a file, or an error if it can't.";
    public override Type Input => typeof(FileContentRequest);

    private readonly ISystemService _fileSystemService;

    public GetFileContentsSystemFunction(ISystemService fileSystemService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (request is FileContentRequest contentRequest)
        {
            return await _fileSystemService.GetFileContentsAsync(contentRequest);
        }

        throw new ArgumentException("Invalid request type", nameof(request));
    }
}

public class WriteFileContentsSystemFunction : FunctionBase
{
    public const string FUNCTION_NAME = "write_file_content";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Writes content to a file and returns the file path or an error.  Optionally appends.";
    public override Type Input => typeof(WriteContentRequest);

    private readonly ISystemService _fileSystemService;

    public WriteFileContentsSystemFunction(ISystemService fileSystemService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (request is WriteContentRequest contentRequest)
        {
            return await _fileSystemService.WriteFileContentsAsync(contentRequest);
        }

        throw new ArgumentException("Invalid request type", nameof(request));
    }
}

public class ExecutePowerShellScriptSystemFunction : FunctionBase
{
    public const string FUNCTION_NAME = "execute_powershell_script";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Executes a PowerShell script (optionally in this process or in a separate console) and returns the output or an error.  ALWAYS pipe output to the Information stream via '| Write-Information'.  Prefer setting RunInSeparateConsole to false unless asked not to.";
    public override Type Input => typeof(PowerShellScriptRequest);

    private readonly ISystemService _fileSystemService;

    public ExecutePowerShellScriptSystemFunction(ISystemService fileSystemService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    protected override async Task<object> ExecuteFunctionAsync(object request)
    {
        if (request is PowerShellScriptRequest scriptRequest)
        {
            return await _fileSystemService.ExecutePowerShellScriptAsync(scriptRequest);
        }

        throw new ArgumentException("Invalid request type", nameof(request));
    }
}

public class EnumerateFilesRequest
{
    [JsonProperty("filter")]
    public string Filter { get; set; } = null;

    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("recursive")]
    public bool Recursive { get; set; }

    [JsonProperty("includeDirectories")]
    public bool IncludeDirectories { get; set; }
}

public class EnumerateFilesResponse
{
    [JsonProperty("files")]
    public string[] Files { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; }
}

public class FileContentRequest
{
    [JsonProperty("path")]
    public string Path { get; set; }
}

public class FileContentResponse
{
    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; }
}

public class WriteContentRequest
{
    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("append")]
    public bool Append { get; set; }
}

public class WriteContentResponse
{
    [JsonProperty("absolutePath")]
    public string AbsolutePath { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; }
}

public class PowerShellScriptRequest
{
    [JsonProperty("script")]
    public string Script { get; set; }

    [JsonProperty("runInSeparateConsole")]
    public bool RunInSeparateConsole { get; set; }
}

public class PowerShellScriptResponse
{
    [JsonProperty("output")]
    public string Output { get; set; }

    [JsonProperty("error")]
    public string Error { get; set; }
}