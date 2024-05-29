using System.Collections.Immutable;
using System.Diagnostics;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.FileSystem;
using MutateCSharp.MutationTesting;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.CLI;

internal static class TestHandler
{
  internal static async Task RunOptions(TestOptions options)
  {
    // (Re)construct the required ingredients to perform mutation testing
    var passingTestNamesSortedByDuration =
      await File.ReadAllLinesAsync(options.AbsolutePassingTestsFilePath);
    
    var passingTestCasesSortedByDuration =
      passingTestNamesSortedByDuration
      .Select(testName =>
        new TestCase(
          testName: testName,
          testProjectPath: options.AbsoluteTestProjectPath,
          runSettingsPath: options.AbsoluteRunSettingsPath)).ToImmutableArray();

    var mutationRegistry =
      await MutationRegistryPersister.ReconstructRegistryFromDisk(
        options.AbsoluteMutationRegistryPath);

    var mutantTraces =
      await MutantExecutionTraces.ReconstructTraceFromDisk(
        options.AbsoluteRecordedExecutionTraceDirectory,
        passingTestCasesSortedByDuration);
    
    // Sanity checks
    WarnIfExecutionTraceIsAbsent(ref passingTestCasesSortedByDuration, ref mutantTraces);

    var testHarness = new MutationTestHarness(passingTestCasesSortedByDuration, mutantTraces, mutationRegistry);
  
    // Run mutation testing
    var stopwatch = new Stopwatch();
    stopwatch.Start();
    var mutationTestResults = await testHarness.PerformMutationTesting();
    stopwatch.Stop();
    
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
}