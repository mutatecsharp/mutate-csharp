using MutateCSharp.FileSystem;

namespace MutateCSharp.Util;

public static class ParseUtil
{
  public static string ParseAbsolutePath(string path, FileExtension extension)
  {
    if (path.Intersect(Path.GetInvalidPathChars()).Any())
      throw new ArgumentException("Unable to parse malformed path.");

    var absolutePath = Path.GetFullPath(path);
    var expectedExtension = extension.ToFriendlyString();
    var extensionMatches = 
      expectedExtension == string.Empty ||
      Path.GetExtension(absolutePath) == expectedExtension;

    if (!File.Exists(absolutePath) || !extensionMatches)
      throw new ArgumentException(
        $"{Path.GetFileName(absolutePath)} does not exist or is invalid.");

    return absolutePath;
  }

  public static string ParseAbsoluteDirectory(string path)
  {
    if (path.Intersect(Path.GetInvalidPathChars()).Any())
      throw new ArgumentException("Unable to parse malformed path.");

    var absolutePath = Path.GetFullPath(path);
    if (!string.IsNullOrEmpty(Path.GetExtension(absolutePath)))
      throw new ArgumentException(
        "Unable to parse file paths; directory paths expected.");
    
    return absolutePath;
  }
}