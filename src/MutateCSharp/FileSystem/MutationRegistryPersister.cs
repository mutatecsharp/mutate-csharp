using System.Text.Json;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Mutation.SyntaxRewriter;

namespace MutateCSharp.FileSystem;

public static class MutationRegistryPersister
{
  public const string MutationRegistryFileName = "registry.mucs.json";
  public const string ExecutionTracerRegistryFileName = "tracer-registry.mucs.json";
  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  // Persist mutation information corresponding to each source file on disk
  public static async Task<string> PersistToDisk(this ProjectLevelMutationRegistry registry, string basePath, SyntaxRewriterMode mutationMode)
  {
    var fileName = mutationMode switch
    {
      SyntaxRewriterMode.Mutate => MutationRegistryFileName,
      SyntaxRewriterMode.TraceExecution => ExecutionTracerRegistryFileName,
      _ => string.Empty
    };

    if (string.IsNullOrEmpty(fileName)) return string.Empty;
    
    var outputPath = Path.Combine(basePath, fileName);
    await using var fs = File.Create(outputPath, bufferSize: 8192, FileOptions.Asynchronous);
    await JsonSerializer.SerializeAsync(fs, registry, JsonOptions);
    return outputPath;
  }

  public static async Task<ProjectLevelMutationRegistry> ReconstructRegistryFromDisk(string absolutePath)
  {
    await using var fs =
      new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
    var registry =
      await JsonSerializer.DeserializeAsync<ProjectLevelMutationRegistry>(fs);
    return registry!;
  }
}