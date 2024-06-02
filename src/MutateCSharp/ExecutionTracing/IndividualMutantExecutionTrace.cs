using System.Collections.Frozen;
using System.Data;
using MutateCSharp.MutationTesting;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.ExecutionTracing;

/*
 * Execution trace of mutants for a single test case.
 */
public class IndividualTestMutantExecutionTrace
{
  // Each test case has a list of mutants to be activated
  private readonly FrozenSet<MutantActivationInfo> _coveredMutants;
  
  private IndividualTestMutantExecutionTrace(FrozenSet<MutantActivationInfo> traces)
  {
    _coveredMutants = traces;
  }
  
  public static async Task<IndividualTestMutantExecutionTrace> ReconstructTraceFromDisk(
    string traceDirectory, TestCase testCase)
  {
    return await ReconstructTraceFromDisk(traceDirectory, testCase, string.Empty);
  }
  
  public static async Task<IndividualTestMutantExecutionTrace> ReconstructTraceFromDisk(
    string traceDirectory, TestCase testCase, string envVar)
  {
    var testTracePath = Path.Combine(traceDirectory,
      TestCaseUtil.ValidTestFileName(testCase.Name));

    if (!Path.Exists(testTracePath))
    {
      // Execution trace not recorded: either no trace found in directory,
      // or for this particular test case, no mutants are relevant in the execution
      // path
      return new IndividualTestMutantExecutionTrace(
        FrozenSet<MutantActivationInfo>.Empty);
    }
    
    // Execution trace recorded
    var mutantTracesForTestCase = await File.ReadAllLinesAsync(testTracePath);
    
    try
    {
      var parsedTrace = mutantTracesForTestCase.Select(ParseRecordedTrace);

      // If mutation testing is only directed to the specific file, then we
      // only care about mutants in that file that is covered in the trace
      if (!string.IsNullOrEmpty(envVar))
      {
        parsedTrace = parsedTrace.Where(trace => trace.EnvVar.Equals(envVar));
      }

      return new IndividualTestMutantExecutionTrace(parsedTrace.ToFrozenSet());
    }
    catch (Exception)
    {
      Log.Error("Error while parsing execution trace for test {TestName}.",
        testTracePath);
      throw;
    }
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
  
  public FrozenSet<MutantActivationInfo> GetCoveredMutants()
  {
    return _coveredMutants;
  }
}