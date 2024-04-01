using CommandLine;

namespace MutateCSharp.Cli;

internal sealed class CliOptions
{
  private readonly string _repository = string.Empty;
  private readonly string _directory = Path.GetFullPath("sut");
  
  [Option("repository", Required = true, HelpText = "The remote URL to the repository under test.")]
  public required string Repository
  {
    get => _repository;
    init
    {
      if (!Uri.IsWellFormedUriString(value, UriKind.Absolute))
      {
        throw new ArgumentException("Unable to parse malformed repository URI");
      }
      _repository = value;
    }
  }

  [Option("directory", HelpText = "The path to clone the repository (Default: current working directory).")]
  public string Directory { 
    get => _directory;
    init
    {
      if (value.Intersect(Path.GetInvalidPathChars()).Any() ||
          value.Intersect(Path.GetInvalidFileNameChars()).Any())
      {
        throw new ArgumentException("Unable to parse malformed directory");
      }

      _directory = Path.GetFullPath(value);
    }
  }

  [Option("branch", HelpText = "The specific branch to clone from. (Default: default branch)")]
  public string Branch { get; init; } = string.Empty;
}