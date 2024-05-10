using CommandLine;
using MutateCSharp.FileSystem;
using MutateCSharp.Util;

namespace MutateCSharp.CLI;

[Verb("analyse", HelpText = "Load mutation registry and commence mutation testing.")]
internal sealed class AnalyseOptions
{
  private readonly string _absoluteRegistryPath = string.Empty;
  private readonly string _absoluteProjectPath = string.Empty;

  [Option("registry", HelpText = "The path to mutation registry (.json).")]
  public required string AbsoluteRegistryPath
  {
    get => _absoluteRegistryPath;
    init => _absoluteRegistryPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.Json);
  }

  [Option("project",
    HelpText = "The path to mutated C# project file (.csproj).")]
  public string AbsoluteProjectPath
  {
    get => _absoluteProjectPath;
    init => _absoluteProjectPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.Project);
  }
}