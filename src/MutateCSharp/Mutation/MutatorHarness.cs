using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using MutateCSharp.FileSystem;
using Serilog;

namespace MutateCSharp.Mutation;

public static class MutatorHarness
{
  public static async Task<Solution> MutateSolution(
    MSBuildWorkspace workspace,
    Solution solution)
  {
    Log.Information("Mutating solution {Solution}.",
      Path.GetFileName(solution.FilePath));
    var mutatedSolution = solution;

    foreach (var projectId in solution.ProjectIds)
    {
      var project = mutatedSolution.GetProject(projectId)!;
      var mutatedProject = await MutateProject(workspace, project);
      mutatedSolution = mutatedProject.Solution;
    }

    var mutateResult = workspace.TryApplyChanges(mutatedSolution);
    if (!mutateResult)
      Log.Error("Failed to mutate solution {Solution}.",
        Path.GetFileName(solution.FilePath));

    return workspace.CurrentSolution;
  }

  private static async Task<Project> MutateProject(
    Workspace workspace,
    Project project)
  {
    Log.Information("Mutating project {Project}.", project.Name);
    var mutatedProject = project;
    // We currently support looking up type symbols from the local project only
    using var portableExecutableStream = new MemoryStream();
    var compilation = await project.GetCompilationAsync();
    var emitResult =
      compilation?.Emit(portableExecutableStream).Success ?? false;
    if (!emitResult)
    {
      Log.Warning("Failed to compile project {Project}. Proceeding...",
        project.Name);
      return project;
    }

    await portableExecutableStream.FlushAsync();
    portableExecutableStream.Seek(0, SeekOrigin.Begin);
    var sutAssembly =
      AssemblyLoadContext.Default.LoadFromStream(
        portableExecutableStream);
    var mutationRegistry = new MutantSchemaRegistry();

    foreach (var documentId in project.DocumentIds)
    {
      var document = mutatedProject.GetDocument(documentId)!;
      var mutatedDocument = await MutateDocument(workspace, sutAssembly,
        document, mutationRegistry);
      mutatedProject = mutatedDocument.Project;
    }

    return mutatedProject;
  }

  private static async Task<Document> MutateDocument(
    Workspace workspace,
    Assembly sutAssembly,
    Document document,
    MutantSchemaRegistry mutantSchemaRegistry)
  {
    var tree = await document.GetValidatedSyntaxTree();
    if (tree is null) return document;

    var semanticModel = await document.GetValidatedSemanticModel();
    if (semanticModel is null) return document;

    Log.Information("Processing source file: {SourceFilePath}",
      document.FilePath);
    var root = tree.GetCompilationUnitRoot();

    // 1: Modify the body of the source file
    var astVisitor =
      new MutatorAstRewriter(sutAssembly, semanticModel, mutantSchemaRegistry);
    var mutatedAstRoot = (CompilationUnitSyntax)astVisitor.Visit(root);

    // 2: Generate mutant schemata for the source file under mutation
    // todo: index by file id
    var schemata =
      MutantSchemataGenerator.GenerateSchemataSyntax(mutantSchemaRegistry);
    if (schemata is null)
    {
      Log.Information(
        "There is nothing to mutate in this source file. Proceeding...");
      return document;
    }

    // 3: Inject mutant schemata to mutated source code
    mutatedAstRoot = mutatedAstRoot.InsertNodesBefore(
      mutatedAstRoot.Members.First(),
      new[] { schemata }
    );

    // 4: Format mutated document
    mutatedAstRoot =
      (CompilationUnitSyntax)Formatter.Format(mutatedAstRoot, workspace);

    return document.WithSyntaxRoot(mutatedAstRoot);
  }
}