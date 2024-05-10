using System.Text.Json;
using MutateCSharp.Mutation.Registry;
using Serilog;

namespace MutateCSharp.CLI;

internal static class AnalyseHandler
{
  internal static async Task RunOptions(AnalyseOptions options)
  {
    // TODO: create test harness
    if (options.AbsoluteRegistryPath.Length > 0)
    {
      Log.Information("Loading mutation registry...");
      await using var fs = new 
        FileStream(options.AbsoluteRegistryPath, FileMode.Open, FileAccess.Read);
      var mutationRegistry =
        await JsonSerializer.DeserializeAsync<ProjectLevelMutationRegistry>(fs);

      var totalMutations = 0;
      foreach (var entry in mutationRegistry!.ProjectRelativePathToRegistry)
      {
        Log.Information("{MutationCount} mutations in {File}", entry.Value.Mutations.Count, entry.Key);
        totalMutations += entry.Value.Mutations.Count;
      }
      Log.Information("Total mutation count: {TotalMutationCount}", totalMutations);
    }
    else
    {
      Log.Error("No mutation registry specified.");
    }
  }
}