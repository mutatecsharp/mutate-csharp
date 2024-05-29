using System.Collections.Immutable;
using System.Text.Json.Serialization;
using MutateCSharp.ExecutionTracing;

namespace MutateCSharp.MutationTesting;

public sealed record MutationTestResult
{
  [JsonInclude]
  public required
    Dictionary<TestCase, ImmutableArray<(MutantActivationInfo, TestRunResult)>>
    TestResultsOfMutants;

  [JsonInclude] 
  public required Dictionary<MutantActivationInfo, MutantStatus> MutantStatus;

  public ImmutableHashSet<MutantActivationInfo> GetMutantOfStatus(
    MutantStatus status)
  {
    return MutantStatus.Where(mutantEntry => mutantEntry.Value == status)
      .Select(mutantEntry => mutantEntry.Key)
      .ToImmutableHashSet();
  }
}