using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.MutationTesting;

public sealed class MutationTestHarness
{
  private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
  private const int MaximumTimeoutScaleFactor = 3;

  private readonly ImmutableArray<TestCase> _testsSortedByDuration;
  private readonly MutantExecutionTraces _executionTraces;

  // One-to-many mapping from a file-scoped mutant activation environment variable
  // to the corresponding mutant/mutations within the mutated file.
  private readonly FrozenDictionary<string, FileLevelMutationRegistry>
    _mutantsByEnvVar;

  private readonly FrozenSet<MutantActivationInfo> _nonTraceableMutants;

  private readonly ConcurrentDictionary<MutantActivationInfo, byte>
    _coveredAndSurvivedMutants;

  private readonly ConcurrentDictionary<MutantActivationInfo, TestCase>
    _killedMutants;

  private readonly ConcurrentDictionary<MutantActivationInfo, TestCase>
    _timedOutMutants;

  private readonly ConcurrentDictionary<TestCase,
    FrozenDictionary<MutantActivationInfo, TestRunResult>> _mutationTestingResults;

  public MutationTestHarness(
    ImmutableArray<TestCase> testsSortedByDuration,
    MutantExecutionTraces executionTraces,
    ProjectLevelMutationRegistry mutationRegistry)
  {
    _testsSortedByDuration = testsSortedByDuration;
    _executionTraces = executionTraces;
    _mutantsByEnvVar = 
      mutationRegistry.ProjectRelativePathToRegistry
      .Values
      .ToFrozenDictionary(registry => registry.EnvironmentVariable,
        registry => registry);
    _killedMutants = 
      new ConcurrentDictionary<MutantActivationInfo, TestCase>();
    _timedOutMutants =
      new ConcurrentDictionary<MutantActivationInfo, TestCase>();
    _mutationTestingResults =
      new ConcurrentDictionary<TestCase,
        FrozenDictionary<MutantActivationInfo, TestRunResult>>();

    // Sanity check: each mutant trace entry has a corresponding entry in mutation registry
    var traceableMutants = _testsSortedByDuration
      .SelectMany(test => _executionTraces.GetCandidateMutantsForTestCase(test))
      .ToHashSet();

    var illegalMutant = traceableMutants.FirstOrDefault(
        mutant => !_mutantsByEnvVar.ContainsKey(mutant.EnvVar) || 
                  !_mutantsByEnvVar[mutant.EnvVar].Mutations.ContainsKey(mutant.MutantId));
    
    if (illegalMutant is not null)
    {
      throw new InvalidDataException(
        $"Found mutant {illegalMutant.MutantId} of {illegalMutant.EnvVar} in execution trace that do not belong to the supplied mutation registry.");
    }
    
    _coveredAndSurvivedMutants =
      new ConcurrentDictionary<MutantActivationInfo, byte>(
        traceableMutants.ToDictionary(mutant => mutant, _ => byte.MinValue));

    var allMutants = _mutantsByEnvVar.SelectMany(envVarToRegistry =>
      envVarToRegistry.Value.Mutations.Select(mutations =>
        new MutantActivationInfo(envVarToRegistry.Key, mutations.Key))).ToHashSet();

    _nonTraceableMutants = allMutants.Except(traceableMutants).ToFrozenSet();
  }
  
