using System.Text.Json;
using MutateCSharp.Mutation.Registry;

namespace MutateCSharp.FileSystem;

public static class MutationRegistryPersister
{
  private const string FileName = "registry.mucs.json";
  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  // Persist mutation information corresponding to each source file on disk
  public static async Task<string> PersistToDisk(this ProjectLevelMutationRegistry registry, string basePath)
  {
    var outputPath = Path.Combine(basePath, FileName);
    await using var fs = File.Create(outputPath, 4096, FileOptions.Asynchronous);
    await JsonSerializer.SerializeAsync(fs, registry, JsonOptions);
    return outputPath;
  }

  public static async Task<ProjectLevelMutationRegistry> ReconstructRegistryFromDisk(string absolutePath)
  {
    using var fs = new StreamReader(absolutePath);
    var registry =
      await JsonSerializer.DeserializeAsync<ProjectLevelMutationRegistry>(fs.BaseStream, JsonOptions);
    return registry!;
  }
}