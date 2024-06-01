using CommandLine;
using MutateCSharp.FileSystem;
using MutateCSharp.Util;

namespace MutateCSharp.CLI;

[Verb("test", HelpText = "Commence mutation testing using execution trace information.")]
internal sealed class MutationTestOptions
{
  private readonly string _absoluteSourceFileUnderTestPath = string.Empty;
  private readonly string _absoluteTestProjectPath = string.Empty;
  private readonly string _absolutePassingTestsPath = string.Empty;
  private readonly string _absoluteMutationRegistryPath = string.Empty;
  private readonly string _absoluteExecutionTraceRegistryPath = string.Empty;
  private readonly string _absoluteMutantExecutionTraceDirectory = string.Empty;
  private readonly string _absoluteRunSettingsPath = string.Empty;
  private readonly string _absoluteProjectUnderTestPath = string.Empty;
  private readonly string _absoluteCompilationArtifactDirectory = string.Empty;
  private readonly string _absoluteTestMetadataDirectory = string.Empty;
  private readonly string _absoluteKilledMutantsMetadataDirectory = string.Empty;

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

  [Option("project",
    HelpText = "Optional. The path to the project under test (.csproj).")]
  public string AbsoluteProjectUnderTestPath
  {
    get => _absoluteProjectUnderTestPath;
    init => _absoluteProjectUnderTestPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.Project);
  }

  [Option("source-file-under-test",
    HelpText =
      "Optional. If specified, mutation testing will only focus its efforts to the particular source file.")]
  public string AbsoluteSourceFileUnderTestPath
  {
    get => _absoluteSourceFileUnderTestPath;
    init => _absoluteSourceFileUnderTestPath =
      ParseUtil.ParseAbsolutePath(value, FileExtension.CSharpSourceFile);
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

  [Option("mutant-traces", Required = true, 
    HelpText = "The directory to mutant execution trace.")]
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

  [Option("compilation-artifact-directory",
    HelpText =
      "The directory that contains compilation artifacts. If specified, it will be emptied between mutation testing iterations.")]
  public string AbsoluteCompilationArtifactDirectory
  {
    get => _absoluteCompilationArtifactDirectory;
    init => _absoluteCompilationArtifactDirectory =
      ParseUtil.ParseAbsoluteDirectory(value);
  }

  [Option("test-output", Required = true,
    HelpText =
      "The output directory to store metadata of tests currently being worked on.")]
  public string AbsoluteTestMetadataDirectory
  {
    get => _absoluteTestMetadataDirectory;
    init => _absoluteTestMetadataDirectory =
      ParseUtil.ParseAbsoluteDirectory(value);
  }

  [Option("killed-mutants-output", Required = true,
    HelpText =
      "The output directory to store metadata of mutants killed by tests.")]
  public string AbsoluteKilledMutantsMetadataDirectory
  {
    get => _absoluteKilledMutantsMetadataDirectory;
    init => _absoluteKilledMutantsMetadataDirectory =
      ParseUtil.ParseAbsoluteDirectory(value);
  }

  [Option("dry-run", Default = false, HelpText = "Perform dry run.")]
  public bool DryRun { get; init; }
}