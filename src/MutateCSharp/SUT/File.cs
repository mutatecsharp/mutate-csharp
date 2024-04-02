namespace MutateCSharp.SUT;

public abstract class File
{
  public string BasePath { get; }
  public string RelativePath { get; }

  protected File(string basePath, string relativePath)
  {
    BasePath = basePath;
    RelativePath = relativePath;
  }
}

public class TestFile : File
{
  public TestFile(string basePath, string relativePath) : base(basePath, relativePath)
  {
  }

  public override string ToString()
  {
    return $"Test File: {RelativePath}";
  }
}

public class FileUnderTest : File
{
  public FileUnderTest(string basePath, string relativePath) : base(basePath, relativePath)
  {
  }

  public override string ToString()
  {
    return $"Source File: {RelativePath}";
  }
}