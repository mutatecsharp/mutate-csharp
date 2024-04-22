using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using Serilog;

namespace MutateCSharp.Mutation;

public static class MutatorHarness
{
    
  // TODO: return the mutated project
  private static async void MutateProject(Workspace workspace, Project project)
  {
    // We currently support looking up type symbols from the local project only
    using var portableExecutableStream = new MemoryStream();
    var compilation = await project.GetCompilationAsync();
    var emitResult = compilation?.Emit(portableExecutableStream).Success ?? false;
    if (!emitResult) return;

    await portableExecutableStream.FlushAsync();
    portableExecutableStream.Seek(0, SeekOrigin.Begin);
    var sutAssembly =
      System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(
        portableExecutableStream);

    foreach (var document in project.Documents)
    {
      _ = await MutateDocument(workspace, sutAssembly, document);
    }
  }

  private static async Task<Document> MutateDocument(Workspace workspace, Assembly sutAssembly, Document document)
  {
    Log.Debug("Processing source file: {SourceFile}", document.FilePath);
    if (!document.SupportsSyntaxTree || !document.SupportsSemanticModel)
      return document;
    
    // Don't mutate if there is nothing to mutate
    if (await document.GetSyntaxRootAsync() is not CompilationUnitSyntax astRoot) return document;
    if (astRoot.Members.Count == 0)
    {
      Log.Information("There is nothing to mutate in this source file. Proceeding...");
      return document;
    }
    var semanticModel = (await document.GetSemanticModelAsync())!;
    
    // Modify the body of the source file
    var astVisitor = new MutatorAstRewriter(sutAssembly, semanticModel);
    var mutatedAstRoot = (CompilationUnitSyntax) astVisitor.Visit(astRoot);
    
    // Generate mutant schemata
    var schemata = 
      MutantSchemataGenerator.GenerateSchemataSyntax(astVisitor.GetRegistry());
    if (schemata is null)
    {
      Log.Information("There is nothing to mutate in this source file. Proceeding...");
      return document;
    }
    
    var formattedSchemata = Formatter.Format(schemata, workspace);
    
    // Inject mutant schemata to mutated source code
    mutatedAstRoot = mutatedAstRoot.InsertNodesBefore(
      mutatedAstRoot.Members.First(),
      new [] {formattedSchemata}
    );
    
    return document.WithSyntaxRoot(mutatedAstRoot);
  }
  
  public static async Task<Solution> MutateSolution(MSBuildWorkspace workspace, Solution solution)
  {
    var mutatedSolution = solution;
    var projects = solution.Projects;

    foreach (var project in projects)
    {
      MutateProject(workspace, project);
    }

    // // Solutions are immutable by default; we create a new solution for each mutated document
    // if (!astRoot.IsEquivalentTo(mutatedAstRoot))
    //   mutatedSolution = mutatedSolution.WithDocumentSyntaxRoot(document.Id, mutatedAstRoot);
    return mutatedSolution;
  }
}