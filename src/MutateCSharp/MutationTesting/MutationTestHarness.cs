using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Text.Json;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.MutationTesting;

public sealed class MutationTestHarness
{
  private static readonly JsonSerializerOptions JsonOptions =
    new() { WriteIndented = true };

  private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
  private const int MaximumTimeoutScaleFactor = 3;
  private readonly bool _dryRun;

  private readonly ImmutableArray<TestCase> _testsSortedByDuration;
  private readonly MutantExecutionTraces? _executionTraces;

  // One-to-many mapping from a file-scoped mutant activation environment variable
  // to the corresponding mutant/mutations within the mutated file.
  private readonly FrozenDictionary<string, FileLevelMutationRegistry>
    _mutantsByEnvVar;

  // Mutants that survived after the mutation test campaign.
  // Used to maintain thread-safety of the analysis.
  private readonly ConcurrentDictionary<MutantActivationInfo, byte>
    _coveredAndSurvivedMutants;

  // For diagnostics purposes.
  private readonly ConcurrentDictionary<MutantActivationInfo, DateTime>
    _timedOutMutants;

  // For diagnostic purposes.
  private readonly ConcurrentDictionary<MutantActivationInfo, DateTime>
    _failedMutants;

  private readonly string _absoluteTestMetadataPath;
  private readonly string _absoluteKilledMutantsMetadataPath;

  private readonly int _allMutantCount;
  private readonly int _totalTraceableMutantCount;
  private readonly int _totalNonTraceableMutantCount;

  public FrozenDictionary<MutantActivationInfo, DateTime> AllKilledMutants
    => _failedMutants.Union(_timedOutMutants).ToFrozenDictionary();

  /*
   * Without execution trace; cannot perform optimisation.
   */
  public MutationTestHarness(
    FrozenSet<MutantActivationInfo> specifiedMutants,
    ImmutableArray<TestCase> testsSortedByDuration,
    ProjectLevelMutationRegistry mutationRegistry,
    string absoluteTestMetadataPath,
    string absoluteKilledMutantsMetadataPath,
    bool dryRun)
  {
    _dryRun = dryRun;
    _testsSortedByDuration = testsSortedByDuration;
    _mutantsByEnvVar =
      mutationRegistry.ProjectRelativePathToRegistry
        .Values
        .ToFrozenDictionary(registry => registry.EnvironmentVariable,
          registry => registry);
    _failedMutants =
      new ConcurrentDictionary<MutantActivationInfo, DateTime>();
    _timedOutMutants =
      new ConcurrentDictionary<MutantActivationInfo, DateTime>();
    _absoluteTestMetadataPath = absoluteTestMetadataPath;
    _absoluteKilledMutantsMetadataPath = absoluteKilledMutantsMetadataPath;

    // Create mutant activation information
    var allMutants = _mutantsByEnvVar.SelectMany(envVarToRegistry =>
        envVarToRegistry.Value.Mutations.Select(mutations =>
          new MutantActivationInfo(envVarToRegistry.Key, mutations.Key)))
      .ToHashSet();

    if (specifiedMutants.Count > 0)
    {
      foreach (var mutant in specifiedMutants)
      {
        Trace.Assert(allMutants.Contains(mutant));
      }

      allMutants = specifiedMutants.ToHashSet();
    }

    _coveredAndSurvivedMutants =
      new ConcurrentDictionary<MutantActivationInfo, byte>(
        allMutants.ToDictionary(mutant => mutant, _ => byte.MinValue));

    _allMutantCount = allMutants.Count;
    // Assume all mutants traceable (not reliable)
    _totalTraceableMutantCount = _allMutantCount;
    _totalNonTraceableMutantCount = 0;
  }

