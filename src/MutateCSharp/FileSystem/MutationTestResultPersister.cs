using System.IO.Compression;
using System.Text.Json;
using MutateCSharp.MutationTesting;

namespace MutateCSharp.FileSystem;

public static class MutationTestResultPersister
{
  public const string MutationTestResultFileName = "mutation-testing.mucs.json";
  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  public static async Task<string> PersistToDisk(this MutationTestResult result,
    string basePath)
  {
    var outputPath = Path.Combine(basePath, MutationTestResultFileName);
    await using var fs = File.Create(outputPath, bufferSize: 8192,
      FileOptions.Asynchronous);
    await JsonSerializer.SerializeAsync(fs, result, JsonOptions);
    return outputPath;
  }

  public static async Task<MutationTestResult> ReconstructTestResultFromDisk(
    string absolutePath)
  {
    await using var fs =
      new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
    var result = await JsonSerializer.DeserializeAsync<MutationTestResult>(fs);
    return result!;
  }
}