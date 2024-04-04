using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Serilog;

namespace MutateCSharp.Mutation;

public static class Mutator
{
  public static async Task<Solution> MutateSolution(MSBuildWorkspace workspace, Solution solution)
  {
    var mutatedSolution = solution;
    var documents = solution.Projects.SelectMany(p => p.Documents);
    
    foreach (var document in documents)
    {
      Log.Debug("Processing source file: {SourceFile}", document.FilePath);
      var astRoot = await document.GetSyntaxRootAsync();
      if (astRoot is null) continue;
      var semanticModel = await document.GetSemanticModelAsync();
      if (semanticModel is null) continue;
      var astVisitor = new MutatorAstRewriter(semanticModel);
      var mutatedAstRoot = astVisitor.Visit(astRoot);
      
      // Solutions are immutable by default; we create a new solution for each mutated document
      if (!astRoot.IsEquivalentTo(mutatedAstRoot))
        mutatedSolution = mutatedSolution.WithDocumentSyntaxRoot(document.Id, mutatedAstRoot);
    }

    return mutatedSolution;
  }
}