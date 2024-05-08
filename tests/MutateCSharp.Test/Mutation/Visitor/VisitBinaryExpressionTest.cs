using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Registry;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Visitor;

public class VisitBinaryExpressionTest(ITestOutputHelper testOutputHelper)
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
  
  [Fact]
  public void ShouldReplaceForNestedShortCircuitOperatorExpression()
  {
    // Example encountered in the wild.
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          Func<A, bool> lambda = _ => true;
          var b = false;
          var predicate = b || lambda != null;
        }
      }
      """;
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var compilation = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var rewriter = new MutatorAstRewriter(
      compilation.sutAssembly, compilation.model, schemaRegistry);
    var binExpr = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<BinaryExpressionSyntax>().First();

    binExpr.ToString().Should().Be("b || lambda != null");
    var mutatedBinExpr =
      rewriter.VisitBinaryExpression(binExpr).DescendantNodesAndSelf()
        .OfType<InvocationExpressionSyntax>().ToArray();

    // The two (nested) binary expressions should both be mutated
    // || expression is short-circuiting, should wrap with lambda
    var orMutatedExpr = mutatedBinExpr[0];
    var orMutatedArgs =
      TestUtil.GetReplacedNodeArguments(orMutatedExpr, schemaRegistry);
    orMutatedArgs[0].Expression.Should().BeOfType<ParenthesizedLambdaExpressionSyntax>();
    orMutatedArgs[1].Expression.Should().BeOfType<ParenthesizedLambdaExpressionSyntax>();
    
    // != expression is not short-circuiting, should not wrap with lambda
    var notEqMutatedExpr = mutatedBinExpr[1];
    var notEqMutatedArgs =
      TestUtil.GetReplacedNodeArguments(notEqMutatedExpr, schemaRegistry);
    notEqMutatedArgs[0].Expression.Should()
      .NotBeOfType<ParenthesizedLambdaExpressionSyntax>();
    notEqMutatedArgs[1].Expression.Should()
      .NotBeOfType<ParenthesizedLambdaExpressionSyntax>();
  }
}