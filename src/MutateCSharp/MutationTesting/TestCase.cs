using System.Data;
using System.Diagnostics;
using System.Text;
using Serilog;

namespace MutateCSharp.MutationTesting;

public sealed partial class TestCase(string testName, string testProjectPath = "", string runSettingsPath = "")
{
  private const string BuildFlags = "--no-restore --no-build --nologo";
  private const string LoggerFlags = "--logger \"console;verbosity=normal\"";

  public string Name { get; } = testName;

  public async Task<(TestRunResult testResult, TimeSpan timeTaken)> RunTestWithTimeout(TimeSpan timeout)
  {
    return await RunTestWithTimeout(string.Empty, default, timeout);
  }

  public async Task<(TestRunResult testResult, TimeSpan timeTaken)> RunTestWithTimeout(string mutantFileEnvVar, int mutantId, TimeSpan timeout)
  {
    var outputTrace = new StringBuilder();
    var errorTrace = new StringBuilder();
    var timeoutSignal = new CancellationTokenSource(timeout);

    var arguments = !string.IsNullOrEmpty(mutantFileEnvVar)
      ? TestCommandArguments(mutantFileEnvVar, mutantId)
      : TestCommandArguments();
    var processInfo = CreateProcessInfo(arguments);

    using var process = new Process();
    process.StartInfo = processInfo;
    process.OutputDataReceived += (_, e) => outputTrace.AppendLine(e.Data);
    process.ErrorDataReceived += (_, e) => errorTrace.AppendLine(e.Data);

    var stopwatch = Stopwatch.StartNew();
    
    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    
    try
    {
      await process.WaitForExitAsync(timeoutSignal.Token);
      stopwatch.Stop();
    }
    catch (OperationCanceledException)
    {
      // Kill process tree since the process timed out
      process.Kill(entireProcessTree: true);
      stopwatch.Stop();
      return (TestRunResult.Timeout, stopwatch.Elapsed);
    }

    var outputTraceString = outputTrace.ToString();
    
    Log.Information(outputTraceString);
    if (outputTraceString.Contains(
          "No test matches the given testcase filter"))
    {
      Log.Warning(
        "Test discovery for {TestName} failed - try running mutation analysis again for the single test case later.",
        Name);
    }
    
    var errorTraceString = errorTrace.ToString();
    if (!string.IsNullOrWhiteSpace(errorTraceString))
    {
      Log.Error("{TestName} failed:\n{TestErrorReason}", Name, errorTraceString);
    }

    var testResult = process.ExitCode == 0
      ? TestRunResult.Success
      : TestRunResult.Failed;

    return (testResult, stopwatch.Elapsed);
  }

  private string TestCommandArguments()
  {
    return $"test {BuildFlags} {LoggerFlags} {SettingsFlag()} --filter \"DisplayName~{Name}\" {testProjectPath}";
  }

  private string TestCommandArguments(string mutantFileEnvVar, int mutantId)
  {
    return $"test {BuildFlags} {LoggerFlags} {SettingsFlag()} -e \"{mutantFileEnvVar}={mutantId}\" --filter \"DisplayName={Name}\" {testProjectPath}";
  }
  
  private string SettingsFlag() => !string.IsNullOrEmpty(runSettingsPath) 
    ? $"--settings {runSettingsPath}" : string.Empty;

  private static ProcessStartInfo CreateProcessInfo(string processArguments)
  {
    return new ProcessStartInfo
    {
      FileName = "dotnet",
      Arguments = processArguments,
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
  }
}

public sealed partial class TestCase
{
  public override bool Equals(object? other)
  {
    if (ReferenceEquals(this, other)) return true;
    if (ReferenceEquals(this, null) || ReferenceEquals(other, null) ||
        GetType() != other.GetType())
      return false;
    var otherTestCase = (other as TestCase)!;
    return Name.Equals(otherTestCase.Name);
  }

  public override int GetHashCode()
  {
    return Name.GetHashCode();
  }
}