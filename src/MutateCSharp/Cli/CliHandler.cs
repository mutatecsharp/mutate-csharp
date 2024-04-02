using CommandLine;
using MutateCSharp.SUT;
using Serilog;

namespace MutateCSharp.Cli;

internal static class CliHandler
{
  internal static void RunOptions(CliOptions options)
  {
    Log.Debug("After parse: {@CliOptions}", options);
    var repository = new VersionControl.Repository(options.Repository, options.Directory, options.Branch);
    var sut = new Application(repository.RootDirectory);
    
    // Log number of source files under test
    Log.Debug("Source file count: {SourceCount}", sut.GetSolutions()
      .SelectMany(solution => 
        solution.GetProjectsOfType<ProjectUnderTest>().Select(project => project.FileCount()))
      .Sum());
    
    // Log number of testing files
    Log.Debug("Test file count: {TestCount}", sut.GetSolutions()
      .SelectMany(solution => 
        solution.GetProjectsOfType<TestProject>().Select(project => project.FileCount()))
      .Sum());
    
    // var mutants = Mutation.Mutator.DeclareMutants(project);
    // var representativeMutants = Mutator.SelectMutants(mutants);
  }

  internal static void HandleParseError(IEnumerable<Error> errorIterator)
  {
    var errors = errorIterator.ToList();
    if (errors.Any(error => error.Tag is ErrorType.HelpRequestedError or ErrorType.VersionRequestedError)) return;

    errors.ForEach(error => Log.Error("{ErrorMessage}", error));
  }
}