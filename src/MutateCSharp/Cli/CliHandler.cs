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
    
    // Log number of source files under test / test files
    foreach (var solution in sut.GetSolutions())
    {
      var sourceCount = solution.GetFilesOfType<FileUnderTest>().Count();
      var testCount = solution.GetFilesOfType<TestFile>().Count();
      Log.Information("{SolutionName}: {SourceCount} source file(s); {TestCount} test file(s)", solution.Name, sourceCount, testCount);
    }
    
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