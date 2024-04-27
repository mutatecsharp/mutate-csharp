using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace MutateCSharp.Mutation;

public sealed record MutationRegistry
{
  [JsonInclude] 
  private readonly string _filePath;
  
  // A mutation produces a mutant. The mutation-mutant relation is a one-to-one
  // mapping; mutation ID and mutant ID can be used interchangeably.
  // A mutation stores the original and replacement syntax trees.
  [JsonInclude] 
  private readonly FrozenDictionary<long, Mutation> _mutations;

  public MutationRegistry(
    string filePath,
    FrozenDictionary<long, Mutation> mutations)
  {
    _filePath = filePath;
    _mutations = mutations;
  }

  public Mutation GetMutation(long mutantId)
  {
    return _mutations[mutantId];
  }
}