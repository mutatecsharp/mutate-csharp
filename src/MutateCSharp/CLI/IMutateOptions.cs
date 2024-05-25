using MutateCSharp.Mutation.SyntaxRewriter;

namespace MutateCSharp.CLI;

public interface IMutateOptions
{
  public string AbsoluteSolutionPath();
  public string AbsoluteProjectPath();
  public string AbsoluteSourceFilePath();
  public bool Backup();
  public bool Optimise();
  public bool DryRun();
  public SyntaxRewriterMode MutationMode();
  public IEnumerable<string> AbsoluteSourceFilePathsToIgnore();
  public IEnumerable<string> AbsoluteDirectoryPaths();
}