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

  var result = Parser.Default.ParseArguments<MutateOptions, AnalyseOptions,
      GenerateTracerOptions, TraceOptions>(args)
    .WithNotParsed(MutateHandler.HandleParseError);
  result = await result.WithParsedAsync<MutateOptions>(MutateHandler.RunOptions);
  result = await result.WithParsedAsync<AnalyseOptions>(AnalyseHandler.RunOptions);
  result = await result.WithParsedAsync<GenerateTracerOptions>(MutateHandler.RunOptions);
  result = await result.WithParsedAsync<TraceOptions>(TraceHandler.RunOptions);
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