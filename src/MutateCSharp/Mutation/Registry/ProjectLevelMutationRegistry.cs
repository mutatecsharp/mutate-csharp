using System.Collections.Frozen;
using System.Text.Json.Serialization;
using MutateCSharp.Util.Converters;

namespace MutateCSharp.Mutation.Registry;

[JsonConverter(typeof(ProjectLevelMutationRegistryConverter))]
public class ProjectLevelMutationRegistry
{
  [JsonInclude]
  public required FrozenDictionary<string, FileLevelMutationRegistry>
    ProjectRelativePathToRegistry { get; init; }
}