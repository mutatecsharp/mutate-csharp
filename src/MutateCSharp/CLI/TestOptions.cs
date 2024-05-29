using CommandLine;
using MutateCSharp.FileSystem;
using MutateCSharp.Util;

namespace MutateCSharp.CLI;

[Verb("test", HelpText = "Commence mutation testing using execution trace information.")]
internal sealed class TestOptions
{
  private readonly string _absoluteTestProjectPath = string.Empty;
  private readonly string _absolutePassingTestsPath = string.Empty;
  private readonly string _absoluteMutationRegistryPath = string.Empty;
  private readonly string _absoluteExecutionTraceRegistryPath = string.Empty;
  private readonly string _absoluteMutantExecutionTraceDirectory = string.Empty;
  private readonly string _absoluteRunSettingsPath = string.Empty;

  [Option("test-project",
    Required = true,
    HelpText = "The path to the test project.")]
  public string AbsoluteTestProjectPath
  {
    get => _absoluteTestProjectPath;
    init => _absoluteTestProjectPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.Project);
  }
  
  [Option("passing-tests",
    Required = true,
    HelpText =
      "The path to list of passing tests, in order of ascending duration.")]
  public string AbsolutePassingTestsFilePath
  {
    get => _absolutePassingTestsPath;
    init => _absolutePassingTestsPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.Any);
  }

  [Option("mutation-registry",
    Required = true,
    HelpText = "The path to the mutation registry (.json).")]
  public required string AbsoluteMutationRegistryPath
  {
    get => _absoluteMutationRegistryPath;
    init
    {
      if (Path.GetFileName(value) !=
          MutationRegistryPersister.MutationRegistryFileName)
      {
        throw new ArgumentException(
          $"Mutation registry file name expected to be {MutationRegistryPersister.MutationRegistryFileName}.");
      }

      _absoluteMutationRegistryPath =
        ParseUtil.ParseAbsolutePath(value, FileExtension.Json);
    }
  }
  
  [Option("tracer-registry",
    Required = true,
    HelpText = "The path to the tracer mutation registry (.json).")]
  public required string AbsoluteExecutionTraceRegistryPath
  {
    get => _absoluteExecutionTraceRegistryPath;
    init
    {
      if (Path.GetFileName(value) != MutationRegistryPersister.ExecutionTracerRegistryFileName)
      {
        throw new ArgumentException(
          $"Mutation registry file name expected to be {MutationRegistryPersister.ExecutionTracerRegistryFileName}.");
      }
      
      _absoluteExecutionTraceRegistryPath = 
        ParseUtil.ParseAbsolutePath(value, FileExtension.Json);
    }
  }

  [Option("mutant-traces", Required = true, HelpText = "The directory to mutant execution trace.")]
  public required string AbsoluteRecordedExecutionTraceDirectory
  {
    get => _absoluteMutantExecutionTraceDirectory;
    init
    {
      // Sanity check: directory should exist
      if (!Directory.Exists(value))
      {
        throw new ArgumentException(
          "Mutant execution trace directory does not exist.");
      }

      _absoluteMutantExecutionTraceDirectory =
        ParseUtil.ParseAbsoluteDirectory(value);
    }
  }
  
  [Option("testrun-settings",
    HelpText = "The path to the individual test run setting (.runsettings).")]
  public string AbsoluteRunSettingsPath
  {
    get => _absoluteRunSettingsPath;
    init => _absoluteRunSettingsPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.DotnetRunSettings);
  }
}