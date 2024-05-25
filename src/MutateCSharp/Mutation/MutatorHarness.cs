using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using MutateCSharp.FileSystem;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Mutation.SchemataGenerator;
using MutateCSharp.Mutation.SyntaxRewriter;
using Serilog;

namespace MutateCSharp.Mutation;

public static class MutatorHarness
{
  public static async Task<(Solution, IDictionary<Project, ProjectLevelMutationRegistry>)> 
    MutateSolution(MSBuildWorkspace workspace, 
      Solution solution, 
      ImmutableArray<string> pathsToIgnore,
      ImmutableArray<string> directoriesToConsider,
      SyntaxRewriterMode mutationMode,
      bool optimise, bool dryRun)
  {
    Log.Information("Mutating solution {Solution}.",
      Path.GetFileName(solution.FilePath));
    var mutatedSolution = solution;
    var projectLevelRegistries =
      new Dictionary<Project, ProjectLevelMutationRegistry>();
    
    foreach (var projectId in solution.ProjectIds)
    {
      var project = mutatedSolution.GetProject(projectId)!;
      var (mutatedProject, projectRegistry) = 
        await MutateProject(workspace, project, pathsToIgnore, 
          directoriesToConsider, mutationMode, optimise, dryRun).ConfigureAwait(false);
      mutatedSolution = mutatedProject.Solution;

      if (projectRegistry is not null)
        projectLevelRegistries[project] = projectRegistry;
    }

    return (mutatedSolution, projectLevelRegistries);
  }

  public static async Task<(Project, ProjectLevelMutationRegistry?)>
    MutateProject(Workspace workspace, 
      Project project, 
      ImmutableArray<string> pathsToIgnore,
      ImmutableArray<string> directoriesToConsider,
      SyntaxRewriterMode mutationMode,
      bool optimise, bool dryRun,
      Document? specifiedDocument = default)
  {
    Log.Information("Mutating project {Project}.", project.Name);
    var mutatedProject = project;
    var registryBuilder = new ProjectLevelMutationRegistryBuilder();
    
    // We currently support looking up type symbols from the local project only
    using var portableExecutableStream = new MemoryStream();
    var compilation = await project.GetCompilationAsync();

    if (compilation is null)
    {
      Log.Warning("Failed to compile project {Project}.", project.Name);
      return (project, null);
    }

    // Log all referenced assemblies
    foreach (var reference in compilation.ReferencedAssemblyNames)
    {
      Log.Debug("Assembly referenced: {ReferenceName}", reference.Name);
    }

    var emitResult = compilation.Emit(portableExecutableStream);
    
    if (!emitResult.Success)
    {
      Log.Warning("Failed to emit assembly for project {Project}.",
        project.Name);
      Log.Debug("Reason for emit failure:");
      foreach (var diagnostic in emitResult.Diagnostics)
      {
        var lineSpan = diagnostic.Location.GetLineSpan();
        Log.Debug("{FileName}(Line {LineNumber}, Column {StartColumn}:{EndColumn}): {Message}", 
          Path.GetFileName(lineSpan.Path), 
          lineSpan.StartLinePosition.Line,
          lineSpan.StartLinePosition.Character,
          lineSpan.EndLinePosition.Character,
          diagnostic.GetMessage());
      }
      return (project, null);
    }

    await portableExecutableStream.FlushAsync();
    portableExecutableStream.Seek(0, SeekOrigin.Begin);
    var sutAssembly =
      AssemblyLoadContext.Default.LoadFromStream(portableExecutableStream);

    var idOfDocumentsToMutate =
      GetDocumentsToMutate(ref project, pathsToIgnore,  directoriesToConsider, specifiedDocument);
    
    foreach (var documentId in idOfDocumentsToMutate)
    {
      var document = mutatedProject.GetDocument(documentId)!;
      
      var (mutatedDocument, fileSchemaRegistry) = 
        await MutateDocument(workspace, sutAssembly, document, 
          mutationMode, optimise, dryRun).ConfigureAwait(false);
      
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
    MutateDocument(Workspace workspace, 
      Assembly sutAssembly, 
      Document document, 
      SyntaxRewriterMode mutationMode,
      bool optimise, bool dryRun)
  {
    var semanticModelTask = document.GetValidatedSemanticModel().ConfigureAwait(false);
    var tree = await document.GetValidatedSyntaxTree().ConfigureAwait(false);
    if (tree is null) return (document, null);
    
    Log.Information("{DryRunStatus}Processing source file: {SourceFilePath}",
      dryRun ? "[DryRun] " : string.Empty, document.FilePath);
    if (dryRun) return (document, null);
    
    var root = tree.GetCompilationUnitRoot();
    var mutantSchemaRegistry = new FileLevelMutantSchemaRegistry();
    var accessRewriter = new AccessModifierRewriter();

    var semanticModel = await semanticModelTask;
    if (semanticModel is null)
    {
      // 0: Modify the accessibility of declaration syntaxes
      return (document.WithSyntaxRoot(accessRewriter.Visit(root)), null);
    }

    // 1: Modify the body of the source file
    var mutationRewriter =
      new MutatorAstRewriter(sutAssembly, semanticModel, mutantSchemaRegistry, mutationMode, optimise);
    var mutatedAstRoot = (CompilationUnitSyntax)mutationRewriter.Visit(root);

    // 2: Generate mutant/tracer schemata for the source file under mutation
    var schemata = mutationMode switch
    {
      SyntaxRewriterMode.Mutate => MutantSchemataGenerator.GenerateSchemataSyntax(mutantSchemaRegistry),
      SyntaxRewriterMode.TraceExecution => ExecutionTracerSchemataGenerator.GenerateSchemataSyntax(mutantSchemaRegistry),
      _ => null
    };
    
    if (schemata is null)
    {
      Log.Information(
        "There is nothing to mutate in this source file. Proceeding...");
      return (document.WithSyntaxRoot(accessRewriter.Visit(root)), null);
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

  private static ImmutableArray<DocumentId> GetDocumentsToMutate(
    ref readonly Project project, 
    ImmutableArray<string> pathsToIgnore,
    ImmutableArray<string> directories,
    Document? specifiedDocument)
  {
    if (specifiedDocument is not null) return [specifiedDocument.Id];
    var ignorePathSet = pathsToIgnore.ToImmutableHashSet();

    var filterIgnoredPaths = ignorePathSet.IsEmpty
      ? project.Documents
      : project.Documents.Where(doc =>
      !ignorePathSet.Contains(Path.GetFullPath(doc.FilePath!)));

    if (directories.IsDefaultOrEmpty)
    {
      return [..filterIgnoredPaths.Select(doc => doc.Id)];
    }
    
    return
    [
      ..filterIgnoredPaths.Where(doc =>
          directories.Any(dir => Path.GetFullPath(doc.FilePath!).StartsWith(dir)))
        .Select(doc => doc.Id)
    ];
  }
}