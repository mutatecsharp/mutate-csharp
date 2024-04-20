using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Serilog;

namespace MutateCSharp.Mutation;

public static class MutatorHarness
{
  public static async Task<Solution> MutateSolution(MSBuildWorkspace workspace, Solution solution)
  {
    var mutatedSolution = solution;
    var projects = solution.Projects;

    foreach (var project in projects)
    {
      // We currently support looking up type symbols from the local project only
      using var portableExecutableStream = new MemoryStream();
      var compilation = await project.GetCompilationAsync();
      var emitResult = compilation?.Emit(portableExecutableStream).Success ?? false;
      if (!emitResult) continue;

      await portableExecutableStream.FlushAsync();
      portableExecutableStream.Seek(0, SeekOrigin.Begin);
      var sutAssembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(
        portableExecutableStream);
      
      foreach (var document in project.Documents)
      {
        Log.Debug("Processing source file: {SourceFile}", document.FilePath);
        var astRoot = await document.GetSyntaxRootAsync();
        if (astRoot is null) continue;
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel is null) continue;
        var astVisitor = new MutatorAstRewriter(sutAssembly, semanticModel);
        var mutatedAstRoot = astVisitor.Visit(astRoot);
    
        // Solutions are immutable by default; we create a new solution for each mutated document
        if (!astRoot.IsEquivalentTo(mutatedAstRoot))
          mutatedSolution = mutatedSolution.WithDocumentSyntaxRoot(document.Id, mutatedAstRoot);
      }
    }

    return mutatedSolution;
  }
}