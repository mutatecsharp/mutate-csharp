using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Registry;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Visitor;

public class VisitIrreplacableConstructTest(ITestOutputHelper testOutputHelper)
{
  [Theory]
  [InlineData("new string[2] { \"abc\", \"def\" };")]
  [InlineData("""
              new string[2, 2]
              {
                {"abc", "def"},
                {"abc", "def"}
              };
              """)]
  public void ShouldNotReplaceArrayRankIfArrayRankAndInitializerExists(string construct)
  {
    var inputUnderMutation =
      $$"""
      using System;
      
      public class A
      {
        public static void Main()
        {
          var x = {{construct}}
        }
      }
      """;
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var node = TestUtil.GetNodeUnderMutationAfterRewrite
      <ArrayCreationExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitArrayCreationExpression(node)
      );

    node.Should().BeOfType<ArrayCreationExpressionSyntax>();
    // Verify all expressions that specify the array size is not mutated
    var arrayCreationSyntax = (ArrayCreationExpressionSyntax)node;
    var sizes = arrayCreationSyntax.Type.RankSpecifiers
      .SelectMany(rank => rank.Sizes);
    
    foreach (var size in sizes)
      TestUtil.NodeShouldNotBeMutated(size, schemaRegistry);
  }

  // Strictly speaking the left operand of the is expression should be mutated
  // but being conservative is sound, just not complete
  [Theory]
  [InlineData("int.MaxValue is int n")]
  [InlineData("!(!!(\"abc\" is string s) && !(true is bool b))")]
  [InlineData("!(false is bool b)")]
  [InlineData("!!!(\"def\" is string s)")]
  public void CannotReplaceNodeContainingDeclarationPatternSyntaxAsDescendant(
    string construct)
  {
    var inputUnderMutation =
      $$"""
      using System;
      
      public class A
      {
        public static void Main()
        {
          var y = {{construct}};
        } 
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();
    
    var varDeclSyntax = (VariableDeclaratorSyntax) TestUtil.GetNodeUnderMutationAfterRewrite
      <VariableDeclaratorSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitVariableDeclarator(node)!
      );

    foreach (var node in
             varDeclSyntax.Initializer.Value.DescendantNodesAndSelf())
    {
      TestUtil.NodeShouldNotBeMutated(node, schemaRegistry);
    }
  }
}