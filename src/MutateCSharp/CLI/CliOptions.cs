using CommandLine;

namespace MutateCSharp.CLI;

internal sealed class CliOptions
{
  private readonly string _absolutePath = string.Empty;

  [Option("solution", Required = true, HelpText = "The path to C# solution file (.sln).")]
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

      if (!File.Exists(absolutePath) || Path.GetExtension(absolutePath) != ".sln")
      {
        throw new ArgumentException("Solution file does not exist or is invalid.");
      }

      _absolutePath = absolutePath;
    }
  }
}