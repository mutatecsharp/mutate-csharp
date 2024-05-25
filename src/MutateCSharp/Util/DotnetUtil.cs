using System.Diagnostics;
using System.Text;
using Serilog;

namespace MutateCSharp.Util;

public static class DotnetUtil
{
  /*
   * A wrapper around .NET CLI build command.
   * Takes a directory or path and builds the solution or project.
   */
  public static async Task<int> Build(string absolutePath)
  {
    // 1) Create an isolated subprocess with environment variable injected into
    // the subprocess
    var processInfo = new ProcessStartInfo
    {
      FileName = "dotnet",
      Arguments = $"build --nologo -c Release {absolutePath}",
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    Log.Information("Building {SUT}.", absolutePath);
    Log.Information("Executing the process with command: {Binary} {CommandArguments}",
      processInfo.FileName, processInfo.Arguments);

    // 2) Start the build subprocess
    var outputTrace = new StringBuilder();
    var errorTrace = new StringBuilder();
    using var process = new Process();
    process.StartInfo = processInfo;
    process.OutputDataReceived += (_, e) => outputTrace.AppendLine(e.Data);
    process.ErrorDataReceived += (_, e) => errorTrace.AppendLine(e.Data);

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    await process.WaitForExitAsync();

    // 3) Record build result to console
    Log.Information(outputTrace.ToString());

    var errorTraceString = errorTrace.ToString();
    if (!string.IsNullOrWhiteSpace(errorTraceString))
    {
      Log.Error("Build failed:\n{BuildErrorReason}", errorTraceString);
    }

    // 4) Return exit code
    return process.ExitCode;
  }
}