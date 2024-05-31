using System.Collections.Immutable;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.FileSystem;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.CLI;

internal static class TraceHandler
{
  internal static async Task RunOptions(TraceOptions options)
  {
    // Validation checks
    var individualTestSpecified =
      !string.IsNullOrEmpty(options.SpecifiedTestName);
    var testSuiteSpecified =
      !string.IsNullOrEmpty(options.AbsoluteListOfTestsFilePath);

    if (individualTestSpecified && testSuiteSpecified ||
        !individualTestSpecified && !testSuiteSpecified)
    {
      throw new ArgumentException(
        "Either both the test suite and an individual test are specified," +
        "or both are not provided as input, but only either one is accepted.");
    }
    
    if (!Directory.Exists(options.AbsoluteExecutionTraceOutputDirectory))
    {
      Directory.CreateDirectory(options.AbsoluteExecutionTraceOutputDirectory);
    }
    
    var match = await CheckMutationRegistryMatches(
      options.AbsoluteMutationRegistryPath,
      options.AbsoluteExecutionTraceRegistryPath);
    
    if (!match)
    {
      Log.Error("The mutation registry does not match. Check again if both" +
                "registries are the result of the same optimisation level enabled.");
      return;
    }

    // 1) Rebuild the SUT, as the SUT assembly could be stale.
    Log.Information("Building solution: {TestProjectPath}.",
      options.AbsoluteTestProjectDirectory);
    var buildExitCode =
      await DotnetUtil.Build(options.AbsoluteTestProjectDirectory);
    if (buildExitCode != 0)
    {
      Log.Error(
        "Solution cannot be rebuild. Perhaps the tracer generation/mutation failed?");
      return;
    }
    
    // 2) Execute tests and trace which mutants are invoked for each test.
    Log.Information("Tracing mutant execution.");
    
    if (individualTestSpecified)
    {
      await ProcessTest(
        testProjectDir: options.AbsoluteTestProjectDirectory,
        traceOutputDir: options.AbsoluteExecutionTraceOutputDirectory,
        testName: options.SpecifiedTestName,
        runSettingsPath: options.AbsoluteRunSettingsPath);
    }
    else
    {
      await ProcessAllTests(
        testProjectDir: options.AbsoluteTestProjectDirectory,
        traceOutputDir: options.AbsoluteExecutionTraceOutputDirectory,
        testListFilePath: options.AbsoluteListOfTestsFilePath,
        runSettingsPath: options.AbsoluteRunSettingsPath);
    }
  }

  // Sanity check: the mutation registry should match that of tracer's mutation
  // registry. This defends against the case where a registry corresponding with 
  // optimised mutant generation is matched with a registry corresponding with
  // non-optimised mutant generation.
  private static async Task<bool> CheckMutationRegistryMatches(
    string registryPath, string tracerRegistryPath)
  {
    var registry =
      (await MutationRegistryPersister.ReconstructRegistryFromDisk(registryPath))
      .ProjectRelativePathToRegistry;

    var tracerRegistry =
      (await MutationRegistryPersister.ReconstructRegistryFromDisk(tracerRegistryPath))
      .ProjectRelativePathToRegistry;

    return registry.Count == tracerRegistry.Count &&
           registry.All(entry =>
             tracerRegistry.ContainsKey(entry.Key) &&
             entry.Value.Mutations.Count == 
             tracerRegistry[entry.Key].Mutations.Count);
  }

  private static async Task<ImmutableArray<string>> ParseListOfTestsFile(string filePath)
  {
    var testNames = await File.ReadAllLinesAsync(filePath);
    return [..testNames.Select(name => name.Trim())];
  }

  private static async Task ProcessTest(
    string testProjectDir,
    string traceOutputDir,
    string testName,
    string runSettingsPath)
  {
    var exitCode = await MutantTracerHarness.TraceExecutionForTest(
      testProjectDirectory: testProjectDir, 
      outputPath: traceOutputDir,
      testName: testName,
      runSettingsPath: runSettingsPath);
    
    // 3) Record flaky test.
    if (exitCode != 0)
    {
      Log.Warning("Failed test: {FailedTestName}", testName);
      Log.Warning("Remove the flaky test that fail by default before proceeding.");
    }
  }

  private static async Task ProcessAllTests(
    string testProjectDir, 
    string traceOutputDir, 
    string testListFilePath,
    string runSettingsPath)
  {
    var failedTests = await MutantTracerHarness.TraceExecutionForAllTests(
      testProjectDirectory: testProjectDir,
      outputDirectory: traceOutputDir,
      testNames: await ParseListOfTestsFile(testListFilePath),
      runSettingsPath: runSettingsPath);

    Log.Information("Mutant tracing complete.");
    
    // 3) Record flaky tests.
    foreach (var failedTest in failedTests)
    {
      Log.Warning("Failed test: {FailedTestName}", failedTest);
    }

    if (failedTests.Count > 0)
    {
      Log.Warning("Remove the flaky tests that fail by default before proceeding.");
    }
  }
}