using CommandLine;
using MutateCSharp.Cli;
using Serilog;
using Serilog.Events;

try
{
  Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.File("log.txt")
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();
  
  Log.Debug("{Args}", args);

  var result = Parser.Default.ParseArguments<CliOptions>(args)
    .WithParsed(CliHandler.RunOptions)
    .WithNotParsed(CliHandler.HandleParseError);

  return (result.Tag is ParserResultType.Parsed) ? 0 : 1;
}
catch (Exception e)
{
  Log.Error(e, "Unknown error");
  return 1;
}
finally
{
  Log.CloseAndFlush();
}
