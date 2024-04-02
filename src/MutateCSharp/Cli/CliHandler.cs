using CommandLine;
using Serilog;

namespace MutateCSharp.Cli;

internal static class CliHandler
{
  internal static void RunOptions(CliOptions options)
  {
    Log.Debug("After parse: {@CliOptions}", options);
    var repository = new VersionControl.Repository(options.Repository, options.Directory, options.Branch);
    var project = new SUT.Application(repository.RootDirectory);
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