  public MutationTestHarness(
    FrozenSet<MutantActivationInfo> specifiedMutants,
    ImmutableArray<TestCase> testsSortedByDuration,
    MutantExecutionTraces executionTraces,
    ProjectLevelMutationRegistry mutationRegistry,
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
    _failedMutants =
      new ConcurrentDictionary<MutantActivationInfo, DateTime>();
    _timedOutMutants =
      new ConcurrentDictionary<MutantActivationInfo, DateTime>();
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

    if (specifiedMutants.Count > 0)
    {
      // sanity check
      foreach (var mutant in specifiedMutants)
      {
        Trace.Assert(allMutants.Contains(mutant));
      }

      // removed from covered and survived
      foreach (var (mutant, _) in _coveredAndSurvivedMutants)
      {
        if (!specifiedMutants.Contains(mutant))
        {
          _coveredAndSurvivedMutants.TryRemove(mutant, out _);
        }
      }

      // removed from traced
      foreach (var mutant in traceableMutants.Where(mutant =>
                 !specifiedMutants.Contains(mutant)))
      {
        traceableMutants.Remove(mutant);
      }

      allMutants = specifiedMutants.ToHashSet();
    }

    var nonTraceableMutants = allMutants.Except(traceableMutants).ToFrozenSet();
    _allMutantCount = allMutants.Count;
    _totalTraceableMutantCount = traceableMutants.Count;
    _totalNonTraceableMutantCount = nonTraceableMutants.Count;

    // Flags mutants not traced by any tests early. Surfacing these mutants
    // early gives us more time to work on them.
    foreach (var nonTracedMutant in nonTraceableMutants)
    {
      Log.Warning(
        "Mutant {MutantId} in {SourceFilePath} is not covered by any tests.",
        nonTracedMutant.MutantId,
        _mutantsByEnvVar[nonTracedMutant.EnvVar].FileRelativePath);
    }

    Log.Information(
      "{TraceableMutantCount} out of {TotalMutantCount} mutants are traceable.",
      _totalTraceableMutantCount, _allMutantCount);

    Log.Information(
      "{RelevantTestCount} out of {TotalTestCount} tests cover one or more mutants.",
      _testsSortedByDuration
        .Select(test => _executionTraces.GetCandidateMutantsForTestCase(test))
        .Count(candidates => candidates.Count > 0),
      _testsSortedByDuration.Length);
  }

