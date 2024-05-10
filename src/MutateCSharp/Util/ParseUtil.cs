using MutateCSharp.FileSystem;

namespace MutateCSharp.Util;

public static class ParseUtil
{
  public static string ParseAbsolutePath(string path, FileExtension extension)
  {
    if (path.Intersect(Path.GetInvalidPathChars()).Any())
      throw new ArgumentException("Unable to parse malformed path.");

    var absolutePath = Path.GetFullPath(path);

    if (!File.Exists(absolutePath) ||
        Path.GetExtension(absolutePath) != extension.ToFriendlyString())
      throw new ArgumentException(
        $"{Path.GetFileName(absolutePath)} does not exist or is invalid.");

    return absolutePath;
  }
}