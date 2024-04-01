using System.Reflection;

namespace MutateCSharp.Util;

public static class MethodTimeLogger
{
  public static void Log(MethodBase _, TimeSpan elapsed, string message)
  {
    var (time, unit) = FormatTimeSpan(elapsed);
    Serilog.Log.Information("{Message} ({Elapsed:0.##} {ElapsedUnit})", message, time, unit);
  }

  private static (double, string) FormatTimeSpan(TimeSpan timeSpan)
  {
    return
      timeSpan.TotalDays >= 1 ? (timeSpan.TotalDays, "day(s)")
      : timeSpan.TotalHours >= 1 ? (timeSpan.TotalHours, "hour(s)")
      : timeSpan.TotalMinutes >= 1 ? (timeSpan.TotalMinutes, "minute(s)")
      : timeSpan.TotalSeconds >= 1 ? (timeSpan.TotalSeconds, "second(s)")
      : timeSpan.TotalMilliseconds >= 1 ? (timeSpan.TotalMilliseconds, "millisecond(s)")
      : timeSpan.TotalMicroseconds >= 1 ? (timeSpan.TotalMicroseconds, "microsecond(s)")
      : (timeSpan.TotalNanoseconds, "nanosecond(s)");
  }
}