  public async Task<MutationTestResult> PerformMutationTesting()
  {
    // Flags mutants not traced by any tests early. Surfacing these mutants
    // early gives us more time to work on them.
    foreach (var nonTracedMutant in _nonTraceableMutants)
    {
      Log.Warning(
        "Mutant {MutantId} in {SourceFilePath} is not covered by any tests.",
        nonTracedMutant.MutantId, _mutantsByEnvVar[nonTracedMutant.EnvVar].FileRelativePath);
    }
    
    // Executes the test cases in order of ascending duration.
    // Note: the operation will execute at most ProcessorCount operations in parallel.
    var testsProcessed = 0;
    var testsToProcess = new ConcurrentQueue<TestCase>(_testsSortedByDuration);
    
    await Parallel.ForEachAsync(testsToProcess, async (test, cancellationToken) =>
    {
      var currentTestOrder = Interlocked.Increment(ref testsProcessed);
      
      Log.Information(
        "Processing test {TestName} ({CurrentCount}/{TotalCount} tests)",
        test.Name, currentTestOrder, _testsSortedByDuration.Length);
      
      // 1) Check if any mutants qualify as candidates
      var tracedMutants =
        _executionTraces.GetCandidateMutantsForTestCase(test);
      
      if (tracedMutants.Count == 0)
      {
        Log.Information(
          "Skipping {TestName} as no mutants were triggered in the test execution path.",
          test.Name);
        return;
      }
      
      var ignoredMutants =
        tracedMutants.Where(mutant => !_coveredAndSurvivedMutants.ContainsKey(mutant))
          .ToImmutableHashSet();
      
      foreach (var mutant in ignoredMutants)
      {
        Log.Information(
          "Skipping mutant {MutantId} in {SourceFile} as it was killed by another test.",
          mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath);
      }
      
      // 2) Randomly shuffle the execution order of candidate mutants 
      // This step is crucial to reduce overlapping work performed
      var candidateMutantsInterim = tracedMutants
        .Where(mutant => !ignoredMutants.Contains(mutant)).ToArray();
      Random.Shared.Shuffle(candidateMutantsInterim);
      var candidateMutants = candidateMutantsInterim.ToImmutableArray();

      // 2) Run the test without mutation to check for failures and record time taken
      var originalRunResult = await test.RunTestWithTimeout(DefaultTimeout);
      if (originalRunResult.testResult is not TestRunResult.Success)
      {
        Log.Information("Skipping {TestName} as it did not originally pass within the time constraint (default: 90 seconds).",
          test.Name);
        return;
      }

      // 3) Run the test with mutations to check for failures
      // Raise the timeout to be 3x the original timeout with a minimum timeout of 90 seconds
      var derivedTimeout =
        originalRunResult.timeTaken.Scale(MaximumTimeoutScaleFactor);
      if (derivedTimeout < DefaultTimeout) derivedTimeout = DefaultTimeout;

      var mutantRunResults =
        new ConcurrentBag<(
          MutantActivationInfo mutant, TestRunResult testResult, TimeSpan
          timeTaken)>();

      foreach (var mutant in candidateMutants)
      {
        var result = await test.RunTestWithTimeout(mutant.EnvVar,
          mutant.MutantId, derivedTimeout);

        // 4) Check result and mark test as killed or surviving
        if (result.testResult is TestRunResult.Failed)
        {
          _coveredAndSurvivedMutants.TryRemove(mutant, out _);
          if (!_killedMutants.TryAdd(mutant, test))
          {
            Log.Information("Another test has been registered that killed mutant {MutantId} in {SourceFile}.", 
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath);
          }
          else
          {
            Log.Information(
              "Mutant {MutantId} in {SourceFile} has been killed by test {TestName}.",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              test.Name);
          }
        }
        else if (result.testResult is TestRunResult.Timeout)
        {
          _coveredAndSurvivedMutants.TryRemove(mutant, out _);
          if (!_timedOutMutants.TryAdd(mutant, test))
          {
            Log.Information("Another test has been registered that caused timeout for mutant {MutantId} in {SourceFile}.", 
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath);
          }
          else
          {
            Log.Information(
              "Mutant {MutantId} in {SourceFile} timed out while running {TestName}.",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              test.Name);
          }
        }
        else if (result.testResult is TestRunResult.Success)
        {
          Log.Information(
            "Mutant {MutantId} in {SourceFile} survives after running {TestName}!",
            mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
            test.Name);
        }
        
        mutantRunResults.Add((mutant, result.testResult, result.timeTaken));
      }
      
      var results = mutantRunResults.ToDictionary(
        result => result.mutant,
        result => result.testResult);

      foreach (var mutant in ignoredMutants)
      {
        results[mutant] = TestRunResult.Skipped;
      }
      
      // 5) Record results for test case
      _mutationTestingResults[test] = results.ToFrozenDictionary();
    });
    
    // 6) Summarise all mutation testing at the end
    var mutantStatus = new Dictionary<MutantActivationInfo, MutantStatus>();
    foreach (var mutantEntry in _killedMutants)
    {
      mutantStatus[mutantEntry.Key] = MutantStatus.Killed;
    }
    foreach (var mutantEntry in _timedOutMutants)
    {
      mutantStatus[mutantEntry.Key] = MutantStatus.Timeout;
    }
    foreach (var mutant in _nonTraceableMutants)
    {
      mutantStatus[mutant] = MutantStatus.Uncovered;
    }
    foreach (var mutantEntry in _coveredAndSurvivedMutants)
    {
      mutantStatus[mutantEntry.Key] = MutantStatus.Survived;
    }

    return new MutationTestResult
    {
      MutantTestResultsOfTestCases = _mutationTestingResults
        .ToDictionary(testCaseToMutantResults => testCaseToMutantResults.Key.Name, 
          entry => entry.Value).ToFrozenDictionary(),
      MutantStatus = mutantStatus.ToFrozenDictionary()
    };
  }
}