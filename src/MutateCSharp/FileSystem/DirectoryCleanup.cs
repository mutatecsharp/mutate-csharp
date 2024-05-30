namespace MutateCSharp.FileSystem;

public static class DirectoryCleanup
{
  public static async Task DeleteAllFilesAndFoldersRecursively(string directory)
  {
    if (Directory.Exists(directory))
    {
      var files = Directory.GetFiles(directory);
      var subdir = Directory.GetDirectories(directory);
      
      var deleteFileTasks = files
        .Select(file => Task.Run(() => File.Delete(file)));
      var deleteSubdirectoryTasks =
        subdir.Select(dir => Task.Run(() => Directory.Delete(dir, true)));

      await Task.WhenAll(deleteFileTasks);
      await Task.WhenAll(deleteSubdirectoryTasks);
    }
  }
}