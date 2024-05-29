using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using MutateCSharp.MutationTesting;
using MutateCSharp.Util;

namespace MutateCSharp.ExecutionTracing;

public sealed class MutantExecutionTraces
{
  // Each test case has a list of mutants to be activated
  private readonly FrozenDictionary<TestCase, FrozenSet<MutantActivationInfo>>
    _executionTraces;

  private MutantExecutionTraces(
    FrozenDictionary<TestCase, FrozenSet<MutantActivationInfo>> traces)
  {
    _executionTraces = traces;
  }
  
  public static async Task<MutantExecutionTraces> ReconstructTraceFromDisk(
    string traceDirectory, ImmutableArray<TestCase> testCases)
  {
    var executionTraces = new Dictionary<TestCase, FrozenSet<MutantActivationInfo>>();

    foreach (var testCase in testCases)
    {
      var testTracePath = Path.Combine(traceDirectory,
        TestCaseUtil.ValidTestFileName(testCase.Name));

      // Execution trace not recorded: either no trace found in directory,
      // or for this particular test case, no mutants are relevant in the execution
      // path
      if (!Path.Exists(testTracePath)) continue;

      // Execution trace recorded
      var mutantTracesForTestCase = await File.ReadAllLinesAsync(testTracePath);

      executionTraces[testCase] = mutantTracesForTestCase
        .Select(ParseRecordedTrace).ToFrozenSet();
    }

    return new MutantExecutionTraces(executionTraces.ToFrozenDictionary());
  }

  // Traces are recorded in the form "ENV_VAR:MUTANT_ID"
  private static MutantActivationInfo ParseRecordedTrace(string trace)
  {
    var result = trace.Split(':');
    Trace.Assert(result.Length == 2);
    return new MutantActivationInfo(
      EnvVar: result[0], MutantId: int.Parse(result[1]));
  }

  public FrozenSet<MutantActivationInfo> GetCandidateMutantsForTestCase(
    TestCase testCase)
  {
    return _executionTraces.TryGetValue(testCase, out var found)
      ? found
      : FrozenSet<MutantActivationInfo>.Empty;
  }
}