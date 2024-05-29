using MutateCSharp.Mutation.Registry;

namespace MutateCSharp.Util;

public static class RegistryUtil
{
  public static int GetTotalMutantCount(
    this ProjectLevelMutationRegistry mutationRegistry)
  {
    return mutationRegistry.ProjectRelativePathToRegistry.Values
      .Select(registry => registry.Mutations.Count).Sum();
  }

  public static int GetMutantCount(
    this ProjectLevelMutationRegistry mutationRegistry, string relativeFilePath)
  {
    return mutationRegistry.ProjectRelativePathToRegistry[relativeFilePath]
      .Mutations.Count;
  }
}