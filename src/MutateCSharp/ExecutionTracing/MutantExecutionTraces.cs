using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Data;
using MutateCSharp.MutationTesting;
using MutateCSharp.Util;
using Serilog;

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
    return await ReconstructTraceFromDisk(traceDirectory, testCases, string.Empty);
  }
  
  public static async Task<MutantExecutionTraces> ReconstructTraceFromDisk(
    string traceDirectory, ImmutableArray<TestCase> testCases, string envVar)
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

      try
      {
        var parsedTrace = mutantTracesForTestCase
          .Select(ParseRecordedTrace);

        // If mutation testing is only directed to the specific file, then we
        // only care about mutants in that file that is covered in the trace
        if (!string.IsNullOrEmpty(envVar))
        {
          parsedTrace = parsedTrace.Where(trace => trace.EnvVar.Equals(envVar));
        }

        executionTraces[testCase] = parsedTrace.ToFrozenSet();
      }
      catch (Exception)
      {
        Log.Error("Error while parsing execution trace for test {TestName}.",
          testTracePath);
        throw;
      }
    }

    return new MutantExecutionTraces(executionTraces.ToFrozenDictionary());
  }

  // Traces are recorded in the form "ENV_VAR:MUTANT_ID"
  private static MutantActivationInfo ParseRecordedTrace(string trace)
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

  public FrozenSet<MutantActivationInfo> GetCandidateMutantsForTestCase(
    TestCase testCase)
  {
    return _executionTraces.TryGetValue(testCase, out var found)
      ? found
      : FrozenSet<MutantActivationInfo>.Empty;
  }
}