using System.Collections.Frozen;

namespace MutateCSharp.Mutation.Registry;

public class ProjectLevelMutationRegistryBuilder
{
  private readonly IDictionary<string, FileLevelMutationRegistry>
    _relativePathToRegistry =
      new Dictionary<string, FileLevelMutationRegistry>();

  public void AddRegistry(FileLevelMutationRegistry registry)
  {
    _relativePathToRegistry[registry.FileRelativePath] = registry;
  }

  public ProjectLevelMutationRegistry ToFinalisedRegistry()
  {
    return new ProjectLevelMutationRegistry
    {
      ProjectRelativePathToRegistry = _relativePathToRegistry.ToFrozenDictionary()
    };
  }
}