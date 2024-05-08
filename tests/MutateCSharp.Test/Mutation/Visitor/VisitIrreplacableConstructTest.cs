using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Util;
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
      testOutputHelper.WriteLine(node.ToFullString());
      TestUtil.NodeShouldNotBeMutated(node, schemaRegistry);
    }
  }
  
  [Theory]
  [InlineData("T")]
  [InlineData("A<T>")]
  [InlineData("List<T>")]
  [InlineData("List<(T, T)>")]
  [InlineData("List<List<(T, T)>>")]
  public void ShouldNotReplaceNodeContainingTypeParameters(string typeConstruct)
  {
    var inputUnderMutation =
      $$"""
      using System;
      using System.Collections.Generic;
      
      public class A<T> where T: class, new()
      {
        static {{typeConstruct}} foo()
        {
          var result = new {{typeConstruct}}();
          return result;
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
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var node = (ReturnStatementSyntax) TestUtil.GetNodeUnderMutationAfterRewrite
      <ReturnStatementSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitReturnStatement(node)
      );
    
    TestUtil.NodeShouldNotBeMutated(node.Expression, schemaRegistry);
  }
}