namespace MutateCSharp.Mutation.Registry;

public class MutationRegistry
{
  private readonly IDictionary<string, FileLevelMutationRegistry>
    _fileAbsolutePathToMutationRegistry =
      new Dictionary<string, FileLevelMutationRegistry>();

  public void AddRegistry(string fileAbsolutePath, FileLevelMutationRegistry registry)
  {
    _fileAbsolutePathToMutationRegistry[fileAbsolutePath] = registry;
  }
}