using CommandLine;
using MutateCSharp.FileSystem;
using MutateCSharp.Util;

namespace MutateCSharp.CLI;

[Verb("trace", HelpText = "Commence mutant execution tracing.")]
internal sealed class TraceOptions
{
  private readonly string _absoluteTestProjectDirectory = string.Empty;
  private readonly string _absoluteTraceOutputDirectory = string.Empty;
  private readonly string _absoluteListOfTestsFilePath = string.Empty;
  private readonly string _absoluteMutationRegistryPath = string.Empty;
  private readonly string _absoluteExecutionTraceRegistryPath = string.Empty;
  private readonly string _absoluteRunSettingsPath = string.Empty;

  [Option("test-project",
    Required = true,
    HelpText = "The directory/path to test project containing the tests.")]
  public required string AbsoluteTestProjectDirectory
  {
    get => _absoluteTestProjectDirectory;
    init
    {
      try
      {
        _absoluteTestProjectDirectory = ParseUtil.ParseAbsoluteDirectory(value);
      }
      catch (ArgumentException)
      {
        _absoluteTestProjectDirectory =
          ParseUtil.ParseAbsolutePath(value, FileExtension.Project);
      }
    }
  }

  [Option("output-directory",
    Required = true,
    HelpText = "The directory to output mutant execution traces.")]
  public required string AbsoluteExecutionTraceOutputDirectory
  {
    get => _absoluteTraceOutputDirectory;
    init => _absoluteTraceOutputDirectory =
      ParseUtil.ParseAbsoluteDirectory(value);
  }

  [Option("tests-list",
    HelpText = "The list of tests to trace mutant execution against.")]
  public string AbsoluteListOfTestsFilePath
  {
    get => _absoluteListOfTestsFilePath;
    init => _absoluteListOfTestsFilePath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.Any);
  }

  [Option("test-name",
    HelpText = "The full name of test to trace mutant execution against.")]
  public string SpecifiedTestName { get; init; } = string.Empty;

  [Option("mutation-registry",
    Required = true,
    HelpText = "The path to the mutation registry (.json).")]
  public required string AbsoluteMutationRegistryPath
  {
    get => _absoluteMutationRegistryPath;
    init
    {
      if (Path.GetFileName(value) != MutationRegistryPersister.MutationRegistryFileName)
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

  [Option("testrun-settings",
    HelpText = "The path to the individual test run setting (.runsettings).")]
  public string AbsoluteRunSettingsPath
  {
    get => _absoluteRunSettingsPath;
    init => _absoluteRunSettingsPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.DotnetRunSettings);
  }
  
  [Option("no-build", Default = false,
    HelpText = "Do not build test project.")]
  public bool DoNotBuild { get; init; }
}