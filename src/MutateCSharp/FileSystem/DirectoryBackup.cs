using Serilog;

namespace MutateCSharp.FileSystem;

/*
 * Creates a backup folder within the same directory, and restores the folder
 * contents upon exit scope of object.
 */
public sealed class DirectoryBackup : IDisposable
{
  private const string BackupFolderName = ".mutate-csharp";
  private readonly string _originalDirectoryPath;
  private readonly string _backupDirectoryPath;

  public DirectoryBackup(string directoryPath)
  {
    _originalDirectoryPath = directoryPath;
    _backupDirectoryPath = Path.Combine(directoryPath, BackupFolderName);
    Directory.CreateDirectory(_backupDirectoryPath);
    CopyDirectoryContentsExceptBackup(_originalDirectoryPath, _backupDirectoryPath);
  }

  public static DirectoryBackup? BackupDirectoryIfNecessary(string directoryPath, bool necessary)
  {
    if (!necessary) return null;
    Log.Information("Temporary backup directory: {BackupDirectory}", directoryPath);
    return new DirectoryBackup(directoryPath);
  }

  public void Dispose()
  {
    DeleteDirectoryContentsExceptBackup(_originalDirectoryPath);
    CopyDirectoryContentsExceptBackup(_backupDirectoryPath, _originalDirectoryPath);
    Directory.Delete(_backupDirectoryPath, recursive: true);
    Log.Information("Restore original directory content and remove temporary backup directory");
  }

  private void CopyDirectoryContentsExceptBackup(string srcDir, string destDir)
  {
    // Copy files
    foreach (var srcFile in Directory.GetFiles(srcDir))
    {
      var fileName = Path.GetFileName(srcFile);
      var destFile = Path.Combine(destDir, fileName);
      File.Copy(srcFile, destFile, overwrite: true);
    }
    
    // Copy subdirectories except in backup folder
    foreach (var srcSubDir in Directory.GetDirectories(srcDir))
    {
      if (srcSubDir.Equals(_backupDirectoryPath)) continue;
      var subDirName = Path.GetFileName(srcSubDir);
      var destSubDir = Path.Combine(destDir, subDirName);
      Directory.CreateDirectory(destSubDir);
      CopyDirectoryContentsExceptBackup(srcSubDir, destSubDir);
    }
  }

  private void DeleteDirectoryContentsExceptBackup(string directory)
  {
    // Delete files in directory
    foreach (var file in Directory.GetFiles(directory))
    {
      File.Delete(file);
    }
    
    // Delete subdirectories except backup directory
    foreach (var dir in Directory.GetDirectories(directory))
    {
      if (dir.Equals(_backupDirectoryPath)) continue;
      Directory.Delete(dir, recursive: true);
    }
  }
}