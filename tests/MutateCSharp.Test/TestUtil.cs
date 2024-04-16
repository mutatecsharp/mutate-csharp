using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
      .NotBeEmpty("because input should not be empty");

    // Check: Input should not have syntax errors.
    tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
      .Should()
      .BeEmpty("because input should be syntactically valid");
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