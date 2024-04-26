using CommandLine;
using MutateCSharp.CLI;
using Serilog;
using Serilog.Events;

try
{
  Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("log.txt")
    .WriteTo.Console(LogEventLevel.Information)
    .CreateLogger();

  var result = await Parser.Default.ParseArguments<CliOptions>(args)
    .WithNotParsed(CliHandler.HandleParseError)
    .WithParsedAsync(CliHandler.RunOptions);

  return result.Tag is ParserResultType.Parsed ? 0 : 1;
}
catch (Exception e)
{
  Log.Error(e, "Unhandled error");
  return 1;
}
finally
{
  Log.CloseAndFlush();
}