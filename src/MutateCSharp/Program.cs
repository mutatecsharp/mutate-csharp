using Serilog;
using Serilog.Events;

try
{
  Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("log.txt")
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();
  
  Log.Debug("Hello, world!");
}
catch (Exception error)
{
  Log.Error(error, "Unhandled exception");
}
finally
{
  Log.CloseAndFlush();
}
