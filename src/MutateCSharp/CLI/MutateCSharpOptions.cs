using System.Collections.Immutable;
using CommandLine;
using MutateCSharp.FileSystem;

namespace MutateCSharp.CLI;

internal sealed class MutateCSharpOptions
{
  private readonly string _absoluteSolutionPath = string.Empty;
  private readonly string _absoluteProjectPath = string.Empty;
  private readonly string _absoluteSourceFilePath = string.Empty;
  private readonly string[] _absoluteSourceFilePathsToIgnore = [];

  [Option("solution",
    HelpText = "The path to C# solution file (.sln).")]
  public string AbsoluteSolutionPath
  {
    get => _absoluteSolutionPath;
    init => _absoluteSolutionPath =
      ParseAbsolutePath(value, FileExtension.Solution);
  }

  [Option("project",
    HelpText = "The path to C# project file (.csproj).")]
  public string AbsoluteProjectPath
  {
    get => _absoluteProjectPath;
    init => _absoluteProjectPath =
      ParseAbsolutePath(value, FileExtension.Project);
  }

  [Option("source-file",
    HelpText = "The path to an individual C# source file (.cs).")]
  public string AbsoluteSourceFilePath
  {
    get => _absoluteSourceFilePath;
    init => _absoluteSourceFilePath =
      ParseAbsolutePath(value, FileExtension.CSharpSourceFile);
  }

  [Option("restore",
    Default = false,
    HelpText =
      "Restore files to original state after applying mutation testing.")]
  public bool Backup { get; init; }

  [Option("ignore-files",
    HelpText = "Path(s) to C# source files to ignore (.cs).")]
  public IEnumerable<string> AbsoluteSourceFilePathsToIgnore
  {
    get => _absoluteSourceFilePathsToIgnore;
    init => _absoluteSourceFilePathsToIgnore = value.ToArray();
  }

  private static string ParseAbsolutePath(string path, FileExtension extension)
  {
    if (path.Intersect(Path.GetInvalidPathChars()).Any())
      throw new ArgumentException("Unable to parse malformed path.");

    var absolutePath = Path.GetFullPath(path);

    if (!File.Exists(absolutePath) ||
        Path.GetExtension(absolutePath) != extension.ToFriendlyString())
      throw new ArgumentException(
        $"{Path.GetFileName(absolutePath)} does not exist or is invalid.");

    return absolutePath;
  }
}