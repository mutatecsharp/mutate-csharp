using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MutateCSharp.Mutation;
using FluentAssertions;
using Xunit.Abstractions;

namespace MutateCSharp.Test;

public class MutatorAstVisitorTest
{
  private readonly ITestOutputHelper _testOutputHelper;

  public MutatorAstVisitorTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }
  
  private void TestMutation(string input, string expected)
  {
    // Sanity check: input should be syntactically and semantically valid
    var inputAst = CSharpSyntaxTree.ParseText(input);
    TestUtil.TestForSyntacticErrors(inputAst);

    var compilation = TestUtil.GetAstCompilation(inputAst);
    var model = compilation.GetSemanticModel(inputAst);
    var visitor = new MutatorAstRewriter(model);

    // Sanity check: actual output should be syntactically valid
    var outputRoot = visitor.Visit(inputAst.GetRoot());
    TestUtil.TestForSyntacticErrors(outputRoot.SyntaxTree);

    // Sanity check: expected output should be syntactically valid
    var expectedAst = CSharpSyntaxTree.ParseText(expected);
    TestUtil.TestForSyntacticErrors(expectedAst);

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
    var t = typeof(int).GetMethods();

    foreach (var m in t)
    {
      _testOutputHelper.WriteLine(m.Name);
    }
    
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