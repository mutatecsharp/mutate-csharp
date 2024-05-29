using System.Collections.Frozen;

namespace MutateCSharp.Util;

public static class TestCaseUtil
{
  private static readonly FrozenSet<char> InvalidFileNameChars = 
    Path.GetInvalidFileNameChars().ToFrozenSet();
  
  public static string ValidTestFileName(string originalTestName)
  {
    return string.Join(string.Empty,
      originalTestName.Where(c => !InvalidFileNameChars.Contains(c)));
  }
}