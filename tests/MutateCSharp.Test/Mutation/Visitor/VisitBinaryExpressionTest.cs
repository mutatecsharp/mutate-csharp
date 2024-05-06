using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;

namespace MutateCSharp.Test.Mutation.Visitor;

public class VisitBinaryExpressionTest
{
  private static IEnumerable<ArgumentSyntax> GetReplacedNodeArguments(
    string inputUnderMutation)
  {
    var schemaRegistry = new FileLevelMutantSchemaRegistry();
    var node = TestUtil.GetNodeUnderMutationAfterRewrite
      <BinaryExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitBinaryExpression(node)
        );
    return TestUtil.GetReplacedNodeArguments(node, schemaRegistry);
  }
  
  [Fact]
  public void ShouldReplaceShortCircuitOperatorsWithLambdaArguments()
  {
    const string inputUnderMutation =
      """
        using System;
        
        public class A
        {
          public static void Main()
          { 
            var collection = new [] { 0 };
            var result = collection is not null && collection[10] > 1;
          }
        }
        """;
    var methodArguments = GetReplacedNodeArguments(inputUnderMutation);
    foreach (var arg in methodArguments)
    {
      arg.Expression.Should().BeOfType<ParenthesizedLambdaExpressionSyntax>();
    }
  }

  [Fact]
  public void ShouldReplaceNonShortCircuitOperatorWithPossiblyMutatedArguments()
  {
    const string inputUnderMutation = 
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var collection = new [] { 0 };
          var result = collection.Length + collection.Length;
        }
      }
      """;
    var methodArguments = GetReplacedNodeArguments(inputUnderMutation);
    foreach (var arg in methodArguments)
    {
      arg.Expression.Should().NotBeOfType<ParenthesizedLambdaExpressionSyntax>();
    }
  }
}