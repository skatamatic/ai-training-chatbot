using Newtonsoft.Json;
using System.Diagnostics;
using System.Management.Automation;
using System.Text;

namespace OpenAIAPI_Rystem.Functions;

public interface ISystemService
{
    Task<EnumerateFilesResponse> GetFilesAsync(EnumerateFilesRequest request);
    Task<FileContentResponse> GetFileContentsAsync(FileContentRequest request);
    Task<WriteContentResponse> WriteFileContentsAsync(WriteContentRequest request);
    Task<PowerShellScriptResponse> ExecutePowerShellScriptAsync(PowerShellScriptRequest request);
}

public class SystemService : ISystemService
{
    public async Task<FileContentResponse> GetFileContentsAsync(FileContentRequest request)
    {
        try
        {
            var content = await File.ReadAllTextAsync(request.Path);
            return new FileContentResponse() { Content = content };
        }
        catch (Exception ex)
        {
            return new FileContentResponse() { Error = ex.Message };
        }
    }

    public async Task<EnumerateFilesResponse> GetFilesAsync(EnumerateFilesRequest request)
    {
        return await Task.Run(() =>
        {
            var files = new List<string>();
            var errors = new List<string>();

            void TryEnumerateFilesAndDirectories(string path, string filter, SearchOption option)
            {
                try
                {
                    files.AddRange(Directory.EnumerateFiles(path, filter, option));
                }
                catch (Exception ex)
                {
                    errors.Add($"Error accessing files in {path}: {ex.Message}");
                }

                if (request.IncludeDirectories)
                {
                    try
                    {
                        files.AddRange(Directory.EnumerateDirectories(path, filter, option));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error accessing directories in {path}: {ex.Message}");
                    }
                }
            }

            if (request.Recursive)
            {
                var stack = new Stack<string>();
                stack.Push(request.Path);

                while (stack.Count > 0)
                {
                    var currentDir = stack.Pop();
                    TryEnumerateFilesAndDirectories(currentDir, request.Filter ?? "*.*", SearchOption.TopDirectoryOnly);

                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(currentDir))
                        {
                            stack.Push(dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error accessing subdirectories in {currentDir}: {ex.Message}");
                    }
                }
            }
            else
            {
                TryEnumerateFilesAndDirectories(request.Path, request.Filter ?? "*.*", SearchOption.TopDirectoryOnly);
            }

            return Task.FromResult(new EnumerateFilesResponse { Files = files.ToArray(), Error = string.Join("\n", errors) });
        });
    }

    public async Task<WriteContentResponse> WriteFileContentsAsync(WriteContentRequest request)
    {
        try
        {
            var directory = Path.GetDirectoryName(request.Path);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (request.Append)
            {
                // Append text to the file
                using var writer = File.AppendText(request.Path);
                await writer.WriteAsync(request.Content);
            }
            else
            {
                // Overwrite the file with the new content
                await File.WriteAllTextAsync(request.Path, request.Content);
            }

            return new WriteContentResponse
            {
                AbsolutePath = Path.GetFullPath(request.Path)
            };
        }
        catch (Exception ex)
        {
            return new WriteContentResponse
            {
                Error = ex.Message
            };
        }
    }

    public async Task<PowerShellScriptResponse> ExecutePowerShellScriptAsync(PowerShellScriptRequest request)
    {
        if (request.RunInSeparateConsole)
        {
            // Run the script in a separate console window
            return await RunScriptInConsoleAsync(request.Script);
        }
        else
        {
            // Run the script in the same process using PowerShell automation
            return await RunScriptInProcessAsync(request.Script);
        }
    }

    private async Task<PowerShellScriptResponse> RunScriptInConsoleAsync(string script)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command \"{script}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = false
        };

        using var process = new Process { StartInfo = processStartInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        await Task.Run(() => process.WaitForExit());

        return new PowerShellScriptResponse
        {
            Output = output
        };
    }


    private async Task<PowerShellScriptResponse> RunScriptInProcessAsync(string script)
    {
        using var powerShell = PowerShell.Create();
        
        powerShell.AddScript(script);
        var results = await Task.Run(() => powerShell.Invoke());

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        foreach (var result in powerShell.Streams.Information)
        {
            outputBuilder.AppendLine(result.ToString());
        }
        foreach (var error in powerShell.Streams.Error)
        {
            errorBuilder.AppendLine(error.ToString());
        }

        return new PowerShellScriptResponse
        {
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString()
        };
    }
}