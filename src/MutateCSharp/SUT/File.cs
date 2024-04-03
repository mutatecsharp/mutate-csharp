namespace MutateCSharp.SUT;

public abstract class File
{
  public string AbsolutePath { get; }

  protected File(string absolutePath)
  {
    AbsolutePath = absolutePath;
  }
}

public class TestFile : File
{
  public TestFile(string absolutePath) : base(absolutePath)
  {
  }

  public override string ToString()
  {
    return $"Test File: {AbsolutePath}";
  }
}

public class FileUnderTest : File
{

  public FileUnderTest(string absolutePath) : base(absolutePath)
  {
  }

  public override string ToString()
  {
    return $"Source File: {AbsolutePath}";
  }
}