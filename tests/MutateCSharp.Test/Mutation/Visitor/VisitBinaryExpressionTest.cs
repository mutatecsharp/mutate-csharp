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
  public void ShouldReplaceForNestedShortCircuitOperatorExpressionWithNestedFunc()
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

  [Theory]
  [InlineData("await foo(6)", true, "true", false)]
  [InlineData("true", false,"await foo(6)", true)]
  [InlineData("await foo(6)", true, "await foo(6)", true)]
  public void ShouldReplaceForAwaitableOperands(string leftConstruct, bool leftAwaitable, 
    string rightConstruct, bool rightAwaitable)
  {
    var inputUnderMutation =
      $$"""
      using System;

      public class A
      {
        public static async System.Threading.Tasks.Task<bool> foo(int x) => true;
        
        public static async System.Threading.Tasks.Task Main()
        {
          var x = {{leftConstruct}} && {{rightConstruct}};
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();
    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <BinaryExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitBinaryExpression(node)
      );
    mutatedNode.Should().BeOfType<AwaitExpressionSyntax>();
    var awaitExpr = (AwaitExpressionSyntax)mutatedNode;
      
    var binaryExprArguments = TestUtil.GetReplacedNodeArguments(awaitExpr.Expression, schemaRegistry);
    // Left argument should be async () => leftOperand since the mutation group
    // includes short-circuit operators

    var left = (ParenthesizedLambdaExpressionSyntax)binaryExprArguments[0].Expression;
    var right = (ParenthesizedLambdaExpressionSyntax)binaryExprArguments[1].Expression;
    
    testOutputHelper.WriteLine(left.ToFullString());
    testOutputHelper.WriteLine(right.ToFullString());

    left.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword).Should()
      .Be(leftAwaitable);
    right.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword).Should()
      .Be(rightAwaitable);
  }
  
  [Fact]
  public void ShouldNotReplaceNonImmediateChildThatContainsAwaitableOperands()
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static async System.Threading.Tasks.Task<bool> foo(int x) => true;
          public static bool bar(bool x) => true;
          
          public static async System.Threading.Tasks.Task Main()
          {
            var x = bar(await foo(6)) && true;
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();
    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <BinaryExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitBinaryExpression(node)
      );
    mutatedNode.Should().BeOfType<InvocationExpressionSyntax>();
      
    var binaryExprArguments = TestUtil.GetReplacedNodeArguments(mutatedNode, schemaRegistry);
    // Left argument should not have async since the original argument is not await
    var left = (ParenthesizedLambdaExpressionSyntax)binaryExprArguments[0].Expression;
    var right = (ParenthesizedLambdaExpressionSyntax)binaryExprArguments[1].Expression;
    
    testOutputHelper.WriteLine(left.ToFullString());
    testOutputHelper.WriteLine(right.ToFullString());

    left.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword).Should().BeFalse();
    right.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword).Should().BeFalse();
  }
}