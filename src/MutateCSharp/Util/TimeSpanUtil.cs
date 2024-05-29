namespace MutateCSharp.Util;

public static class TimeSpanUtil
{
  public static TimeSpan Scale(this TimeSpan timeSpan, int factor)
  {
    return TimeSpan.FromMilliseconds(timeSpan.Milliseconds * factor);
  }
}