using CommandLine;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Build.Locator;
using MutateCSharp.FileSystem;
using MutateCSharp.Mutation;
using Serilog;

namespace MutateCSharp.CLI;

internal static class CliHandler
{
  internal static async void RunOptions(CliOptions options)
  {
    using var backup = new DirectoryBackup(Path.GetDirectoryName(options.AbsolutePath)!);
    // See https://learn.microsoft.com/en-us/visualstudio/msbuild/find-and-use-msbuild-versions?view=vs-2022
    MSBuildLocator.RegisterDefaults();
    
    using var workspace = MSBuildWorkspace.Create();
    workspace.LoadMetadataForReferencedProjects = true;
    workspace.SkipUnrecognizedProjects = true;
    
    var solution = await workspace.OpenSolutionAsync(options.AbsolutePath);
    var mutatedSolution =
      await MutatorHarness.MutateSolution(workspace, solution);
  }

  internal static void HandleParseError(IEnumerable<Error> errorIterator)
  {
    var errors = errorIterator.ToList();
    if (errors.Any(error => error.Tag is ErrorType.HelpRequestedError or ErrorType.VersionRequestedError)) return;

    errors.ForEach(error => Log.Error("{ErrorMessage}", error));
  }
}