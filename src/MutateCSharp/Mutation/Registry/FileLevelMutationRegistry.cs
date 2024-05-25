using System.Collections.Frozen;
using System.Text.Json.Serialization;
using MutateCSharp.Util.Converters;

namespace MutateCSharp.Mutation.Registry;

[JsonConverter(typeof(FileLevelMutationRegistryConverter))]
public sealed record FileLevelMutationRegistry
{
  // Relative to project base path.
  [JsonInclude]
  public required string FileRelativePath { get; init; }
  
  [JsonInclude]
  public required string EnvironmentVariable { get; init; }
  
  // A mutation produces a mutant. The mutation-mutant relation is a one-to-one
  // mapping; mutation ID and mutant ID can be used interchangeably.
  // A mutation stores the original and replacement syntax trees.
  [JsonInclude]
  public required FrozenDictionary<int, Mutation> Mutations { get; init; }
  
  public Mutation GetMutation(int mutantId) => Mutations[mutantId];
}