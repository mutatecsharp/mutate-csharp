﻿using CommandLine;
using MutateCSharp.CLI;
using Serilog;
using Serilog.Events;

try
{
  Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(path: "log.txt")
    .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

  var result = Parser.Default.ParseArguments<CliOptions>(args)
    .WithParsed(CliHandler.RunOptions)
    .WithNotParsed(CliHandler.HandleParseError);

  return (result.Tag is ParserResultType.Parsed) ? 0 : 1;
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