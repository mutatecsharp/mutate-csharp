using System.Reflection;
using System.Text;
using Xunit.Sdk;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Registry;

namespace MutateCSharp.Test;

public static class TestUtil
{
  private static readonly CSharpCompilationOptions TestCompileOptions =
    new CSharpCompilationOptions(OutputKind.ConsoleApplication)
      .WithNullableContextOptions(NullableContextOptions.Enable);
  
  private static readonly PortableExecutableReference MicrosoftCoreLibrary =
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

  public static CSharpCompilation GetCompilation(SyntaxTree tree)
  {
    return CSharpCompilation.Create(
      assemblyName: Path.GetRandomFileName(),
      syntaxTrees: [tree],
      references: [MicrosoftCoreLibrary],
      options: TestCompileOptions);
  }

  public static (SemanticModel model, Assembly sutAssembly)
    GetAstSemanticModelAndAssembly(SyntaxTree tree)
  {
    var compilation = GetCompilation(tree);

    // Obtain semantic model of input
    var model = compilation.GetSemanticModel(tree);
    model.Should().NotBeNull();
    TestForSemanticErrors(model);

    // Get the compiled output containing type metadata of input
    using var portableExecutableStream = new MemoryStream();

    var emitResult = compilation.Emit(portableExecutableStream);
    emitResult.Success.Should().BeTrue();

    portableExecutableStream.Flush();
    portableExecutableStream.Seek(0, SeekOrigin.Begin);

    var sutAssembly =
      System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(
        portableExecutableStream);
    sutAssembly.Should().NotBeNull();

    return (model, sutAssembly);
  }

