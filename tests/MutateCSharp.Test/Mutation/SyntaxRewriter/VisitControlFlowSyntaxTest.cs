using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Mutation.SyntaxRewriter;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.SyntaxRewriter;

public class VisitControlFlowSyntaxTest(ITestOutputHelper testOutputHelper)
{
  private static FileLevelMutantSchemaRegistry CreateSchemaRegistry()
    => new FileLevelMutantSchemaRegistry();
  
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
    
    var schemaRegistry = CreateSchemaRegistry();

    var node = TestUtil.GetNodeUnderMutationAfterRewrite
      <SwitchSectionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitSwitchSection(node),
        SyntaxRewriterMode.Mutate
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
    
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <SwitchSectionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitSwitchSection(node),
        SyntaxRewriterMode.Mutate
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
  [InlineData("async System.Threading.Tasks.Task", "", false)]
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
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <MethodDeclarationSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitMethodDeclaration(node),
        SyntaxRewriterMode.Mutate
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
  [InlineData("string", "while (true) { return string.Empty; }", false)]
  [InlineData("System.Collections.Generic.IEnumerable<int>", "yield return 1;", false)]
  [InlineData("System.Collections.Generic.IEnumerable<int>", " while (true) { yield return 1; } ", true)]
  public void ShouldInsertYieldBreakStatementForMethodDeclarations(
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
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <MethodDeclarationSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitMethodDeclaration(node),
        SyntaxRewriterMode.Mutate
      );
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());

    var methodDecl = (MethodDeclarationSyntax)mutatedNode;
    var lastStat = methodDecl.Body.Statements.LastOrDefault();
    
    if (shouldReplace)
    {
      lastStat.ToFullString()
        .Contains("break", StringComparison.OrdinalIgnoreCase)
        .Should().BeTrue();
    }
    else
    {
      methodDecl.Body.Statements.Where(node =>
          node.ToFullString()
            .Contains("break", StringComparison.OrdinalIgnoreCase))
        .Should().BeEmpty();
    }
  }
  
  [Theory]
  [InlineData("int", "while(true) { return 1; }", true)]
  [InlineData("int", "return 1;", false)]
  [InlineData("void", "", false)]
  [InlineData("async System.Threading.Tasks.Task", "", false)]
  public void ShouldInsertReturnDefaultStatementForLocalFunctionStatements(
    string returnType, string construct, bool shouldReplace)
  {
    var inputUnderMutation =
      $$"""
      using System;

      public class A
      {
        public void foo()
        {
          bar();
          
          {{returnType}} bar()
          {
            {{construct}}
          }
        }
      
        public static void Main() {}
      }
      """;

    testOutputHelper.WriteLine(inputUnderMutation);
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <LocalFunctionStatementSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitLocalFunctionStatement(node),
        SyntaxRewriterMode.Mutate
      );
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());

    var methodDecl = (LocalFunctionStatementSyntax)mutatedNode;
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
  [InlineData("string", "while (true) { return string.Empty; }", false)]
  [InlineData("System.Collections.Generic.IEnumerable<int>", "yield return 1;", false)]
  [InlineData("System.Collections.Generic.IEnumerable<int>", " while (true) { yield return 1; } ", true)]
  public void ShouldInsertYieldBreakStatementForLocalFunctionStatements(
    string returnType, string construct, bool shouldReplace)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void barbar()
          { 
            var x = foo();
          
            {{returnType}} foo()
            {
              {{construct}}
            }
          }
        
          public static void Main() {}
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <LocalFunctionStatementSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitLocalFunctionStatement(node),
        SyntaxRewriterMode.Mutate
      );
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());

    var methodDecl = (LocalFunctionStatementSyntax)mutatedNode;
    var lastStat = methodDecl.Body.Statements.LastOrDefault();
    
    if (shouldReplace)
    {
      lastStat.ToFullString()
        .Contains("break", StringComparison.OrdinalIgnoreCase)
        .Should().BeTrue();
    }
    else
    {
      methodDecl.Body.Statements.Where(node =>
          node.ToFullString()
            .Contains("break", StringComparison.OrdinalIgnoreCase))
        .Should().BeEmpty();
    }
  }
  
  [Theory]
  [InlineData("() => { foo(); }", false)]
  [InlineData("() => { return 1; }", false)]
  [InlineData("async () => { foo(); }", false)]
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
    
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <ParenthesizedLambdaExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitParenthesizedLambdaExpression(node),
        SyntaxRewriterMode.Mutate
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
    
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = TestUtil.GetNodeUnderMutationAfterRewrite
      <SimpleLambdaExpressionSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitSimpleLambdaExpression(node),
        SyntaxRewriterMode.Mutate
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
  
  [Theory]
  [InlineData("int", "init { Foo = 2; }", false)]
  [InlineData("int","set {}", false)]
  [InlineData("int","set; get;", false)]
  [InlineData("int","get { return 1; }", false)]
  [InlineData("int","set { Foo = 2; } get { return 1; }", false)]
  [InlineData("int","get { while (true) { return 1; } }", true)]
  [InlineData("int","get { while (true) { return 1; } } set { Foo = 2; }", true)]
  [InlineData("int","init {} get { while (true) { return 1; } }", true)]
  public void ShouldInsertReturnDefaultStatementForPropertyGetAccessors(
    string returnType, string construct, bool shouldReplace)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public {{returnType}} Foo
          {
            {{construct}}
          }
        
          public static void Main() {}
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = (PropertyDeclarationSyntax) TestUtil.GetNodeUnderMutationAfterRewrite
      <PropertyDeclarationSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitPropertyDeclaration(node),
        SyntaxRewriterMode.Mutate
      );
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());

    if (shouldReplace)
    {
      var getter = mutatedNode.AccessorList.Accessors
        .First(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
      getter.Body.Statements.Last().ToFullString().Contains("default").Should()
        .BeTrue();
    }
    else
    {
      // Check default literal does not exist
      mutatedNode.AccessorList.Accessors.All(node =>
          !node.ToFullString()
            .Contains("default", StringComparison.OrdinalIgnoreCase))
        .Should().BeTrue();
    }
  }
  
  [Theory]
  [InlineData("string", "get { while (true) { return string.Empty; } }", false)]
  [InlineData("System.Collections.Generic.IEnumerable<int>", "set {} get { yield return 1; }", false)]
  [InlineData("System.Collections.Generic.IEnumerable<int>", "set {} get { return System.Linq.Enumerable.Range(1, 10); }", false)]
  [InlineData("System.Collections.Generic.IEnumerable<int>", "get { while (true) { yield return 1; } }", true)]
  public void ShouldInsertYieldReturnStatementForPropertyGetAccessors(
    string returnType, string construct, bool shouldReplace)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public {{returnType}} Foo
          {
            {{construct}}
          }
        
          public static void Main() {}
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = (PropertyDeclarationSyntax) TestUtil.GetNodeUnderMutationAfterRewrite
      <PropertyDeclarationSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitPropertyDeclaration(node),
        SyntaxRewriterMode.Mutate
      );
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());

    if (shouldReplace)
    {
      var getter = mutatedNode.AccessorList.Accessors
        .First(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
      getter.Body.Statements.Last().ToFullString().Contains("break").Should()
        .BeTrue();
    }
    else
    {
      // Check default literal does not exist
      mutatedNode.AccessorList.Accessors.All(node =>
          !node.ToFullString()
            .Contains("break", StringComparison.OrdinalIgnoreCase))
        .Should().BeTrue();
    }
  }

  [Fact]
  public void ShouldInsertNegationOperatorForWhileAndIfStatements()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = false;
          
          while (x)
          {
            x = false;
          }
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = (WhileStatementSyntax) TestUtil.GetNodeUnderMutationAfterRewrite
      <WhileStatementSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitWhileStatement(node),
        SyntaxRewriterMode.Mutate
      );

    mutatedNode.Condition.Should().BeOfType<InvocationExpressionSyntax>();
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());
  }
  
  [Fact]
  public void ShouldInsertNegationOperatorForWhileStatements()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = false;
          
          while (x)
          {
            x = false;
          }
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = (WhileStatementSyntax) TestUtil.GetNodeUnderMutationAfterRewrite
      <WhileStatementSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitWhileStatement(node),
        SyntaxRewriterMode.Mutate
      );

    mutatedNode.Condition.Should().BeOfType<InvocationExpressionSyntax>();
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());
  }
  
  [Fact]
  public void ShouldInsertNegationOperatorForIfStatements()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = false;
          
          if (x)
          {
            x = false;
          }
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var schemaRegistry = CreateSchemaRegistry();

    var mutatedNode = (IfStatementSyntax) TestUtil.GetNodeUnderMutationAfterRewrite
      <IfStatementSyntax>(
        inputUnderMutation,
        schemaRegistry,
        (rewriter, node) => rewriter.VisitIfStatement(node),
        SyntaxRewriterMode.Mutate
      );

    mutatedNode.Condition.Should().BeOfType<InvocationExpressionSyntax>();
    
    testOutputHelper.WriteLine(mutatedNode.ToFullString());
  }
}