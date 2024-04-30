using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;

namespace MutateCSharp.Test.Mutation.Visitor;

public class VisitBinaryExpressionTest
{
  private static SyntaxNode VisitSyntaxUnderTest(MutatorAstRewriter rewriter, BinaryExpressionSyntax syntax)
  {
    return rewriter.VisitBinaryExpression(syntax);
  }

  private static IList<ArgumentSyntax> GetReplacedNodeArguments(
    string inputUnderMutation)
  {
    return TestUtil.GetReplacedNodeArguments<BinaryExpressionSyntax>(
      inputUnderMutation, VisitSyntaxUnderTest);
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