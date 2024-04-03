using CommandLine;

namespace MutateCSharp.CLI;

internal sealed class CliOptions
{
  private readonly string _absolutePath = string.Empty;

  [Option("project", Required = true, HelpText = "The path to C# project file (.csproj).")]
  public required string AbsolutePath { 
    get => _absolutePath;
    init
    {
      if (value.Intersect(Path.GetInvalidPathChars()).Any() ||
          value.Intersect(Path.GetInvalidFileNameChars()).Any())
      {
        throw new ArgumentException("Unable to parse malformed path.");
      }

      var absolutePath = Path.GetFullPath(value);

      if (File.Exists(absolutePath))
      {
        throw new ArgumentException("Project file does not exist.");
      }

      _absolutePath = absolutePath;
    }
  }
}