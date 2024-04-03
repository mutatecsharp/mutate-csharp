using CommandLine;

namespace MutateCSharp.CLI;

internal sealed class CliOptions
{
  private readonly string _absolutePath = string.Empty;

  [Option("file", Required = true, HelpText = "The path to .cs source file.")]
  public required string AbsolutePath { 
    get => _absolutePath;
    init
    {
      if (value.Intersect(Path.GetInvalidPathChars()).Any() ||
          value.Intersect(Path.GetInvalidFileNameChars()).Any())
      {
        throw new ArgumentException("Unable to parse malformed directory");
      }

      _absolutePath = Path.GetFullPath(value);
    }
  }
}