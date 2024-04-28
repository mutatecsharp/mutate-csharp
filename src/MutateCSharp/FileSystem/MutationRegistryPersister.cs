using System.Text.Json;
using MutateCSharp.Mutation.Registry;

namespace MutateCSharp.FileSystem;

public static class MutationRegistryPersister
{
  private const string FileName = "registry.mucs.json";
  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  // Persist mutation information corresponding to each source file on disk
  public static async Task<string> PersistToDisk(this MutationRegistry registry, string path)
  {
    var outputPath = Path.Combine(path, FileName);
    await using var fs = File.Create(outputPath, 4096, FileOptions.Asynchronous);
    await JsonSerializer.SerializeAsync(fs, registry, JsonOptions);
    return outputPath;
  }

  public static async Task<MutationRegistry> ReconstructRegistryFromDisk(string path)
  {
    using var fs = new StreamReader(path);
    var registry =
      await JsonSerializer.DeserializeAsync<MutationRegistry>(fs.BaseStream, JsonOptions);
    return registry!;
  }
}