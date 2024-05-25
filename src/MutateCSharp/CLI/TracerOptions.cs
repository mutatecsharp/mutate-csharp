using CommandLine;
using MutateCSharp.FileSystem;
using MutateCSharp.Mutation.SyntaxRewriter;
using MutateCSharp.Util;

namespace MutateCSharp.CLI;

[Verb("trace",
  HelpText = "Generate mutant execution tracer.")]
internal sealed class TracerOptions: IMutateOptions
{
  private readonly string _absoluteSolutionPath = string.Empty;
  private readonly string _absoluteProjectPath = string.Empty;
  private readonly string _absoluteSourceFilePath = string.Empty;
  private readonly string[] _absoluteDirectoryPaths = [];
  private readonly string[] _absoluteSourceFilePathsToIgnore = [];
  
  [Option("solution",
    HelpText = "The path to C# solution file (.sln).")]
  public string AbsoluteSolutionPath
  {
    get => _absoluteSolutionPath;
    init => _absoluteSolutionPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.Solution);
  }

  [Option("project",
    HelpText = "The path to C# project file (.csproj).")]
  public string AbsoluteProjectPath
  {
    get => _absoluteProjectPath;
    init => _absoluteProjectPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.Project);
  }

  [Option("directories",
    HelpText =
      "The directories within a C# project containing C# source files.")]
  public IEnumerable<string> AbsoluteDirectoryPaths
  {
    get => _absoluteDirectoryPaths;
    init => _absoluteDirectoryPaths =
      value.Select(ParseUtil.ParseAbsoluteDirectory).ToArray();
  }

  [Option("source-file",
    HelpText = "The path to an individual C# source file (.cs).")]
  public string AbsoluteSourceFilePath
  {
    get => _absoluteSourceFilePath;
    init => _absoluteSourceFilePath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.CSharpSourceFile);
  }

  [Option("restore",
    Default = false,
    HelpText =
      "Restore files to original state after generating mutant execution tracer.")]
  public bool Backup { get; init; }
  
  [Option("omit-redundant", 
    Default = false, 
    HelpText = "Do not generate equivalent or redundant mutants.")]
  public bool Optimise { get; init; }
  
  [Option("dry-run", Default = false, HelpText = "Perform a dry run.")]
  public bool DryRun { get; init; }

  [Option("ignore-files",
    HelpText = "Path(s) to C# source files to ignore (.cs).")]
  public IEnumerable<string> AbsoluteSourceFilePathsToIgnore
  {
    get => _absoluteSourceFilePathsToIgnore;
    init => _absoluteSourceFilePathsToIgnore = value.ToArray();
  }

  string IMutateOptions.AbsoluteSolutionPath()
  {
    return AbsoluteSolutionPath;
  }

  string IMutateOptions.AbsoluteProjectPath()
  {
    return AbsoluteProjectPath;
  }

  string IMutateOptions.AbsoluteSourceFilePath()
  {
    return AbsoluteSourceFilePath;
  }

  bool IMutateOptions.Backup()
  {
    return Backup;
  }

  bool IMutateOptions.Optimise()
  {
    return Optimise;
  }

  bool IMutateOptions.DryRun()
  {
    return DryRun;
  }

  SyntaxRewriterMode IMutateOptions.MutationMode()
  {
    return SyntaxRewriterMode.TraceExecution;
  }

  IEnumerable<string> IMutateOptions.AbsoluteSourceFilePathsToIgnore()
  {
    return AbsoluteSourceFilePathsToIgnore;
  }

  IEnumerable<string> IMutateOptions.AbsoluteDirectoryPaths()
  {
    return AbsoluteDirectoryPaths;
  }
}