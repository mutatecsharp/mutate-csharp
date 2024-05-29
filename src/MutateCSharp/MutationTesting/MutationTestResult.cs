using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.Util.Converters;

namespace MutateCSharp.MutationTesting;

public sealed record MutationTestResult
{
  // Test case name to a collection of mutant test run results.
  [JsonInclude]
  [JsonConverter(typeof(MutantTestResultsConverter))]
  public required
    FrozenDictionary<string, FrozenDictionary<MutantActivationInfo, TestRunResult>>
    MutantTestResultsOfTestCases;

  // One of { Survived, Killed, Timeout, Uncovered }.
  [JsonInclude] 
  [JsonConverter(typeof(MutantStatusConverter))]
  public required FrozenDictionary<MutantActivationInfo, MutantStatus> MutantStatus;

  public ImmutableHashSet<MutantActivationInfo> GetMutantOfStatus(
    MutantStatus status)
  {
    return MutantStatus.Where(mutantEntry => mutantEntry.Value == status)
      .Select(mutantEntry => mutantEntry.Key)
      .ToImmutableHashSet();
  }
}