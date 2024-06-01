using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.FileSystem;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.MutationTesting;

public sealed class MutationTestHarness
{
  private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);
  private const int MaximumTimeoutScaleFactor = 3;
  private readonly bool _dryRun;

  private readonly ImmutableArray<TestCase> _testsSortedByDuration;
  private readonly MutantExecutionTraces _executionTraces;

  // One-to-many mapping from a file-scoped mutant activation environment variable
  // to the corresponding mutant/mutations within the mutated file.
  private readonly FrozenDictionary<string, FileLevelMutationRegistry>
    _mutantsByEnvVar;

  private readonly FrozenSet<MutantActivationInfo> _nonTraceableMutants;

  // Mutants that survived after the mutation test campaign.
  private readonly ConcurrentDictionary<MutantActivationInfo, byte>
    _coveredAndSurvivedMutants;

  // Mutants that are independently discovered to be killed or timed out.
  private readonly ConcurrentDictionary<MutantActivationInfo, byte>
    _skippedMutants;

  // Mutants that are killed during the mutation test campaign.
  private readonly ConcurrentDictionary<MutantActivationInfo, TestCase>
    _killedMutants;

  // Mutants that timed out during the mutation test campaign.
  // We treat these mutants as killed.
  private readonly ConcurrentDictionary<MutantActivationInfo, TestCase>
    _timedOutMutants;

  private readonly ConcurrentDictionary<TestCase,
      FrozenDictionary<MutantActivationInfo, TestRunResult>>
    _mutationTestingResults;

  private readonly string _absoluteCompilationArtifactPath;
  private readonly string _absoluteTestMetadataPath;
  private readonly string _absoluteKilledMutantsMetadataPath;

  private readonly int _totalTraceableMutantCount;

  public MutationTestHarness(
    ImmutableArray<TestCase> testsSortedByDuration,
    MutantExecutionTraces executionTraces,
    ProjectLevelMutationRegistry mutationRegistry,
    string absoluteCompilationTemporaryDirectoryPath,
    string absoluteTestMetadataPath,
    string absoluteKilledMutantsMetadataPath,
    bool dryRun)
  {
    _dryRun = dryRun;
    _testsSortedByDuration = testsSortedByDuration;
    _executionTraces = executionTraces;
    _mutantsByEnvVar =
      mutationRegistry.ProjectRelativePathToRegistry
        .Values
        .ToFrozenDictionary(registry => registry.EnvironmentVariable,
          registry => registry);
    _skippedMutants =
      new ConcurrentDictionary<MutantActivationInfo, byte>();
    _killedMutants =
      new ConcurrentDictionary<MutantActivationInfo, TestCase>();
    _timedOutMutants =
      new ConcurrentDictionary<MutantActivationInfo, TestCase>();
    _mutationTestingResults =
      new ConcurrentDictionary<TestCase,
        FrozenDictionary<MutantActivationInfo, TestRunResult>>();
    _absoluteCompilationArtifactPath =
      absoluteCompilationTemporaryDirectoryPath;
    _absoluteTestMetadataPath = absoluteTestMetadataPath;
    _absoluteKilledMutantsMetadataPath = absoluteKilledMutantsMetadataPath;

    // Sanity check: each mutant trace entry has a corresponding entry in mutation registry
    var traceableMutants = _testsSortedByDuration
      .SelectMany(test => _executionTraces.GetCandidateMutantsForTestCase(test))
      .ToHashSet();

    var illegalMutant = traceableMutants.FirstOrDefault(
      mutant => !_mutantsByEnvVar.ContainsKey(mutant.EnvVar) ||
                !_mutantsByEnvVar[mutant.EnvVar].Mutations
                  .ContainsKey(mutant.MutantId));

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
          new MutantActivationInfo(envVarToRegistry.Key, mutations.Key)))
      .ToHashSet();

    _nonTraceableMutants = allMutants.Except(traceableMutants).ToFrozenSet();
    _totalTraceableMutantCount = traceableMutants.Count;
  }

  public async Task<MutationTestResult> PerformMutationTesting()
  {
    // Flags mutants not traced by any tests early. Surfacing these mutants
    // early gives us more time to work on them.
    foreach (var nonTracedMutant in _nonTraceableMutants)
    {
      Log.Warning(
        "Mutant {MutantId} in {SourceFilePath} is not covered by any tests.",
        nonTracedMutant.MutantId,
        _mutantsByEnvVar[nonTracedMutant.EnvVar].FileRelativePath);
    }

    Log.Information(
      "{TraceableMutantCount} out of {TotalMutantCount} mutants are traceable.",
      _totalTraceableMutantCount,
      _mutantsByEnvVar.Values
        .Select(registry => registry.Mutations.Count).Sum());

    Log.Information(
      "{RelevantTestCount} out of {TotalTestCount} tests cover one or more mutants.",
      _testsSortedByDuration
        .Select(test => _executionTraces.GetCandidateMutantsForTestCase(test))
        .Count(candidates => candidates.Count > 0),
      _testsSortedByDuration.Length);

    if (_dryRun)
    {
      return new MutationTestResult
      {
        MutantTestResultsOfTestCases = FrozenDictionary<string,
          FrozenDictionary<MutantActivationInfo, TestRunResult>>.Empty,
        MutantStatus =
          FrozenDictionary<MutantActivationInfo, MutantStatus>.Empty
      };
    }

    // Executes the test cases in order of ascending duration.
    for (var i = 0; i < _testsSortedByDuration.Length; i++)
    {
      var testCase = _testsSortedByDuration[i];
      Log.Information(
        "Processing test {TestName} ({CurrentCount}/{TotalCount} tests)",
        testCase.Name, i + 1, _testsSortedByDuration.Length);
      Log.Information(
        "Live mutants: {SurvivedMutantCount} | Killed mutants: {KilledMutantCount} | Timed out mutants: {TimedOutMutantCount} | Total traceable mutants: {TraceableMutantCount} | Untraceable mutants: {UntraceableMutantCount}",
        _coveredAndSurvivedMutants.Count,
        _killedMutants.Count,
        _timedOutMutants.Count,
        _totalTraceableMutantCount,
        _nonTraceableMutants.Count);

      // 1) Check if the current test has been evaluated or are under evaluation.
      // We perform the check using persisted metadata; this allows us to recover
      // from crashes and start at the same spot where we left off.
      var testCaseMetadataDir = Path.Combine(_absoluteTestMetadataPath,
        TestCaseUtil.ValidTestFileName(testCase.Name));

      // Note: this check is not thread-safe, and assumes only this program
      // instance is working on mutation testing.
      if (Directory.Exists(testCaseMetadataDir))
      {
        Log.Information(
          "Skipping {TestName} as it is under evaluation or has been evaluated.",
          testCase.Name);
        continue;
      }

      Directory.CreateDirectory(testCaseMetadataDir);

      // 2) Check if any mutants qualify as candidates
      var tracedMutants =
        _executionTraces.GetCandidateMutantsForTestCase(testCase);

      if (tracedMutants.Count == 0)
      {
        Log.Information(
          "Skipping {TestName} as no mutants were triggered in the test execution path.",
          testCase.Name);
        continue;
      }

      var ignoredMutants =
        tracedMutants.Where(mutant =>
            !_coveredAndSurvivedMutants.ContainsKey(mutant))
          .ToImmutableHashSet();

      foreach (var mutant in ignoredMutants)
      {
        Log.Information(
          "Skipping mutant {MutantId} in {SourceFile} as it was evaluated as killed/timed out.",
          mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath);
      }

      var candidateMutants = tracedMutants
        .Where(mutant => !ignoredMutants.Contains(mutant)).ToImmutableHashSet();

      // 3) Run the test without mutation to check for failures and record time taken
      var originalRunResult = await testCase.RunTestWithTimeout(DefaultTimeout);
      if (originalRunResult.testResult is not TestRunResult.Success)
      {
        Log.Information("Skipping {TestName} as it did not originally pass.",
          testCase.Name);
        continue;
      }

      // 4) Concurrently run the test with mutations to check for failures
      // Raise the timeout to be 3x the original timeout with a minimum timeout of 90 seconds
      var derivedTimeout =
        originalRunResult.timeTaken.Scale(MaximumTimeoutScaleFactor);
      if (derivedTimeout < DefaultTimeout) derivedTimeout = DefaultTimeout;

      var mutantRunResults =
        new ConcurrentBag<(MutantActivationInfo mutant, TestRunResult testResult
          )>();

      // Note: the operation will execute at most ProcessorCount operations in parallel.
      await Parallel.ForEachAsync(candidateMutants,
        async (mutant, cancellationToken) =>
        {
          // 5) Check if mutant is already killed.
          // Important to check that the python script also has the same name.
          var mutantFileName = $"{mutant.EnvVar}-{mutant.MutantId}";
          var killedMutantMetadataDir =
            Path.Combine(_absoluteKilledMutantsMetadataPath, mutantFileName);

          if (Directory.Exists(killedMutantMetadataDir))
          {
            Log.Information(
              "Skipping mutant {MutantId} in {SourceFile} as it has been killed.",
              mutant.MutantId,
              _mutantsByEnvVar[mutant.EnvVar].FileRelativePath);
            mutantRunResults.Add((mutant, TestRunResult.Skipped));
            return;
          }

          var result = await testCase.RunTestWithTimeout(mutant.EnvVar,
            mutant.MutantId, derivedTimeout);
          mutantRunResults.Add((mutant, result.testResult));
          if (result.testResult is TestRunResult.Success)
          {
            Log.Information(
              "Mutant {MutantId} in {SourceFile} survives after running {TestName}!",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              testCase.Name);
            return;
          }

          // 6) Persist individual mutant kill result.
          // Note: since all mutants are only tested at most once concurrently,
          // we don't have to check if the mutant is killed again.
          var jsonData = new
          {
            mutant = $"{mutant.EnvVar}:{mutant.MutantId}",
            killed_by_test = testCase.Name,
            kill_status = result.testResult.ToString()
          };

          var killMetadataPath =
            Path.Combine(killedMutantMetadataDir, "kill_info.json");

          try
          {
            Directory.CreateDirectory(killedMutantMetadataDir);
            Log.Information(
              "Persisting kill information of mutant {MutantId} in {SourceFile} to {Path}.",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              killMetadataPath);
            await File.WriteAllTextAsync(killMetadataPath,
              JsonSerializer.Serialize(jsonData), cancellationToken);
          }
          catch (Exception)
          {
            Log.Error(
              "Kill information could not be recorded for mutant {MutantId} in {SourceFile} against test {TestName}.",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              testCase.Name);
          }

          // 5) Check result and mark test as killed or surviving
          if (result.testResult is TestRunResult.Failed)
          {
            Log.Information(
              "Mutant {MutantId} in {SourceFile} has been killed by test {TestName}.",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              testCase.Name);
          }
          else if (result.testResult is TestRunResult.Timeout)
          {
            Log.Information(
              "Mutant {MutantId} in {SourceFile} timed out while running {TestName}.",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              testCase.Name);
          }
        });

      // 7) Record results (put the killed mutants in corresponding bins)
      foreach (var runResult in mutantRunResults)
      {
        switch (runResult.testResult)
        {
          case TestRunResult.Failed:
            _coveredAndSurvivedMutants.TryRemove(runResult.mutant, out _);
            _killedMutants.TryAdd(runResult.mutant, testCase);
            break;
          case TestRunResult.Timeout:
            _coveredAndSurvivedMutants.TryRemove(runResult.mutant, out _);
            _timedOutMutants.TryAdd(runResult.mutant, testCase);
            break;
          case TestRunResult.Skipped:
            _coveredAndSurvivedMutants.TryRemove(runResult.mutant, out _);
            _skippedMutants.TryAdd(runResult.mutant, byte.MinValue);
            break;
        }
      }

      var results = mutantRunResults.ToDictionary(
        result => result.mutant,
        result => result.testResult);

      foreach (var mutant in ignoredMutants)
      {
        results[mutant] = TestRunResult.Skipped;
      }

      _mutationTestingResults[testCase] = results.ToFrozenDictionary();

      // Sanity check: result list should have the same mutants as traced mutant list
      if (tracedMutants.Count != results.Count)
      {
        throw new DataException(
          $"Number of evaluated mutants do not match number of" +
          $"traced mutants for test {testCase.Name}");
      }

      foreach (var mutantInfo in results.Keys.Where(mutantInfo => !tracedMutants.Contains(mutantInfo)))
      {
        throw new DataException(
          $"Mutant {mutantInfo.MutantId} in {_mutantsByEnvVar[mutantInfo.EnvVar].FileRelativePath} is traced by test {testCase.Name} but not evaluated.");
      }

      // 8) Persist current test case result summary.
      var testSummary = new
      {
        test_name = testCase.Name,
        killed_mutants = results
          .Where(entry =>
            entry.Value is TestRunResult.Failed or TestRunResult.Timeout)
          .Select(entry => $"{entry.Key.EnvVar}:{entry.Key.MutantId}")
          .ToArray(),
        skipped_mutants = results
          .Where(entry => entry.Value is TestRunResult.Skipped)
          .Select(entry => $"{entry.Key.EnvVar}:{entry.Key.MutantId}")
          .ToArray(),
        survived_mutants = results
          .Where(entry => entry.Value is TestRunResult.Success)
          .Select(entry => $"{entry.Key.EnvVar}:{entry.Key.MutantId}")
          .ToArray(),
        covered_mutants = results.Select(entry =>
            $"{entry.Key.EnvVar}:{entry.Key.MutantId}")
          .ToArray()
      };

      var testSummaryPath =
        Path.Combine(testCaseMetadataDir, "test-summary.json");

      try
      {
        Log.Information(
          "Persisting test result summary of {TestName} to {Path}.",
          testCase.Name, testSummaryPath);
        await File.WriteAllTextAsync(testSummaryPath,
          JsonSerializer.Serialize(testSummary));
      }
      catch (Exception)
      {
        Log.Error(
          "Test summary cannot be recorded for test {TestName}.",
          testCase.Name);
      }

      // Delete compilation artifacts.
      DirectoryCleanup.DeleteAllDirectoryContents(
        _absoluteCompilationArtifactPath);
    }

    // 9) Summarise all mutation testing at the end.
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

    foreach (var mutantEntry in _skippedMutants)
    {
      mutantStatus[mutantEntry.Key] = MutantStatus.Skipped;
    }

    return new MutationTestResult
    {
      MutantTestResultsOfTestCases = _mutationTestingResults
        .ToDictionary(
          testCaseToMutantResults => testCaseToMutantResults.Key.Name,
          entry => entry.Value).ToFrozenDictionary(),
      MutantStatus = mutantStatus.ToFrozenDictionary()
    };
  }
}