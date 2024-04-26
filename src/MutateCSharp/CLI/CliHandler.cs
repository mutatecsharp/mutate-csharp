using CommandLine;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;
using MutateCSharp.FileSystem;
using MutateCSharp.Mutation;
using Serilog;

namespace MutateCSharp.CLI;

internal static class CliHandler
{
  internal static async Task RunOptions(CliOptions options)
  {
    using var backup = DirectoryBackup.BackupDirectoryIfNecessary(
      Path.GetDirectoryName(options.AbsoluteSolutionPath)!,
      options.Backup);

    // See https://learn.microsoft.com/en-us/visualstudio/msbuild/find-and-use-msbuild-versions?view=vs-2022
    MSBuildLocator.RegisterDefaults();

    using var workspace = MSBuildWorkspace.Create();
    workspace.LoadMetadataForReferencedProjects = true;
    workspace.SkipUnrecognizedProjects = true;
    var solution =
      await workspace.OpenSolutionAsync(options.AbsoluteSolutionPath);
    _ = await MutatorHarness.MutateSolution(workspace, solution);
  }

  internal static void HandleParseError(IEnumerable<Error> errorIterator)
  {
    var errors = errorIterator.ToList();
    if (errors.Any(error =>
          error.Tag is ErrorType.HelpRequestedError
            or ErrorType.VersionRequestedError)) return;

    errors.ForEach(error => Log.Error("{ErrorMessage}", error));
  }
}