using Serilog;

namespace MutateCSharp.Mutation;

public class Mutator
{
  public static IEnumerable<Mutant> DeclareMutants(string projectDirectory)
  {
    var files = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
    files.ToList().ForEach(file => Log.Information("File: {CSharpFile}", file));
    return [];
  }
}