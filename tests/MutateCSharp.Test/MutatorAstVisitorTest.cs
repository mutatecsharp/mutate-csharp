using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MutateCSharp.Mutation;
using FluentAssertions;
using Xunit.Abstractions;

namespace MutateCSharp.Test;

public class MutatorAstVisitorTest
{
  private readonly ITestOutputHelper _testOutputHelper;
  private const string CompilationName = "AstTestCompilation";

  private static readonly PortableExecutableReference MicrosoftCoreLibrary =
    MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

  public MutatorAstVisitorTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }

  private static void TestForSyntacticErrors(SyntaxTree tree)
  {
    // Check: Input should have a non-empty syntax tree.
    tree.GetRoot().DescendantNodesAndSelf().Should()
      .NotBeEmpty("because input should not be empty");

    // Check: Input should not have syntax errors.
    tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
      .Should()
      .BeEmpty("because input should be syntactically valid");
  }

  private void TestMutation(string input, string expected)
  {
    // Sanity check: input should be syntactically and semantically valid
    var inputAst = CSharpSyntaxTree.ParseText(input);
    TestForSyntacticErrors(inputAst);

    var compilation = CSharpCompilation.Create(CompilationName)
      .WithReferences(MicrosoftCoreLibrary)
      .AddSyntaxTrees(inputAst);
    var model = compilation.GetSemanticModel(inputAst);
    var visitor = new MutatorAstRewriter(model);

    // Sanity check: actual output should be syntactically valid
    var outputRoot = visitor.Visit(inputAst.GetRoot());
    TestForSyntacticErrors(outputRoot.SyntaxTree);

    // Sanity check: expected output should be syntactically valid
    var expectedAst = CSharpSyntaxTree.ParseText(expected);
    TestForSyntacticErrors(expectedAst);

    // Check: Actual output should be equivalent to the expected output
    // Comparison ignores trivia (whitespaces)
    // Hack: convert output AST to string and parse string back to AST
    var outputAst =
      CSharpSyntaxTree.ParseText(outputRoot.ToFullString());
    _testOutputHelper.WriteLine(outputAst.ToString());
    outputAst.IsEquivalentTo(expectedAst).Should().BeTrue();
  }

  [Fact]
  public void MutateIntegerConstantAssignment_ShouldReplaceIntegerConstant()
  {
    const string input =
      """
      void f()
      {
        int x = 2;
      }
      """;

    const string expected =
      """
      void f()
      {
        int x = MutateCSharp.Schemata.ReplaceInt32Constant12345(0, 2);
      }
      """;

    TestMutation(input, expected);
  }

  [Fact]
  public void MutateBooleanConstantAssignment_ShouldReplaceBooleanConstant()
  {
    const string input =
      """
      void f()
      {
        bool x = true;
      }
      """;

    const string expected =
      """
      void f()
      {
        bool x = MutateCSharp.Schemata.ReplaceBooleanConstant1(0, true);
      }
      """;

   TestMutation(input, expected);
  }
  
  [Fact]
  public void MutateDoubleConstantAssignment_ShouldReplaceDoubleConstant()
  {
    const string input =
      """
      void f()
      {
        double x = 2.0d;
      }
      """;

    const string expected =
      """
      void f()
      {
        double x = MutateCSharp.Schemata.ReplaceDoubleConstant12345(0, 2.0d);
      }
      """;

    TestMutation(input, expected);
  }
}