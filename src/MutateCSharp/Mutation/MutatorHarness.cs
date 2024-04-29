using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using MutateCSharp.FileSystem;
using MutateCSharp.Mutation.Registry;
using Serilog;

namespace MutateCSharp.Mutation;

public static class MutatorHarness
{
  public static async Task<(Solution, IDictionary<Project, ProjectLevelMutationRegistry>)> 
    MutateSolution(MSBuildWorkspace workspace, Solution solution)
  {
    Log.Information("Mutating solution {Solution}.",
      Path.GetFileName(solution.FilePath));
    var mutatedSolution = solution;
    var projectLevelRegistries =
      new Dictionary<Project, ProjectLevelMutationRegistry>();
    
    foreach (var projectId in solution.ProjectIds)
    {
      var project = mutatedSolution.GetProject(projectId)!;
      var (mutatedProject, projectRegistry) = await MutateProject(workspace, project);
      mutatedSolution = mutatedProject.Solution;

      if (projectRegistry is not null)
        projectLevelRegistries[project] = projectRegistry;
    }

    return (mutatedSolution, projectLevelRegistries);
  }

  public static async Task<(Project, ProjectLevelMutationRegistry?)> 
    MutateProject(Workspace workspace, Project project)
  {
    Log.Information("Mutating project {Project}.", project.Name);
    var mutatedProject = project;
    var registryBuilder = new ProjectLevelMutationRegistryBuilder();
    
    // We currently support looking up type symbols from the local project only
    using var portableExecutableStream = new MemoryStream();
    var compilation = await project.GetCompilationAsync();
    var emitResult =
      compilation?.Emit(portableExecutableStream).Success ?? false;
    if (!emitResult)
    {
      Log.Warning("Failed to compile project {Project}.",
        project.Name);
      return (project, null);
    }

    await portableExecutableStream.FlushAsync();
    portableExecutableStream.Seek(0, SeekOrigin.Begin);
    var sutAssembly =
      AssemblyLoadContext.Default.LoadFromStream(portableExecutableStream);

    foreach (var documentId in project.DocumentIds)
    {
      var document = mutatedProject.GetDocument(documentId)!;
      var (mutatedDocument, fileSchemaRegistry) = 
        await MutateDocument(workspace, sutAssembly, document);
      
      // Record mutations in registry
      var relativePath = Path.GetRelativePath(
        Path.GetDirectoryName(project.FilePath)!, document.FilePath!);
      
      mutatedProject = mutatedDocument.Project;
      
      if (fileSchemaRegistry is not null)
        registryBuilder.AddRegistry(fileSchemaRegistry.ToMutationRegistry(relativePath));
    }
    
    return (mutatedProject, registryBuilder.ToFinalisedRegistry());
  }

  private static async Task<(Document, FileLevelMutantSchemaRegistry?)> 
    MutateDocument(Workspace workspace, Assembly sutAssembly, Document document)
  {
    var tree = await document.GetValidatedSyntaxTree();
    if (tree is null) return (document, null);

    var semanticModel = await document.GetValidatedSemanticModel();
    if (semanticModel is null) return (document, null);

    Log.Information("Processing source file: {SourceFilePath}",
      document.FilePath);
    var root = tree.GetCompilationUnitRoot();
    var mutantSchemaRegistry = new FileLevelMutantSchemaRegistry();

    // 1: Modify the body of the source file
    var astVisitor =
      new MutatorAstRewriter(sutAssembly, semanticModel, mutantSchemaRegistry);
    var mutatedAstRoot = (CompilationUnitSyntax)astVisitor.Visit(root);

    // 2: Generate mutant schemata for the source file under mutation
    var schemata =
      MutantSchemataGenerator.GenerateSchemataSyntax(mutantSchemaRegistry);
    if (schemata is null)
    {
      Log.Information(
        "There is nothing to mutate in this source file. Proceeding...");
      return (document, null);
    }

    // 3: Inject mutant schemata to mutated source code
    mutatedAstRoot = mutatedAstRoot.InsertNodesBefore(
      mutatedAstRoot.Members.First(),
      new[] { schemata }
    );

    // 4: Format mutated document
    mutatedAstRoot =
      (CompilationUnitSyntax)Formatter.Format(mutatedAstRoot, workspace);
    
    return (document.WithSyntaxRoot(mutatedAstRoot), mutantSchemaRegistry);
  }
}