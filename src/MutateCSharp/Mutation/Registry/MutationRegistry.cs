using System.Collections.Frozen;
using System.Text.Json.Serialization;
using MutateCSharp.Util.Converters;

namespace MutateCSharp.Mutation.Registry;

[JsonConverter(typeof(MutationRegistryConverter))]
public class MutationRegistry
{
  [JsonInclude]
  public required FrozenDictionary<string, FileLevelMutationRegistry>
    RelativePathToRegistry { get; init; }
}