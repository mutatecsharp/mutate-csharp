using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Visitor;

public class VisitControlFlowSyntaxTest(ITestOutputHelper testOutputHelper)
{
  [Theory]
  [InlineData("""
              switch (x)
              {
                case 1:
                  x = 2;
                  break;
              }
              """)]
  [InlineData("""
              switch (x)
              {
                case 1:
                  while (true) { x = 2; }
              }
              """)]
  public void ShouldInsertBreakStatementsAfterEachSwitchSection(string construct)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            var x = 1;
            {{construct}}
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var node = TestUtil.GetNodeUnderMutationAfterRewrite
      <SwitchSectionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitSwitchSection(node)
      );

    node.Should().BeOfType<SwitchSectionSyntax>();
    var switchSection = (SwitchSectionSyntax)node;
    switchSection.Statements.Last().Should().BeOfType<BreakStatementSyntax>();
  }
  
  [Theory]
  [InlineData("""
              switch (x)
              {
                case 1:
                case 2:
                  x = 2;
                  break;
              }
              """)]
  [InlineData("""
              switch (x)
              {
                case 1:
                case 2:
                  while (true) { x = 2; }
              }
              """)]
  public void ShouldNotInsertBreakStatementsInBetweenCasesInSwitchSections(string construct)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            var x = 1;
            {{construct}}
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <SwitchSectionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitSwitchSection(node)
      );

    mutatedNode.Should().BeOfType<SwitchSectionSyntax>();
    var count = mutatedNode.DescendantNodesAndSelf().OfType<SwitchSectionSyntax>().Count();
    count.Should().Be(1);

    var switchSection = (SwitchSectionSyntax)mutatedNode;
    switchSection.Statements.Last().Should().BeOfType<BreakStatementSyntax>();
  }

  [Theory]
  [InlineData("int", "while(true) { return 1; }", true)]
  [InlineData("int", "return 1;", false)]
  [InlineData("void", "", false)]
  public void ShouldInsertReturnDefaultStatementForMethodDeclarations(
    string returnType, string construct, bool shouldReplace)
  {
    var inputUnderMutation =
      $$"""
      using System;

      public class A
      {
        public static {{returnType}} foo()
        {
          {{construct}}
        }
      
        public static void Main() {}
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <MethodDeclarationSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitMethodDeclaration(node)
      );
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());

    var methodDecl = (MethodDeclarationSyntax)mutatedNode;
    var lastStat = methodDecl.Body.Statements.LastOrDefault();
    
    if (shouldReplace)
    {
      lastStat.ToFullString()
        .Contains("default", StringComparison.OrdinalIgnoreCase)
        .Should().BeTrue();
    }
    else
    {
      methodDecl.Body.Statements.Where(node =>
          node.ToFullString()
            .Contains("default", StringComparison.OrdinalIgnoreCase))
        .Should().BeEmpty();
    }
  }
  
  [Theory]
  [InlineData("() => { foo(); }", false)]
  [InlineData("() => { return 1; }", false)]
  [InlineData("() => { while (true) { return 1; } }", true)]
  public void ShouldInsertReturnDefaultStatementForParenthesizedLambdaExpressions(
    string construct, bool shouldReplace)
  {
    var inputUnderMutation =
      $$"""
      using System;

      public class A
      {
        public static void foo() {}
      
        public static void Main() 
        {
          var x = {{construct}};
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <ParenthesizedLambdaExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitParenthesizedLambdaExpression(node)
      );
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());

    var methodDecl = (ParenthesizedLambdaExpressionSyntax)mutatedNode;
    var lastStat = methodDecl.Block.Statements.LastOrDefault();

    if (shouldReplace)
    {
      lastStat.ToFullString()
        .Contains("default", StringComparison.OrdinalIgnoreCase)
        .Should().BeTrue();
    }
    else
    {
      methodDecl.Block.Statements.Where(node =>
          node.ToFullString()
            .Contains("default", StringComparison.OrdinalIgnoreCase))
        .Should().BeEmpty();
    }
  }
  
  [Theory]
  [InlineData("Action<int> x = z => {}", false)]
  [InlineData("Func<int, int> x = z => {return z;}", false)]
  [InlineData("Func<int, int> x = z => { while (true) { return 1; } }", true)]
  public void ShouldInsertReturnDefaultStatementForSimpleLambdaExpressions(
    string construct, bool shouldReplace)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{construct}};
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = new FileLevelMutantSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <SimpleLambdaExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitSimpleLambdaExpression(node)
      );
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());

    var methodDecl = (SimpleLambdaExpressionSyntax)mutatedNode;
    var lastStat = methodDecl.Block.Statements.LastOrDefault();

    if (shouldReplace)
    {
      lastStat.ToFullString()
        .Contains("default", StringComparison.OrdinalIgnoreCase)
        .Should().BeTrue();
    }
    else
    {
      // Check default literal does not exist
      methodDecl.Block.Statements.Where(node =>
          node.ToFullString()
            .Contains("default", StringComparison.OrdinalIgnoreCase))
        .Should().BeEmpty();
    }
  }
}