using System.Collections.Frozen;

namespace MutateCSharp.Mutation.Registry;

public class MutationRegistryBuilder
{
  private readonly IDictionary<string, FileLevelMutationRegistry>
    _relativePathToRegistry =
      new Dictionary<string, FileLevelMutationRegistry>();

  public void AddRegistry(FileLevelMutationRegistry registry)
  {
    _relativePathToRegistry[registry.FileRelativePath] = registry;
  }

  public MutationRegistry ToFinalisedRegistry()
  {
    return new MutationRegistry
    {
      RelativePathToRegistry = _relativePathToRegistry.ToFrozenDictionary()
    };
  }
}