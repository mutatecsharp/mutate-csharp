using CommandLine;
using MutateCSharp.SUT;
using Serilog;

namespace MutateCSharp.CLI;

internal static class CliHandler
{
  internal static void RunOptions(CliOptions options)
  {
    var fileUnderTest = new FileUnderTest(options.AbsolutePath);
    
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