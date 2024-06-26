using System.Collections.Frozen;

namespace MutateCSharp.FileSystem;

public enum FileExtension
{
  Solution,
  Project,
  CSharpSourceFile,
  Json,
  DotnetRunSettings,
  Any
}

public static class FileExtensionConstants
{
  private static readonly FrozenDictionary<FileExtension, string>
    FileExtensions =
      new Dictionary<FileExtension, string>
      {
        [FileExtension.Solution] = ".sln",
        [FileExtension.Project] = ".csproj",
        [FileExtension.CSharpSourceFile] = ".cs",
        [FileExtension.Json] = ".json",
        [FileExtension.DotnetRunSettings] = ".runsettings"
      }.ToFrozenDictionary();

  public static string ToFriendlyString(this FileExtension fileExtension)
  {
    return FileExtensions.TryGetValue(fileExtension, out var extension)
      ? extension
      : string.Empty;
  }
}