  public async Task PerformMutationTesting()
  {
    if (_dryRun) return;

    // Executes the test cases in order of ascending duration.
    // Note: a single test cannot be run concurrently but different tests can
    // be run concurrently, to avoid concurrency bugs present in underlying tests
    var testsProcessed = 0;
    var testsToProcess = new ConcurrentQueue<TestCase>(_testsSortedByDuration);

    await Parallel.ForEachAsync(testsToProcess,
      async (testCase, cancellationToken) =>
      {
        var currentTestOrder = Interlocked.Increment(ref testsProcessed);

        // 1) Check if the current test has been evaluated or are under evaluation.
        // We perform the check using persisted metadata; this allows us to recover
        // from crashes and start at the same spot where we left off.
        var testCaseMetadataDir = Path.Combine(_absoluteTestMetadataPath,
          TestCaseUtil.ValidTestFileName(testCase.Name));

        // Each test gets assigned to at most one thread so this check is safe
        if (Directory.Exists(testCaseMetadataDir))
        {
          Log.Information(
            "Skipping {TestName} as it has been evaluated.",
            testCase.Name);
          return;
        }

        Directory.CreateDirectory(testCaseMetadataDir);

        // 2) Check if any mutants qualify as candidates
        var tracedMutants =
          _executionTraces?.GetCandidateMutantsForTestCase(testCase);

        if (_executionTraces is not null && tracedMutants?.Count == 0)
        {
          Log.Information(
            "Skipping {TestName} as no mutants were triggered in the test execution path.",
            testCase.Name);
          return;
        }

        // Default: ignore failed / timed out mutants (not atomic but is thread-safe)
        var ignoredMutants =
          tracedMutants?.Where(mutant =>
              !_coveredAndSurvivedMutants.ContainsKey(mutant))
            .ToImmutableHashSet()
          ?? _failedMutants.Keys.Union(_timedOutMutants.Keys)
            .ToImmutableHashSet();

        // 3) Log information about current state for diagnostic purposes.
        foreach (var mutant in ignoredMutants)
        {
          Log.Information(
            "Skipping mutant {MutantId} in {SourceFile} as it was evaluated as killed/timed out.",
            mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath);
        }

        Log.Information(
          "Processing test {TestName} ({CurrentCount}/{TotalCount} tests)",
          testCase.Name, currentTestOrder, _testsSortedByDuration.Length);

        // Diagnostic report
        if (_executionTraces is not null)
        {
          Log.Information(
            "[Current session] Live mutants: {SurvivedMutantCount} | Failed mutants: {KilledMutantCount} | Timed out mutants: {TimedOutMutantCount} | Total traceable mutants: {TraceableMutantCount} | Untraceable mutants: {UntraceableMutantCount}",
            _coveredAndSurvivedMutants.Count,
            _failedMutants.Count,
            _timedOutMutants.Count,
            _totalTraceableMutantCount,
            _totalNonTraceableMutantCount);
        }
        else
        {
          Log.Information(
            "[Current session] Live mutants: {SurvivedMutantCount} | Failed mutants: {KilledMutantCount} | Timed out mutants: {TimedOutMutantCount}",
            _coveredAndSurvivedMutants.Count,
            _failedMutants.Count,
            _timedOutMutants.Count);
        }

        // 4) Run the test without mutation to check for failures and record time taken
        var originalRunResult =
          await testCase.RunTestWithTimeout(DefaultTimeout);

        if (originalRunResult.testResult is not TestRunResult.Success)
        {
          Log.Information("Skipping {TestName} as it did not originally pass.",
            testCase.Name);

          // Persist this information in disk
          var jsonTestFailData = new
          {
            test_name = testCase.Name,
            test_result = originalRunResult.testResult.ToString()
          };

          var testFailMetadataPath =
            Path.Combine(testCaseMetadataDir, "test_failed.json");

          Log.Information(
            "Persisting failure of test {TestName} to {Path}.",
            testCase.Name, testFailMetadataPath);

          try
          {
            File.WriteAllText(testFailMetadataPath,
              JsonSerializer.Serialize(jsonTestFailData, JsonOptions));
          }
          catch (Exception)
          {
            Log.Error(
              "Test fail information could not be recorded for test {TestName}.",
              testCase.Name);
          }

          return;
        }

        // TODO: change this to be injected / allow specify
        var candidateMutants =
          tracedMutants
            ?.Where(mutant =>
              !ignoredMutants.Contains(mutant))
            .ToImmutableHashSet()
          ?? _coveredAndSurvivedMutants.Keys
            .ToImmutableHashSet();

        // 5) Run the test with mutations to check for failures
        // Raise the timeout to be 3x the original timeout with a minimum timeout of 60 seconds
        var derivedTimeout =
          originalRunResult.timeTaken.Scale(MaximumTimeoutScaleFactor);
        if (derivedTimeout < DefaultTimeout) derivedTimeout = DefaultTimeout;

        var mutantRunResults =
          new ConcurrentBag<
            (MutantActivationInfo mutant, MutantStatus mutantStatus)>();

        foreach (var mutant in candidateMutants)
        {
          // 6) Check if mutant is already killed.
          // As the update is atomic this check is thread safe.
          if (!_coveredAndSurvivedMutants.ContainsKey(mutant))
          {
            Log.Information(
              "Skipping mutant {MutantId} in {SourceFile} as it has been killed.",
              mutant.MutantId,
              _mutantsByEnvVar[mutant.EnvVar].FileRelativePath);
            mutantRunResults.Add((mutant, MutantStatus.Skipped));
            continue;
          }

          var result = await testCase.RunTestWithTimeout(mutant.EnvVar,
            mutant.MutantId, derivedTimeout);

          var mutantStatus = result.testResult switch
          {
            TestRunResult.Failed => MutantStatus.Killed,
            TestRunResult.Timeout => MutantStatus.Timeout,
            TestRunResult.Success => MutantStatus.Survived,
            TestRunResult.Skipped => MutantStatus.Skipped,
          };

          mutantRunResults.Add((mutant, mutantStatus));

          if (mutantStatus is MutantStatus.Survived)
          {
            Log.Information(
              "Mutant {MutantId} in {SourceFile} survives after running {TestName}!",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              testCase.Name);
            continue;
          }

          // Test is killed!
          // 7) Persist individual mutant kill result.
          if (_coveredAndSurvivedMutants.TryRemove(mutant, out _))
          {
            var killedAtTime = DateTime.UtcNow;

            // 8) Update diagnostics
            if (mutantStatus is MutantStatus.Killed)
            {
              _failedMutants.TryAdd(mutant, killedAtTime);
              Log.Information(
                "Mutant {MutantId} in {SourceFile} has been killed by test {TestName}.",
                mutant.MutantId,
                _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
                testCase.Name);
            }
            else if (mutantStatus is MutantStatus.Timeout)
            {
              _timedOutMutants.TryAdd(mutant, killedAtTime);
              Log.Information(
                "Mutant {MutantId} in {SourceFile} timed out while running {TestName}.",
                mutant.MutantId,
                _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
                testCase.Name);
            }

            var jsonKillData = new
            {
              mutant = $"{mutant.EnvVar}:{mutant.MutantId}",
              killed_by_test = testCase.Name,
              kill_status = mutantStatus.ToString()
            };

            // Important to check that the python script also has the same name.
            var mutantFileName = $"{mutant.EnvVar}-{mutant.MutantId}";
            var killedMutantMetadataDir =
              Path.Combine(_absoluteKilledMutantsMetadataPath, mutantFileName);
            var killMetadataPath =
              Path.Combine(killedMutantMetadataDir, "kill_info.json");

            Log.Information(
              "Persisting kill information of mutant {MutantId} in {SourceFile} to {Path}.",
              mutant.MutantId, _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
              killMetadataPath);

            try
            {
              Directory.CreateDirectory(killedMutantMetadataDir);
              File.WriteAllText(killMetadataPath,
                JsonSerializer.Serialize(jsonKillData, JsonOptions));
            }
            catch (Exception e)
            {
              Log.Error(
                "Kill information could not be recorded for mutant {MutantId} in {SourceFile} against test {TestName}: {ExceptionMessage}",
                mutant.MutantId,
                _mutantsByEnvVar[mutant.EnvVar].FileRelativePath,
                testCase.Name, e.Message);
            }
          }
          else
          {
            Log.Information(
              "Mutant {MutantId} in {SourceFile} has been killed by another test.",
              mutant.MutantId,
              _mutantsByEnvVar[mutant.EnvVar].FileRelativePath);
          }
        }

        var results = mutantRunResults.ToDictionary(
          result => result.mutant,
          result => result.mutantStatus);

        foreach (var mutant in ignoredMutants)
        {
          results[mutant] = MutantStatus.Skipped;
        }

        // Sanity check: result list should have the same mutants as traced mutant list
        if (_executionTraces is not null)
        {
          if (tracedMutants?.Count != results.Count)
          {
            throw new DataException(
              $"Number of evaluated mutants do not match number of" +
              $"traced mutants for test {testCase.Name}");
          }

          foreach (var mutantInfo in results.Keys.Where(mutantInfo =>
                     !tracedMutants.Contains(mutantInfo)))
          {
            throw new DataException(
              $"Mutant {mutantInfo.MutantId} in {_mutantsByEnvVar[mutantInfo.EnvVar].FileRelativePath} is traced by test {testCase.Name} but not evaluated.");
          }
        }

        // 9) Persist current test case result summary.
        var testSummary = new
        {
          test_name = testCase.Name,
          killed_mutants = results
            .Where(entry =>
              entry.Value is MutantStatus.Killed or MutantStatus.Timeout)
            .Select(entry => $"{entry.Key.EnvVar}:{entry.Key.MutantId}")
            .ToArray(),
          skipped_mutants = results
            .Where(entry => entry.Value is MutantStatus.Skipped)
            .Select(entry => $"{entry.Key.EnvVar}:{entry.Key.MutantId}")
            .ToArray(),
          survived_mutants = results
            .Where(entry => entry.Value is MutantStatus.Survived)
            .Select(entry => $"{entry.Key.EnvVar}:{entry.Key.MutantId}")
            .ToArray(),
          covered_mutants = results.Select(entry =>
              $"{entry.Key.EnvVar}:{entry.Key.MutantId}")
            .ToArray()
        };

        var testSummaryPath =
          Path.Combine(testCaseMetadataDir, "test-summary.json");

        Log.Information(
          "Persisting test result summary of {TestName} to {Path}.",
          testCase.Name, testSummaryPath);

        try
        {
          File.WriteAllText(testSummaryPath,
            JsonSerializer.Serialize(testSummary, JsonOptions));
        }
        catch (Exception)
        {
          Log.Error(
            "Test summary cannot be recorded for test {TestName}.",
            testCase.Name);
        }
      });
  }
}