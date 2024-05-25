using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using MutateCSharp.Mutation.SchemataGenerator;
using Serilog;

namespace MutateCSharp.ExecutionTracing;

public static class MutantTracerHarness
{
  /*
   * For each test, set the environment variable for the output path,
   * and run the test on the instrumented system under test.
   */
  public static async Task<FrozenSet<string>> TraceExecutionForAllTests(
    string testProjectDirectory,
    string outputDirectory,
    ImmutableArray<string> testNames,
    string runSettingsPath)
  {
    var invalidFileNameChars = Path.GetInvalidFileNameChars().ToFrozenSet();
    var failedTests = new HashSet<string>();

    foreach (var testName in testNames)
    {
      var sanitisedName = string.Join(string.Empty,
        testName.Where(c => !invalidFileNameChars.Contains(c)));
      var traceFilePath = Path.Combine(outputDirectory, sanitisedName);
      var exitCode = await TraceExecutionForTest(testProjectDirectory,
        traceFilePath, testName, runSettingsPath);
      
      if (exitCode != 0) failedTests.Add(testName);
    }

    return failedTests.ToFrozenSet();
  }

  private static async Task<int> TraceExecutionForTest(
    string testProjectDirectory,
    string outputPath,
    string testName,
    string runSettingsPath)
  {
    // 1) Obtain the output path environment variable
    var envVar = ExecutionTracerSchemataGenerator.MutantTracerFilePathEnvVar;

    // 2) Initialise necessary arguments for test runs
    var buildArgs = "--no-build --nologo -c Release";
    var loggerArgs = "--logger \"console;verbosity=normal\"";
    var runsettingsArgs = !string.IsNullOrEmpty(runSettingsPath)
      ? $"--settings \"{runSettingsPath}\""
      : string.Empty;
    var injectEnvVarFlags = $"-e {envVar}=\"{outputPath}\"";
    var testcaseFilterArgs = $"--filter \"DisplayName~{testName}\"";

    // 3) Create an isolated subprocess with environment variable injected into
    // the subprocess
    var processInfo = new ProcessStartInfo
    {
      FileName = "dotnet",
      Arguments =
        $"test {buildArgs} {runsettingsArgs} {loggerArgs} {injectEnvVarFlags} {testcaseFilterArgs} {testProjectDirectory}",
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };

    Log.Information("Testing {TestName}.", testName);
    Log.Information("Executing the process with command: {Binary} {CommandArguments}",
      processInfo.FileName, processInfo.Arguments);

    // 4) Start the testing subprocess
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

    // 5) Record test result to console
    Log.Information(outputTrace.ToString());

    var errorTraceString = errorTrace.ToString();
    if (!string.IsNullOrWhiteSpace(errorTraceString))
    {
      Log.Error("{TestName} failed:\n{TestErrorReason}", testName, errorTraceString);
    }

    // 6) Return exit code
    return process.ExitCode;
  }
}