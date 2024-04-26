using System.Collections.Frozen;

namespace MutateCSharp.FileSystem;

public enum FileExtension
{
  Solution,
  Project,
  CSharpSourceFile
}

public static class FileExtensionConstants
{
  private static readonly FrozenDictionary<FileExtension, string>
    FileExtensions =
      new Dictionary<FileExtension, string>
      {
        [FileExtension.Solution] = ".sln",
        [FileExtension.Project] = ".csproj",
        [FileExtension.CSharpSourceFile] = ".cs"
      }.ToFrozenDictionary();

  public static string ToFriendlyString(this FileExtension fileExtension)
  {
    return FileExtensions.TryGetValue(fileExtension, out var extension)
      ? extension
      : string.Empty;
  }
}