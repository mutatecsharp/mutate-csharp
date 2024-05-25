using MutateCSharp.Mutation.SyntaxRewriter;

namespace MutateCSharp.CLI;

public interface IMutateOptions
{
  public string AbsoluteSolutionPath();
  public string AbsoluteProjectPath();
  public string AbsoluteSourceFilePath();
  public bool Backup();
  public bool Optimise();
  public SyntaxRewriterMode MutationMode();
  public IEnumerable<string> AbsoluteSourceFilePathsToIgnore();
}