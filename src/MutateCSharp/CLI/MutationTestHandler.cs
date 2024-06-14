using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Globalization;
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

    var specifiedMutants =
      GetAllSpecifiedMutants(options.AbsoluteSpecifiedMutantsListPath);
    
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
    var mutantTraces = await
      ReconstructExecutionTrace(
        traceDirectory: options.AbsoluteRecordedExecutionTraceDirectory,
        passingTestCasesSortedByDuration: passingTestCasesSortedByDuration,
        sourceFileUnderTestPath: options.AbsoluteSourceFileUnderTestPath, 
        fileEnvVar: fileEnvVar
        );
    
    // Create directories if they don't exist
    Directory.CreateDirectory(options.AbsoluteTestMetadataDirectory);
    Directory.CreateDirectory(options.AbsoluteKilledMutantsMetadataDirectory);

    var testHarness =
      mutantTraces is not null
        ? new MutationTestHarness(
          specifiedMutants: specifiedMutants,
          testsSortedByDuration: passingTestCasesSortedByDuration,
          executionTraces: mutantTraces,
          mutationRegistry: mutationRegistry,
          absoluteTestMetadataPath: options.AbsoluteTestMetadataDirectory,
          absoluteKilledMutantsMetadataPath: options
            .AbsoluteKilledMutantsMetadataDirectory,
          dryRun: options.DryRun
        )
        : new MutationTestHarness(
          specifiedMutants: specifiedMutants,
          testsSortedByDuration: passingTestCasesSortedByDuration,
          mutationRegistry: mutationRegistry,
          absoluteTestMetadataPath: options.AbsoluteTestMetadataDirectory,
          absoluteKilledMutantsMetadataPath: options
            .AbsoluteKilledMutantsMetadataDirectory,
          dryRun: options.DryRun
        );
  
    // Run mutation testing (results are persisted to disk)
    var stopwatch = new Stopwatch();
    stopwatch.Start();
    await testHarness.PerformMutationTesting();
    stopwatch.Stop();

    if (options.DryRun) return;
    
    // Log results 
    Log.Information("Mutation testing completed.");
    Log.Information("Time elapsed: {ElapsedDays} day(s) {ElapsedHours} hour(s) {ElapsedMinutes} minute(s) {ElapsedSeconds} second(s)",
      stopwatch.Elapsed.Days, stopwatch.Elapsed.Hours, stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

    // Analyse results
    var mutationTestResults =
      mutantTraces is not null
        ? await AnalyseMutationTestResultsWithRecordedTrace(
          passingTests: passingTestCasesSortedByDuration,
          mutationRegistry: mutationRegistry,
          executionTraces: mutantTraces,
          absoluteKilledMutantsMetadataPath:
          options.AbsoluteKilledMutantsMetadataDirectory
          )
        : await AnalyseMutationTestResults(
          passingTests: passingTestCasesSortedByDuration,
          mutationRegistry: mutationRegistry,
          absoluteKilledMutantsMetadataPath:
          options.AbsoluteKilledMutantsMetadataDirectory
        );
      
    // Do some tallying (the mutants can only be in at most one status)
    var killedMutants = 
      mutationTestResults.GetMutantOfStatus(MutantStatus.Killed);
    var survivedMutants =
      mutationTestResults.GetMutantOfStatus(MutantStatus.Survived);
    var notTracedMutants =
      mutationTestResults.GetMutantOfStatus(MutantStatus.Uncovered);
    var timedOutMutants =
      mutationTestResults.GetMutantOfStatus(MutantStatus.Timeout);
    var skippedMutants =
      mutationTestResults.GetMutantOfStatus(MutantStatus.Skipped);

    var totalEvaluatedMutants = killedMutants.Count + survivedMutants.Count +
                       notTracedMutants.Count + timedOutMutants.Count + skippedMutants.Count;
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
    Log.Information("Skipped mutants: {SkippedMutantCount}", skippedMutants.Count);
    Log.Information("Non-traced mutants: {NonTracedMutantCount}", notTracedMutants.Count);
    
    // Persist results to disk
    var outputDirectory =
      Path.GetDirectoryName(options.AbsoluteTestProjectPath)!;

    var killTrendPath = Path.Combine(outputDirectory, "kill-trend.csv");
    PersistMutantKillTrendAgainstTime(testHarness.AllKilledMutants, killTrendPath);
    
    var resultPath = await mutationTestResults.PersistToDisk(outputDirectory);
    Log.Information(
      "Mutation testing result has been persisted to {ResultPath}.", resultPath);
  }

  private static FrozenSet<MutantActivationInfo> GetAllSpecifiedMutants(
    string absoluteSpecifiedMutantsListPath)
  {
    if (string.IsNullOrEmpty(absoluteSpecifiedMutantsListPath)) return 
      FrozenSet<MutantActivationInfo>.Empty;
    
    var mutantTracesForTestCase = 
      File.ReadAllLines(absoluteSpecifiedMutantsListPath);

    try
    {
      var parsedTrace = mutantTracesForTestCase
        .Select(ParseRecordedTrace);

      return parsedTrace.ToFrozenSet();
    }
    catch (Exception)
    {
      Log.Error("Error while parsing execution trace for test {TestName}.",
        absoluteSpecifiedMutantsListPath);
      throw;
    }
      
    MutantActivationInfo ParseRecordedTrace(string trace)
    {
      var result = trace.Split(':');
    
      if (result.Length != 2 ||
          !result[0].StartsWith("MUTATE_CSHARP_ACTIVATED_MUTANT"))
      {
        throw new DataException($"Parse failed: {trace}");
      }
    
      return new MutantActivationInfo(
        EnvVar: result[0], MutantId: int.Parse(result[1]));
    }
  }

  private static async Task<MutantExecutionTraces?> 
    ReconstructExecutionTrace(
      string traceDirectory,
      ImmutableArray<TestCase> passingTestCasesSortedByDuration,
      string sourceFileUnderTestPath,
      string fileEnvVar)
  {
    if (string.IsNullOrEmpty(traceDirectory)) return null;
    
    var mutantTraces =
      string.IsNullOrEmpty(sourceFileUnderTestPath)
        ? await MutantExecutionTraces.ReconstructTraceFromDisk(
          traceDirectory,
          passingTestCasesSortedByDuration)
        : await MutantExecutionTraces.ReconstructTraceFromDisk(
          traceDirectory,
          passingTestCasesSortedByDuration,
          fileEnvVar);
    
    // Sanity checks
    WarnIfExecutionTraceIsAbsent(ref passingTestCasesSortedByDuration, ref mutantTraces);

    return mutantTraces;
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

  /*
   * Reconstructs results from individual tests and check if a mutant is killed.
   */
  private static async Task<MutationTestResult> 
    AnalyseMutationTestResultsWithRecordedTrace(
      ImmutableArray<TestCase> passingTests,
      ProjectLevelMutationRegistry mutationRegistry,
      MutantExecutionTraces executionTraces,
      string absoluteKilledMutantsMetadataPath)
  {
    var mutantStatuses =
      mutationRegistry.ProjectRelativePathToRegistry.Values
        .SelectMany(fileRegistry => fileRegistry.Mutations.Select(mutation =>
          new MutantActivationInfo(fileRegistry.EnvironmentVariable, mutation.Key)))
        .ToDictionary(mutant => mutant, _ => MutantStatus.Survived);

    var traceableMutants = passingTests
      .SelectMany(executionTraces.GetCandidateMutantsForTestCase)
      .ToHashSet();

    // 1) Identify and assign non-traceable mutants
    var nonTraceableMutants = mutantStatuses.Keys.Except(traceableMutants);
    foreach (var mutant in nonTraceableMutants)
    {
      mutantStatuses[mutant] = MutantStatus.Uncovered;
    }
    
    // 2) Identify and assign killed mutants
    // This information will have been persisted in killed mutants metadata directory
    foreach (var mutant in traceableMutants)
    {
      var mutantFileName = $"{mutant.EnvVar}-{mutant.MutantId}";
      var killedMutantMetadataFilePath =
        Path.Combine(absoluteKilledMutantsMetadataPath, mutantFileName, "kill_info.json");
      if (!Path.Exists(killedMutantMetadataFilePath)) continue;
      
      await using var fs =
        new FileStream(killedMutantMetadataFilePath, FileMode.Open, FileAccess.Read);
      var killResult = await ConverterUtil.DeserializeToAnonymousTypeAsync
        (fs, new { mutant = "", killed_by_test = "", kill_status = ""});
      if (killResult is null)
      {
        Log.Warning("Mutant {MutantInfo} marked as killed but JSON cannot be parsed.", $"{mutant.EnvVar}:{mutant.MutantId}");
        continue;
      }
      
      // Parse kill status back to enum
      if (!Enum.TryParse(typeof(MutantStatus), killResult.kill_status, out var killStatus))
      {
        Log.Warning("Mutant {MutantInfo} marked as killed but kill status cannot be parsed.", mutantFileName);
        continue;
      }

      // Update mutant kill status (timeout / fail)
      var status = (MutantStatus)killStatus;
      mutantStatuses[mutant] = status;
    }

    return new MutationTestResult
    {
      MutantStatus = mutantStatuses.ToFrozenDictionary()
    };
  }
  
  /*
   * Reconstructs results from individual tests and check if a mutant is killed.
   */
  private static async Task<MutationTestResult> 
    AnalyseMutationTestResults(
      ImmutableArray<TestCase> passingTests,
      ProjectLevelMutationRegistry mutationRegistry,
      string absoluteKilledMutantsMetadataPath)
  {
    var mutantStatuses =
      mutationRegistry.ProjectRelativePathToRegistry.Values
        .SelectMany(fileRegistry => fileRegistry.Mutations.Select(mutation =>
          new MutantActivationInfo(fileRegistry.EnvironmentVariable, mutation.Key)))
        .ToDictionary(mutant => mutant, _ => MutantStatus.Survived);
    
    var mutantsByEnvVar =
      mutationRegistry.ProjectRelativePathToRegistry
        .Values
        .ToFrozenDictionary(registry => registry.EnvironmentVariable,
          registry => registry);
    
    var allMutants = mutantsByEnvVar.SelectMany(envVarToRegistry =>
            envVarToRegistry.Value.Mutations.Select(mutations =>
              new MutantActivationInfo(envVarToRegistry.Key, mutations.Key)))
          .ToImmutableHashSet();
    
    // 2) Identify and record killed mutants based on directory
    // This information will have been persisted in killed mutants metadata directory
    foreach (var mutant in allMutants)
    {
      var mutantFileName = $"{mutant.EnvVar}-{mutant.MutantId}";
      var killedMutantMetadataFilePath =
        Path.Combine(absoluteKilledMutantsMetadataPath, mutantFileName, "kill_info.json");
      if (!Path.Exists(killedMutantMetadataFilePath)) continue;
      
      await using var fs =
        new FileStream(killedMutantMetadataFilePath, FileMode.Open, FileAccess.Read);
      var killResult = await ConverterUtil.DeserializeToAnonymousTypeAsync
        (fs, new { mutant = "", killed_by_test = "", kill_status = ""});
      if (killResult is null)
      {
        Log.Warning("Mutant {MutantInfo} marked as killed but JSON cannot be parsed.", $"{mutant.EnvVar}:{mutant.MutantId}");
        continue;
      }
      
      // Parse kill status back to enum
      if (!Enum.TryParse(typeof(MutantStatus), killResult.kill_status, out var killStatus))
      {
        Log.Warning("Mutant {MutantInfo} marked as killed but kill status cannot be parsed.", mutantFileName);
        continue;
      }

      // Update mutant kill status (timeout / fail)
      var status = (MutantStatus)killStatus;
      mutantStatuses[mutant] = status;
    }

    return new MutationTestResult
    {
      MutantStatus = mutantStatuses.ToFrozenDictionary()
    };
  }
    
  
  private static void PersistMutantKillTrendAgainstTime(
    FrozenDictionary<MutantActivationInfo, DateTime> allMutantsKilled,
    string outputPath) // csv
  {
    // 1) Sort by ascending kill time
    var sortedByKillTime =
      allMutantsKilled
        .OrderBy(mutantToKillTime => mutantToKillTime.Value)
        .Select(mutantToKillTime =>
          (mutantToKillTime.Key,
            mutantToKillTime.Value.ToString("o", CultureInfo.InvariantCulture))
        )
        .ToImmutableArray();

    Log.Information("Logging kill trend to {Path}.", outputPath);

    using (var writer = new StreamWriter(outputPath))
    {
      writer.WriteLine("Mutant,KilledTimestamp");
      foreach (var (mutant, formattedTimestamp) in sortedByKillTime)
      {
        var eventName =
          $"{mutant.EnvVar}:{mutant.MutantId}";
        writer.WriteLine($"{eventName},{formattedTimestamp}");
      }
    }

    Log.Information("Successfully logged kill trend.");
  }
}