using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MutateCSharp.Mutation;

namespace MutateCSharp.Test;

public static class TestUtil
{
  private static readonly PortableExecutableReference MicrosoftCoreLibrary =
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

  public static CSharpCompilation GetAstCompilation(SyntaxTree tree)
  { 
    return CSharpCompilation.Create(Path.GetRandomFileName())
      .WithReferences(MicrosoftCoreLibrary)
      .AddSyntaxTrees(tree);
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
  where T: IMutationOperator
  where TU: SyntaxNode
  {
    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    TestForSyntacticErrors(inputAst);

    var compilation = GetAstCompilation(inputAst);
    var model = compilation.GetSemanticModel(inputAst);
    model.Should().NotBeNull();
    TestForSemanticErrors(model);

    var mutationOperator = (T)Activator.CreateInstance(typeof(T), model)!;
    var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<TU>().FirstOrDefault();

    var mutationGroup = mutationOperator.CreateMutationGroup(constructUnderTest);
    mutationGroup.Should().BeNull();
  }

  public static MutationGroup GetValidMutationGroup<T, TU>(
    string inputUnderMutation) 
    where T: IMutationOperator
    where TU: SyntaxNode
  {
    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    TestForSyntacticErrors(inputAst);

    var compilation = GetAstCompilation(inputAst);
    var model = compilation.GetSemanticModel(inputAst);
    model.Should().NotBeNull();
    TestForSemanticErrors(model);

    var mutationOperator = (T) Activator.CreateInstance(typeof(T), model)!;
    var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<TU>().FirstOrDefault();
    constructUnderTest.Should().NotBeNull("because at least one construct of specified type exists");

    var mutationGroup = mutationOperator.CreateMutationGroup(constructUnderTest);
    mutationGroup.Should().NotBeNull();

    return mutationGroup!;
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