using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Text;
using MutateCSharp.Mutation.SchemataGenerator;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.ExecutionTracing;

public static class MutantTracerHarness
{
  public const string GlobalMutexPrefix = "mutate-csharp-trace";
  
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
    var failedTests = new ConcurrentBag<string>();
    // Each test run gets a global lock instance
    var lockIdGenerator = 1;

    await Parallel.ForEachAsync(testNames,
      async (testName, cancellationToken) =>
      {
        var localLockId = Interlocked.Increment(ref lockIdGenerator);
        var lockName = $"{GlobalMutexPrefix}{localLockId}";
        
        var sanitisedName = TestCaseUtil.ValidTestFileName(testName);
        var traceFilePath = Path.Combine(outputDirectory, sanitisedName);
        var exitCode = await
          TraceExecutionForTest(
            testProjectDirectory: testProjectDirectory,
            outputPath: traceFilePath,
            mutexName: lockName,
            testName: testName,
            runSettingsPath: runSettingsPath);
        if (exitCode != 0) failedTests.Add(testName);
      });

    return failedTests.ToFrozenSet();
  }

  public static async Task<int> TraceExecutionForTest(
    string testProjectDirectory,
    string outputPath,
    string mutexName,
    string testName,
    string runSettingsPath)
  {
    // 1) Obtain the output path environment variable
    var traceFileKey =
      ExecutionTracerSchemataGenerator.MutantTracerFilePathEnvVar;
    var traceGlobalMutexKey =
      ExecutionTracerSchemataGenerator.MutantTracerGlobalMutexEnvVar;

    // 2) Initialise necessary arguments for test runs
    var buildArgs = "--no-build --nologo -c Release";
    var loggerArgs = "--logger \"console;verbosity=normal\"";
    var runsettingsArgs = !string.IsNullOrEmpty(runSettingsPath)
      ? $"--settings \"{runSettingsPath}\""
      : string.Empty;

    // Inject the key for global mutex lock *and* the trace file output
    var injectEnvVarFlags =
      $"-e {traceFileKey}=\"{outputPath}\" -e {traceGlobalMutexKey}=\"{mutexName}\"";
    var testcaseFilterArgs = $"--filter \"FullyQualifiedName~{testName}\"";

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
    Log.Information(
      "Executing the process with command: {Binary} {CommandArguments}",
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
    // Note: this assumes the test will terminate
    await process.WaitForExitAsync();

    // 5) Record test result to console
    var outputTraceString = outputTrace.ToString();

    Log.Information(outputTraceString);
    if (outputTraceString.Contains(
          "No test matches the given testcase filter"))
    {
      throw new DataException($"{testName} failed: test not found");
    }

    var errorTraceString = errorTrace.ToString();
    if (!string.IsNullOrWhiteSpace(errorTraceString))
    {
      Log.Error("{TestName} failed:\n{TestErrorReason}", testName,
        errorTraceString);
    }

    // 6) Return exit code
    return process.ExitCode;
  }
}