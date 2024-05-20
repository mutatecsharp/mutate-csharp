using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;

namespace MutateCSharp.Test.Mutation.SyntaxRewriter;

public class VisitNumericLiteralTest
{
  private static FileLevelMutantSchemaRegistry CreateSchemaRegistry()
    => new FileLevelMutantSchemaRegistry();
  
  private static IEnumerable<ArgumentSyntax> GetLiteralReplacedNodeArguments(
    string inputUnderMutation)
  {
    var schemaRegistry = CreateSchemaRegistry();
    var node = TestUtil.GetNodeUnderMutationAfterRewrite
      <LiteralExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitLiteralExpression(node)
      );
    return TestUtil.GetReplacedNodeArguments(node, schemaRegistry);
  }
  
  private static IEnumerable<ArgumentSyntax> GetNegativeLiteralReplacedNodeArguments(
    string inputUnderMutation)
  {
    var schemaRegistry = CreateSchemaRegistry();
    var node = TestUtil.GetNodeUnderMutationAfterRewrite
      <PrefixUnaryExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitPrefixUnaryExpression(node)
      );
    return TestUtil.GetReplacedNodeArguments(node, schemaRegistry);
  }

  /*
   * Literals are interpreted as positive values in C#. A syntax visitor
   * would treat -1 as unary - followed by numeric literal 1, which should
   * not be visited separately if it is a known compile-time constant.
   */
  [Theory]
  [InlineData("-1")]
  [InlineData("-1234567")]
  [InlineData("-123123213")]
  [InlineData("-2147483648")]
  [InlineData("-2147483649")]
  [InlineData("-214748364923232")]
  [InlineData("-9223372036854775808")]
  [InlineData("-1L")]
  [InlineData("-1234567L")]
  [InlineData("-2147483648L")]
  [InlineData("-9223372036854775808L")]
  public void ShouldReplaceNegativeIntegerLiterals(string number)
  {
    var inputUnderMutation =
      $$"""
        using System;

        class A
        {
          public static void Main()
          {
            var x = {{number}};
          }
        }
        """;

    var methodArguments = GetNegativeLiteralReplacedNodeArguments(inputUnderMutation).ToList();
    methodArguments.Should().HaveCount(1);
    methodArguments[0].ToString().Should().Be(number);
  }
}