  public static void TestForSyntacticErrors(SyntaxTree tree)
  {
    // Check: Input should have a non-empty syntax tree.
    tree.GetRoot().DescendantNodesAndSelf().Should()
      .NotBeEmpty("because empty source is trivially compilable");

    // Check: Input should not have syntax errors.
    tree.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error)
      .Should()
      .BeFalse("because input should be syntactically valid");
  }

  public static void TestForSemanticErrors(SemanticModel model)
  {
    var sb = new StringBuilder();

    foreach (var diagnostic in model.Compilation.GetDiagnostics())
    {
      if (diagnostic.Severity == DiagnosticSeverity.Error)
      {
        sb.AppendLine(diagnostic.GetMessage());
      }
    }

    // Check: Input should not have semantic errors.
    model.Compilation.GetDiagnostics()
      .Any(d => d.Severity == DiagnosticSeverity.Error)
      .Should().BeFalse($"because input should be semantically valid\n{sb}");
  }

  public static void ShouldNotHaveValidMutationGroup<T, TU>(
    string inputUnderMutation)
    where T : IMutationOperator
    where TU : SyntaxNode
  {
    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    TestForSyntacticErrors(inputAst);

    var compilation = GetAstSemanticModelAndAssembly(inputAst);

    var mutationOperator = (T)Activator.CreateInstance(typeof(T),
      compilation.sutAssembly, compilation.model)!;
    var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<TU>().FirstOrDefault();

    var mutationGroup =
      mutationOperator.CreateMutationGroup(constructUnderTest);
    mutationGroup.Should().BeNull();
  }

  public static MutationGroup GetValidMutationGroup<T, TU>(
    string inputUnderMutation)
    where T : IMutationOperator
    where TU : SyntaxNode
  {
    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    TestForSyntacticErrors(inputAst);

    var compilation = GetAstSemanticModelAndAssembly(inputAst);

    var mutationOperator = (T)Activator.CreateInstance(typeof(T),
      compilation.sutAssembly, compilation.model)!;
    var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<TU>().FirstOrDefault();
    constructUnderTest.Should()
      .NotBeNull("because at least one construct of specified type exists");

    var mutationGroup =
      mutationOperator.CreateMutationGroup(constructUnderTest);
    mutationGroup.Should().NotBeNull();

    return mutationGroup!;
  }

  public static MutationGroup[] GetAllValidMutationGroups<T, TU>(
    string inputUnderMutation)
    where T : IMutationOperator
    where TU : SyntaxNode
  {
    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    TestForSyntacticErrors(inputAst);

    var compilation = GetAstSemanticModelAndAssembly(inputAst);

    var mutationOperator = (T)Activator.CreateInstance(typeof(T),
      compilation.sutAssembly, compilation.model)!;
    var constructsUnderTest = inputAst.GetCompilationUnitRoot()
      .DescendantNodes()
      .OfType<TU>().ToArray();
    constructsUnderTest.Should()
      .NotBeEmpty("because at least one construct of specified type exists");

    var mutationGroups = constructsUnderTest
      .Select(mutationOperator.CreateMutationGroup)
      .Where(group => group != null)
      .Select(group => group!)
      .ToArray();
    mutationGroups.Should()
      .NotBeEmpty("because at least one mutation group should exist");

    return mutationGroups;
  }

  public static SyntaxNode GetNodeUnderMutationAfterRewrite<T>(
    string inputUnderMutation,
    FileLevelMutantSchemaRegistry schemaRegistry,
    Func<MutatorAstRewriter, T, SyntaxNode> visitSyntaxUnderTest)
    where T : SyntaxNode
  {
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var compilation = GetAstSemanticModelAndAssembly(ast);
    var rewriter = new MutatorAstRewriter(
      compilation.sutAssembly, compilation.model, schemaRegistry);
    var construct = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<T>().First();
    return visitSyntaxUnderTest(rewriter, construct);
  }

  public static IList<ArgumentSyntax> GetReplacedNodeArguments(
    SyntaxNode node, FileLevelMutantSchemaRegistry schemaRegistry)
  {
    node.Should().BeOfType<InvocationExpressionSyntax>();
    var mutantConstruct = (InvocationExpressionSyntax)node;

    // Get method
    mutantConstruct.Expression.Should()
      .BeOfType<MemberAccessExpressionSyntax>();
    var methodMemberAccessExpr =
      (MemberAccessExpressionSyntax)mutantConstruct.Expression;

    // Check class name matches
    methodMemberAccessExpr.Expression.Should()
      .BeOfType<MemberAccessExpressionSyntax>();
    var classMemberAccessExpr =
      (MemberAccessExpressionSyntax)methodMemberAccessExpr.Expression;
    classMemberAccessExpr.Name.Identifier.Text.Should()
      .BeEquivalentTo(schemaRegistry.ClassName);

    // Check namespace name matches
    classMemberAccessExpr.Expression.Should()
      .BeOfType<IdentifierNameSyntax>();
    var namespaceMemberAccessExpr =
      (IdentifierNameSyntax)classMemberAccessExpr.Expression;
    namespaceMemberAccessExpr.Identifier.Text.Should()
      .BeEquivalentTo(MutantSchemataGenerator.Namespace);

    // Extract arguments to mutant schema method invocation
    var args = mutantConstruct.ArgumentList.Arguments;

    // First argument will always be mutant ID assignment (numeric literal)
    var mutantIdAssignment = args[0].Expression;
    mutantIdAssignment.Should().BeOfType<LiteralExpressionSyntax>();

    // Validate mutant ID is a numeric literal and has type "long"
    var mutantId = (LiteralExpressionSyntax)mutantIdAssignment;
    mutantId.IsKind(SyntaxKind.NumericLiteralExpression).Should()
      .BeTrue();
    mutantId.Token.Text.Should().EndWith("L");

    // Drop mutantId argument and return the rest of the arguments of interest
    return args.Skip(1).ToList();
  }

  public static void NodeShouldNotBeMutated(SyntaxNode node, FileLevelMutantSchemaRegistry schemaRegistry)
  {
    // Check node is not method invocation
    if (node is not InvocationExpressionSyntax methodInvocation) return;
    // Check method is not from mutant schema class
    if (methodInvocation.Expression is not
        MemberAccessExpressionSyntax methodMemberAccessExpr)
      return;
    // Check class does not have the same name as the mutant schemata class name
    if (methodMemberAccessExpr.Expression is not
        MemberAccessExpressionSyntax classMemberAccessExpr)
      return;
    classMemberAccessExpr.Name.Identifier.Text.Should()
      .NotBe(schemaRegistry.ClassName);
  }

  public static IEnumerable<string> GetMutantExpressionTemplates(
    MutationGroup group)
    => group.SchemaMutantExpressions.Select(mutant =>
      mutant.ExpressionTemplate);

  public static IEnumerable<object[]>
    GenerateMutationTestCases(IEnumerable<string> operatorGroup)
  {
    var opSet = operatorGroup.ToHashSet();
    return opSet.Select(op => new object[]
      { op, opSet.Except([op]).ToArray() });
  }

  public static IEnumerable<object[]>
    GenerateTestCaseCombinationsBetweenTypeAndMutations(
      IEnumerable<string> types, IEnumerable<string> operatorGroup)
  {
    var opSet = operatorGroup.ToHashSet();
    var mutations = GenerateMutationTestCases(opSet);

    // Get unique pairs between types and operators
    return types.SelectMany(type =>
      mutations.Select(group => new[]
        { type, group[0], group[1] }));
  }

  public static IEnumerable<object[]>
    GenerateTestCaseCombinations(IEnumerable<object> left,
      IEnumerable<object> right)
  {
    var rightArray = right.ToArray();
    
    foreach (var leftItem in left)
    {
      foreach (var rightItem in rightArray)
      {
        yield return [leftItem, rightItem];
      }
    }
  }
}