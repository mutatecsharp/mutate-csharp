using System.Collections.Immutable;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.FileSystem;
using Serilog;

namespace MutateCSharp.CLI;

internal static class TraceHandler
{
  internal static async Task RunOptions(TraceOptions options)
  {
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
    
    Log.Information("Tracing mutant execution.");
    var failedTests = await MutantTracerHarness.TraceExecutionForAllTests(
      options.AbsoluteTestProjectDirectory,
      options.AbsoluteExecutionTraceOutputDirectory,
      await ParseListOfTestsFile(options.AbsoluteListOfTestsFilePath),
      options.AbsoluteRunSettingsPath);

    Log.Information("Mutant tracing complete.");
    
    foreach (var failedTest in failedTests)
    {
      Log.Warning("Failed test: {FailedTestName}", failedTest);
    }

    if (failedTests.Count > 0)
    {
      Log.Warning("Remove the flaky tests that fail by default before proceeding.");
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
}