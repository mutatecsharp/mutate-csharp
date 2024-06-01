using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.FileSystem;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.MutationTesting;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.CLI;

internal static class MutationTestHandler
{
  internal static async Task RunOptions(MutationTestOptions options)
  {
    var match = await CheckMutationRegistryMatches(
      options.AbsoluteMutationRegistryPath,
      options.AbsoluteExecutionTraceRegistryPath);

    if (!match)
    {
      Log.Error("The mutation registry does not match. Check again if both" +
                "registries are the result of the same optimisation level enabled.");
      return;
    }

    Log.Information("Dry run: {DryRun}", options.DryRun);

    // Rebuild the test project, as the test project assembly could be stale.
    if (!options.DryRun)
    {
      Log.Information("Building test project: {TestProjectPath}.",
        options.AbsoluteTestProjectPath);
      var buildExitCode =
        await DotnetUtil.Build(options.AbsoluteTestProjectPath);
      if (buildExitCode != 0)
      {
        Log.Error(
          "Test project cannot be rebuild. Perhaps the tracer generation/mutation failed?");
        return;
      }
    }
    
    // (Re)construct the required ingredients to perform mutation testing
    // Tests list
    var passingTestNamesSortedByDuration =
      await File.ReadAllLinesAsync(options.AbsolutePassingTestsFilePath);
    
    var passingTestCasesSortedByDuration =
      passingTestNamesSortedByDuration
        .Select(testName =>
          new TestCase(
            testName: testName,
            testProjectPath: options.AbsoluteTestProjectPath,
            runSettingsPath: options.AbsoluteRunSettingsPath)).ToImmutableArray();

    // Mutation registry
    var mutationRegistry =
      await MutationRegistryPersister.ReconstructRegistryFromDisk(
        options.AbsoluteMutationRegistryPath);
    var fileEnvVar = string.Empty;

    if (!string.IsNullOrEmpty(options.AbsoluteSourceFileUnderTestPath))
    {
      if (string.IsNullOrEmpty(options.AbsoluteProjectUnderTestPath))
      {
        throw new ArgumentException(
          "Project path must be specified if source file path is specified.");
      }
      
      // Derive relative path and create a mutation registry specific for the file
      var relativePath = Path.GetRelativePath(
        Path.GetDirectoryName(options.AbsoluteProjectUnderTestPath)!, 
        options.AbsoluteSourceFileUnderTestPath);
      
      mutationRegistry = new ProjectLevelMutationRegistry
      {
        ProjectRelativePathToRegistry = new Dictionary<string, FileLevelMutationRegistry>
        {
          [relativePath] = mutationRegistry.ProjectRelativePathToRegistry[relativePath]
        }.ToFrozenDictionary()
      };

      fileEnvVar = mutationRegistry.ProjectRelativePathToRegistry[relativePath]
        .EnvironmentVariable;
      Trace.Assert(fileEnvVar.Length > 0);
    }

    // Mutant execution trace
    // Filter the mutant traces with the environment variable corresponding
    // to the file
    var mutantTraces =
      string.IsNullOrEmpty(options.AbsoluteSourceFileUnderTestPath)
        ? await MutantExecutionTraces.ReconstructTraceFromDisk(
          options.AbsoluteRecordedExecutionTraceDirectory,
          passingTestCasesSortedByDuration)
        : await MutantExecutionTraces.ReconstructTraceFromDisk(
          options.AbsoluteRecordedExecutionTraceDirectory,
          passingTestCasesSortedByDuration,
          fileEnvVar);
    
    // Sanity checks
    WarnIfExecutionTraceIsAbsent(ref passingTestCasesSortedByDuration, ref mutantTraces);
    
    // Create directories if they don't exist
    Directory.CreateDirectory(options.AbsoluteTestMetadataDirectory);
    Directory.CreateDirectory(options.AbsoluteKilledMutantsMetadataDirectory);

    var testHarness = 
      new MutationTestHarness(
        testsSortedByDuration: passingTestCasesSortedByDuration, 
        executionTraces: mutantTraces, 
        mutationRegistry: mutationRegistry, 
        absoluteCompilationTemporaryDirectoryPath: options.AbsoluteCompilationArtifactDirectory,
        absoluteTestMetadataPath: options.AbsoluteTestMetadataDirectory,
        absoluteKilledMutantsMetadataPath: options.AbsoluteKilledMutantsMetadataDirectory,
        dryRun: options.DryRun
      );
  
    // Run mutation testing
    var stopwatch = new Stopwatch();
    stopwatch.Start();
    var mutationTestResults = await testHarness.PerformMutationTesting();
    stopwatch.Stop();

    if (options.DryRun) return;
    
    // Log results 
    Log.Information("Mutation testing completed.");
    Log.Information("Time elapsed: {ElapsedDays} day(s) {ElapsedHours} hour(s) {ElapsedMinutes} minute(s) {ElapsedSeconds} second(s)",
      stopwatch.Elapsed.Days, stopwatch.Elapsed.Hours, stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

    // Do some tallying (the mutants can only be in at most one status)
    var killedMutants = 
      mutationTestResults.GetMutantOfStatus(MutantStatus.Killed);
    var survivedMutants =
      mutationTestResults.GetMutantOfStatus(MutantStatus.Survived);
    var notTracedMutants =
      mutationTestResults.GetMutantOfStatus(MutantStatus.Uncovered);
    var timedOutMutants =
      mutationTestResults.GetMutantOfStatus(MutantStatus.Timeout);

    var totalEvaluatedMutants = killedMutants.Count + survivedMutants.Count +
                       notTracedMutants.Count + timedOutMutants.Count;
    var totalRegisteredMutants = mutationRegistry.GetTotalMutantCount();

    if (totalEvaluatedMutants != totalRegisteredMutants)
    {
      Log.Warning(
        "Evaluated a total of {EvaluatedMutantCount} mutants but there were {RegisteredMutantCount} mutants registered.",
        totalEvaluatedMutants, totalRegisteredMutants);
    }
    
    Log.Information("Total mutants: {TotalMutantCount}", totalEvaluatedMutants);
    Log.Information("Killed mutants: {KilledMutantCount}", killedMutants.Count);
    Log.Information("Survived mutants: {SurvivedMutantCount}", survivedMutants.Count);
    Log.Information("Timed out mutants: {TimedOutMutantCount}", timedOutMutants.Count);
    Log.Information("Non-traced mutants: {NonTracedMutantCount}", notTracedMutants.Count);
    
    // Persist results to disk
    var outputDirectory =
      Path.GetDirectoryName(options.AbsoluteTestProjectPath)!;
    var resultPath = await mutationTestResults.PersistToDisk(outputDirectory);
    Log.Information(
      "Mutation testing result has been persisted to {ResultPath}.", resultPath);
  }

  private static void WarnIfExecutionTraceIsAbsent(
    ref readonly ImmutableArray<TestCase> allTests,
    ref readonly MutantExecutionTraces allTraces)
  {
    var anyTraceRecorded = false;
    
    foreach (var testCase in allTests)
    {
      var traces = allTraces.GetCandidateMutantsForTestCase(testCase);
      anyTraceRecorded |= traces.Count != 0;
      
      if (traces.Count == 0)
      {
        Log.Warning(
          "The execution trace for test {TestName} has either not been recorded, or no candidate mutants have been found for this test case.",
          testCase.Name);
      }
    }

    if (!anyTraceRecorded)
      throw new ArgumentException(
        "Mutant execution trace directory does not contain any execution traces.");
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
}