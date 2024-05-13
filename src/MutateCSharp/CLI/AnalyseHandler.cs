using System.Collections.Immutable;
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
      if (mutationRegistry is null)
      {
        Log.Error("Registry has been corrupted. To resolve this, rerun the mutate phase to obtain a fresh registry set.");
        return;
      }
      
      DisplayMutationInfo(mutationRegistry);
    }
    else
    {
      Log.Error("No mutation registry specified.");
    }
  }

  internal static void DisplayMutationInfo(
    ProjectLevelMutationRegistry mutationRegistry)
  {
    var totalMutations = 0;
    
    // Sort by path for nicer output
    var registrySortedByPath =
      mutationRegistry.ProjectRelativePathToRegistry
        .OrderBy(entry => entry.Key)
        .ToImmutableSortedDictionary();
    
    foreach (var entry in registrySortedByPath)
    {
      Log.Information("{MutationCount} mutations in {File}", entry.Value.Mutations.Count, entry.Key);
      totalMutations += entry.Value.Mutations.Count;
    }
    
    Log.Information("Total mutation count: {TotalMutationCount}", totalMutations);
  }
}