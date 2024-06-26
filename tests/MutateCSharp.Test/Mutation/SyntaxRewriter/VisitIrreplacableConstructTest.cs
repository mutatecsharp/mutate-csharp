using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Mutation.SyntaxRewriter;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.SyntaxRewriter;

public class VisitIrreplacableConstructTest(ITestOutputHelper testOutputHelper)
{
  private static FileLevelMutantSchemaRegistry CreateSchemaRegistry()
    => new FileLevelMutantSchemaRegistry();
  
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
    var schemaRegistry = CreateSchemaRegistry();

    var node = TestUtil.GetNodeUnderMutationAfterRewrite
      <ArrayCreationExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitArrayCreationExpression(node),
        SyntaxRewriterMode.Mutate
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
  [InlineData("int.MaxValue is var n")]
  [InlineData("int.MaxValue is int n")]
  [InlineData("bar(ob, out var b)")]
  [InlineData("!(!!(ob is string s) && !(ob is bool b))")]
  [InlineData("!(ob is bool b)")]
  [InlineData("!!!(ob is string s)")]
  [InlineData("foo(ob is bool b)")]
  [InlineData("!foo(ob is var b)")]
  [InlineData("choc(out ob)")]
  public void ShouldNotReplaceNodeContainingDeclarationPatternSyntaxAsDescendant(
    string construct)
  {
    var inputUnderMutation =
      $$"""
      using System;
      
      public class A
      {
        public static bool foo(object b) => true;
        public static bool bar(object b, out bool x) => x = true;
        public static int choc(out object b)
        {
          b = 10;
          return 1;
        }
      
        public static void choc(object ob)
        {
          var y = {{construct}};
        }
      
        public static void Main()
        {
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);

    var schemaRegistry = CreateSchemaRegistry();
    
    var varDeclSyntax = (VariableDeclaratorSyntax) TestUtil.GetNodeUnderMutationAfterRewrite
      <VariableDeclaratorSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitVariableDeclarator(node)!,
        SyntaxRewriterMode.Mutate
      );

    foreach (var node in
             varDeclSyntax.Initializer.Value.DescendantNodesAndSelf())
    {
      testOutputHelper.WriteLine(node.ToFullString());
      TestUtil.NodeShouldNotBeMutated(node, schemaRegistry);
    }
  }
  
  [Theory]
  [InlineData("T")]
  [InlineData("A<T>")]
  [InlineData("A<A<T>>")]
  [InlineData("A<(T, T)>")]
  [InlineData("A<A<(T, T)>>")]
  public void ShouldNotReplaceNodeContainingTypeParameters(string typeConstruct)
  {
    var inputUnderMutation =
      $$"""
      using System;
      using System.Collections.Generic;
      
      public class A<T> where T: new()
      {
        static {{typeConstruct}} foo()
        {
          var result = new {{typeConstruct}}();
          var validate = result != null;
          {{( typeConstruct is "T" ? string.Empty : "var prefix = !result;")}}
          return result;
        }
        
        public static bool operator!(A<T> a) => false;
      }
      
      public class B
      {
        public static void Main() 
        {
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);

    var schemaRegistry = CreateSchemaRegistry();
    
    var mutatedBinaryNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <BinaryExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitBinaryExpression(node),
        SyntaxRewriterMode.Mutate
      );
    testOutputHelper.WriteLine(mutatedBinaryNode.ToFullString());
    mutatedBinaryNode.Should().BeOfType<BinaryExpressionSyntax>();

    if (typeConstruct is not "T")
    {
      var mutatedUnaryNode = TestUtil.GetNodeUnderMutationAfterRewrite
        <PrefixUnaryExpressionSyntax>(
          inputUnderMutation, 
          schemaRegistry,
          (rewriter, node) => rewriter.VisitPrefixUnaryExpression(node),
          SyntaxRewriterMode.Mutate
        );
      testOutputHelper.WriteLine(mutatedUnaryNode.ToFullString());
      mutatedUnaryNode.Should().BeOfType<PrefixUnaryExpressionSyntax>();
    }

    var mutatedReturnStatementNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <ReturnStatementSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitReturnStatement(node),
        SyntaxRewriterMode.Mutate
      );
    mutatedReturnStatementNode.Should().BeOfType<ReturnStatementSyntax>();
    var returnStat = (ReturnStatementSyntax)mutatedReturnStatementNode;
    
