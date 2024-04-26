using CommandLine;
using MutateCSharp.FileSystem;

namespace MutateCSharp.CLI;

internal sealed class CliOptions
{
  private readonly string _absoluteSolutionPath = string.Empty;
  private readonly string _absoluteSourceFilePath = string.Empty;

  [Option("solution",
    HelpText = "The path to C# solution file (.sln).")]
  public required string AbsoluteSolutionPath
  {
    get => _absoluteSolutionPath;
    init => _absoluteSolutionPath =
      ParseAbsolutePath(value, FileExtension.Solution);
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

  private static string ParseAbsolutePath(string path, FileExtension extension)
  {
    if (path.Intersect(Path.GetInvalidPathChars()).Any())
      throw new ArgumentException("Unable to parse malformed path.");

    var absolutePath = Path.GetFullPath(path);

    if (!File.Exists(absolutePath) ||
        Path.GetExtension(absolutePath) != extension.ToFriendlyString())
      throw new ArgumentException(
        "Solution file does not exist or is invalid.");

    return absolutePath;
  }
}