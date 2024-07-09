using Newtonsoft.Json;
using OpenAIAPI_Rystem.Services;
using Shared;

namespace OpenAIAPI_Rystem.Functions;

public class EnumerateFileSystemFunction : FunctionBase<EnumerateFilesRequest, EnumerateFilesResponse>
{
    public const string FUNCTION_NAME = "enumerate_files";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Lists files at the provided path. Requires well-formed windows paths with a trailing backslash. Returns an error on failure. Only do recursive if explicitly asked. Same with a filter. If it is not needed, *.* will be used automatically. Has a flag for including dirs in the result too. Filters need to be compatible with .net's Directory.EnumerateFiles method.";

    private readonly ISystemService _fileSystemService;

    public EnumerateFileSystemFunction(ISystemService fileSystemService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    protected override async Task<EnumerateFilesResponse> ExecuteFunctionAsync(EnumerateFilesRequest request)
    {
        try
        {
            return await _fileSystemService.GetFilesAsync(request);
        }
        catch (Exception ex)
        {
            return new EnumerateFilesResponse() { Error = ex.ToString() };
        }
    }
}

public class GetFileContentsSystemFunction : FunctionBase<FileContentRequest, FileContentResponse>
{
    public const string FUNCTION_NAME = "get_file_content";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Gets the contents of a file, or an error if it can't.";

    private readonly ISystemService _fileSystemService;

    public GetFileContentsSystemFunction(ISystemService fileSystemService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    protected override async Task<FileContentResponse> ExecuteFunctionAsync(FileContentRequest request)
    {
        try
        {
            return await _fileSystemService.GetFileContentsAsync(request);
        }
        catch (Exception ex)
        {
            return new FileContentResponse() { Error = ex.ToString() };
        }
    }
}

public class WriteFileContentsSystemFunction : FunctionBase<WriteContentRequest, WriteContentResponse>
{
    public const string FUNCTION_NAME = "write_file_content";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Writes content to a file and returns the file path or an error. Optionally appends.";

    private readonly ISystemService _fileSystemService;

    public WriteFileContentsSystemFunction(ISystemService fileSystemService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    protected override async Task<WriteContentResponse> ExecuteFunctionAsync(WriteContentRequest request)
    {
        try
        {
            return await _fileSystemService.WriteFileContentsAsync(request);
        }
        catch (Exception ex)
        {
            return new WriteContentResponse() { Error = ex.ToString() };
        }
    }
}

public class ExecutePowerShellScriptSystemFunction : FunctionBase<PowerShellScriptRequest, PowerShellScriptResponse>
{
    public const string FUNCTION_NAME = "execute_powershell_script";

    public override string Name => FUNCTION_NAME;
    public override string Description => "Executes a PowerShell script and returns the output or an error. ALWAYS pipe output (like Writes) to the Information stream via '| Write-Information'";

    private readonly ISystemService _fileSystemService;

    public ExecutePowerShellScriptSystemFunction(ISystemService fileSystemService, IFunctionInvocationEmitter invocationEmitter)
        : base(invocationEmitter)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
    }

    protected override async Task<PowerShellScriptResponse> ExecuteFunctionAsync(PowerShellScriptRequest request)
    {
        try
        {
            request.RunInSeparateConsole = false;
            return await _fileSystemService.ExecutePowerShellScriptAsync(request);
        }
        catch (Exception ex)
        {
            return new PowerShellScriptResponse() { Error = ex.ToString() };
        }
    }
}

// Request and Response classes

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