    TestUtil.NodeShouldNotBeMutated(returnStat.Expression, schemaRegistry);
  }

  [Theory]
  [InlineData("b1 && b2")]
  [InlineData("b1 || b1")]
  [InlineData("b2 && b1")]
  public void
    ShouldNotReplaceIfRefParametersAreOperandsForBinaryShortCircuitingOperators(
      string construct)
  {
    var inputUnderMutation =
      $$"""
      using System;

      public class A
      {
        static bool foo(ref bool b1)
        {
          bool b2 = false;
          return {{construct}};
        }
        
        public static void Main()
        {
        }
      }
      """;

    var schemaRegistry = CreateSchemaRegistry();
    
    var mutatedReturnStatementNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <ReturnStatementSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitReturnStatement(node),
        SyntaxRewriterMode.Mutate
      );
    mutatedReturnStatementNode.Should().BeOfType<ReturnStatementSyntax>();
    var returnStat = (ReturnStatementSyntax)mutatedReturnStatementNode;
    
    TestUtil.NodeShouldNotBeMutated(returnStat.Expression, schemaRegistry);
  }

  [Fact]
  public void ShouldNotReplaceNumericLiteralIfItIsAssignedToEnum()
  {
    const string inputUnderMutation =
      """
      using System;

      public enum Enum
      {
        First,
        Second,
        Third
      }

      public class A
      {
        public static void Main()
        {
          Enum x = 0;
        }
      }
      """;

    var schemaRegistry = CreateSchemaRegistry();
    
    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <LiteralExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitLiteralExpression(node),
        SyntaxRewriterMode.Mutate
      );
    mutatedNode.Should().BeOfType<LiteralExpressionSyntax>();
    var literalSyntax = (LiteralExpressionSyntax)mutatedNode;
    
    TestUtil.NodeShouldNotBeMutated(literalSyntax, schemaRegistry);
  }
  
  [Fact]
  public void ShouldNotReplaceNodeWithTypeParameterAlsoDefinedAsAClass()
  {
    var inputUnderMutation =
      """
      using System;
      using System.Collections.Generic;
      
      public class T {}
      
      public class A<T> where T: class
      {
        static A<T> foo()
        {
          var result = new A<T>();
          var validate = result != null;
          var prefix = !result;
          return result;
        }
        
        public static bool operator!(A<T> a) => false;
        
        public class U
        { 
          static bool foo()
          { 
            var result = new A<T>();
            var validate = result != null;
            var prefix = !result;
            return true;
          }
        }
      }
      
      public class B
      {
        public static void Main() 
        {
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);

    var schemaRegistry = CreateSchemaRegistry();
    
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var compilation = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var rewriter = new MutatorAstRewriter(
      compilation.sutAssembly, compilation.model, schemaRegistry, SyntaxRewriterMode.Mutate, optimise: false);
    var binExprs = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<BinaryExpressionSyntax>();

    foreach (var binExpr in binExprs)
    {
      var mutatedBinNode = rewriter.VisitBinaryExpression(binExpr);
      testOutputHelper.WriteLine(mutatedBinNode.ToFullString());
      mutatedBinNode.Should().BeOfType<BinaryExpressionSyntax>();
    }
    
    var unaryExprs = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<PrefixUnaryExpressionSyntax>();

    foreach (var unaryExpr in unaryExprs)
    {
      var mutatedUnaryNode = rewriter.VisitPrefixUnaryExpression(unaryExpr);
      testOutputHelper.WriteLine(mutatedUnaryNode.ToFullString());
      mutatedUnaryNode.Should().BeOfType<PrefixUnaryExpressionSyntax>();
    }
  }
}