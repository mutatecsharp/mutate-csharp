using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MutateCSharp.Mutation;

namespace MutateCSharp.Test;

public static class TestUtil
{
  private static readonly PortableExecutableReference MicrosoftCoreLibrary =
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

  public static (SemanticModel model, Assembly sutAssembly)
    GetAstSemanticModelAndAssembly(SyntaxTree tree)
  {
    var compilation = CSharpCompilation.Create(Path.GetRandomFileName())
      .WithReferences(MicrosoftCoreLibrary)
      .AddSyntaxTrees(tree);

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
    // Check: Input should not have semantic errors.
    model.Compilation.GetDiagnostics()
      .Any(d => d.Severity == DiagnosticSeverity.Error)
      .Should().BeFalse("because input should be semantically valid");
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
}