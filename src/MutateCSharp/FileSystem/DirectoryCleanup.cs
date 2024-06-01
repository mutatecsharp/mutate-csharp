using Serilog;

namespace MutateCSharp.FileSystem;

public static class DirectoryCleanup
{
  /*
   * Best-effort deletion to prevent errors from halting a long-running
   * mutation testing campaign.
   */
  public static void DeleteAllDirectoryContents(string directory)
  {
    if (!Directory.Exists(directory)) return;
    
    try
    {
      Directory.Delete(directory, recursive: true);
    }
    catch (Exception e)
    {
      Log.Warning("Failed to delete compilation artifact: {ErrorMessage}", e.Message);
    }
  }
}