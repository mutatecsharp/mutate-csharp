using System.Collections.Immutable;
using CommandLine;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using MutateCSharp.FileSystem;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.SyntaxRewriter;
using Serilog;

namespace MutateCSharp.CLI;

internal static class MutateHandler
{
  internal static async Task RunOptions(IMutateOptions options)
  {
    // See https://learn.microsoft.com/en-us/visualstudio/msbuild/find-and-use-msbuild-versions?view=vs-2022
    MSBuildLocator.RegisterDefaults();
    
    using var workspace = MSBuildWorkspace.Create();
    workspace.LoadMetadataForReferencedProjects = true;
    workspace.SkipUnrecognizedProjects = true;
    var pathsToIgnore =
      options.AbsoluteSourceFilePathsToIgnore().ToImmutableArray();
    var directoriesToConsider =
      options.AbsoluteDirectoryPaths().ToImmutableArray();

    Log.Information("Dry run: {DryRunStatus}", options.DryRun());
    Log.Information("Omitting equivalent/redundant mutants: {RemoveRedundantMutants}", options.Optimise());

    foreach (var ignorePath in pathsToIgnore)
    {
      Log.Information("Ignoring source file: {IgnoredFilePath}", ignorePath);
    }

    if (options.AbsoluteProjectPath().Length > 0 && options.AbsoluteSourceFilePath().Length > 0)
    {
      using var backup = DirectoryBackup.BackupDirectoryIfNecessary(
        Path.GetDirectoryName(options.AbsoluteSourceFilePath())!, 
        options.Backup());
      await ProcessSourceFile(workspace, 
        options.AbsoluteProjectPath(), 
        options.AbsoluteSourceFilePath(), 
        pathsToIgnore,
        directoriesToConsider,
        options.MutationMode(),
        options.Optimise(),
        options.DryRun());
    }
    else if (options.AbsoluteProjectPath().Length > 0)
    {
      using var backup = DirectoryBackup.BackupDirectoryIfNecessary(
        Path.GetDirectoryName(options.AbsoluteProjectPath())!, 
        options.Backup());
      await ProcessProject(workspace, 
        options.AbsoluteProjectPath(), 
        pathsToIgnore, 
        directoriesToConsider,
        options.MutationMode(),
        options.Optimise(),
        options.DryRun());
    }
    else if (options.AbsoluteSolutionPath().Length > 0)
    {
      using var backup = DirectoryBackup.BackupDirectoryIfNecessary(
        Path.GetDirectoryName(options.AbsoluteSolutionPath())!, 
        options.Backup());
      await ProcessSolution(workspace, 
        options.AbsoluteSolutionPath(), 
        pathsToIgnore, 
        directoriesToConsider,
        options.MutationMode(),
        options.Optimise(),
        options.DryRun());
    }
    else
      Log.Error("No project or solution specified.");
  }

  internal static void HandleParseError(IEnumerable<Error> errorIterator)
  {
    var errors = errorIterator.ToList();
    if (errors.Any(error =>
          error.Tag is ErrorType.HelpRequestedError
            or ErrorType.VersionRequestedError)) return;

    errors.ForEach(error => Log.Error("{ErrorMessage}", error));
  }

  private static async Task ProcessSolution(
    MSBuildWorkspace workspace, 
    string absolutePath, 
    ImmutableArray<string> pathsToIgnore,
    ImmutableArray<string> directoriesToConsider,
    SyntaxRewriterMode mutationMode,
    bool optimise, bool dryRun)
  {
    var solution = await workspace.OpenSolutionAsync(absolutePath);
    
    // 1: Generate mutant schema and acquire mutation registry
    var (mutatedSolution, projectRegistries) =
      await MutatorHarness.MutateSolution(workspace, solution, pathsToIgnore,
        directoriesToConsider, mutationMode, optimise, dryRun);

    if (dryRun) return;
    
    // 2: Persist mutated solution under test
    var mutateResult = workspace.TryApplyChanges(mutatedSolution);
    if (!mutateResult)
      Log.Error("Failed to mutate solution {Solution}.",
        Path.GetFileName(solution.FilePath));
    
    // 3: Serialise and persist mutation registry on disk
    foreach (var entry in projectRegistries)
    {
      var (project, registry) = entry;
      var directory = Path.GetDirectoryName(project.FilePath)!;
      var registryPath = await registry.PersistToDisk(directory, mutationMode);
      Log.Information("Mutation registry persisted to disk: {RegistryPath}", registryPath);
    }
  }

  private static async Task ProcessProject(
    MSBuildWorkspace workspace, 
    string absolutePath, 
    ImmutableArray<string> pathsToIgnore,
    ImmutableArray<string> directoriesToConsider,
    SyntaxRewriterMode mutationMode,
    bool optimise, bool dryRun)
  {
    var project = await workspace.OpenProjectAsync(absolutePath);
    
    // 1: Generate mutant schema and acquire mutation registry
    var (mutatedProject, projectRegistry) =
      await MutatorHarness.MutateProject(workspace, project, pathsToIgnore, 
        directoriesToConsider, mutationMode, optimise, dryRun);

    if (dryRun) return;
    
    // 2: Persist mutated project under test
    var mutateResult = workspace.TryApplyChanges(mutatedProject.Solution);
    if (!mutateResult)
      Log.Error("Failed to mutate project {Project}.",
        Path.GetFileName(project.FilePath));
    
    // 3: Serialise and persist mutation registry on disk
    if (projectRegistry is not null)
    {
      var directory = Path.GetDirectoryName(absolutePath)!;
      var registryPath = await projectRegistry.PersistToDisk(directory, mutationMode);
      Log.Information("Mutation registry persisted to disk: {RegistryPath}", registryPath);
    }
  }
  
  private static async Task ProcessSourceFile(
    MSBuildWorkspace workspace, 
    string projectAbsolutePath, 
    string sourceFileAbsolutePath, 
    ImmutableArray<string> pathsToIgnore,
    ImmutableArray<string> directoriesToConsider,
    SyntaxRewriterMode mutationMode,
    bool optimise, bool dryRun)
  {
    var project = await workspace.OpenProjectAsync(projectAbsolutePath);
    var document = project.Documents.FirstOrDefault(doc =>
      Path.GetFullPath(doc.FilePath ?? string.Empty).Equals(sourceFileAbsolutePath));
    
    // 1: Generate mutant schema and acquire mutation registry
    var (mutatedProject, projectRegistry) =
      await MutatorHarness.MutateProject(workspace, project, pathsToIgnore, 
        directoriesToConsider, mutationMode, optimise, dryRun, document);

    if (dryRun) return;
    
    // 2: Persist mutated project under test
    var mutateResult = workspace.TryApplyChanges(mutatedProject.Solution);
    if (!mutateResult)
      Log.Error("Failed to mutate project {Project}.",
        Path.GetFileName(project.FilePath));
    
    // 3: Serialise and persist mutation registry on disk
    if (projectRegistry is not null)
    {
      var directory = Path.GetDirectoryName(projectAbsolutePath)!;
      var registryPath = await projectRegistry.PersistToDisk(directory, mutationMode);
      Log.Information("Mutation registry persisted to disk: {RegistryPath}", registryPath);
    }
  }
}