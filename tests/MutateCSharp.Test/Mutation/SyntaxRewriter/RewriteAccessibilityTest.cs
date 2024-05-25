using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Mutation.SyntaxRewriter;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.SyntaxRewriter;

public class RewriteAccessibilityTest
{
  private readonly ITestOutputHelper _testOutputHelper;

  public RewriteAccessibilityTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }

  private static FileLevelMutantSchemaRegistry CreateSchemaRegistry()
    => new FileLevelMutantSchemaRegistry();
  
  [Theory]
  [InlineData("private")]
  [InlineData("protected")]
  [InlineData("internal")]
  [InlineData("protected internal")]
  [InlineData("private protected")]
  public void ShouldIncreaseAccessibilityToPublic(string visibility)
  {
    var inputUnderMutation =
      $$"""
      using System;
      
      file class C;

      class A 
      {
        {{visibility}} class B
        {
          public static void Main() {}
        }
      }
      """;
    
    // Case 1: Access modifier rewriter
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var accessRewriter = new AccessModifierRewriter();
    var accessRewrittenNode = accessRewriter.Visit(ast.GetCompilationUnitRoot());
    
    _testOutputHelper.WriteLine(accessRewrittenNode.ToString());

    var accessClassDecl = accessRewrittenNode.DescendantNodesAndSelf()
      .OfType<ClassDeclarationSyntax>();

    foreach (var classDecl in accessClassDecl)
    {
      classDecl.Modifiers
        .Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)
                         && modifier.Kind() is not 
                           (SyntaxKind.PrivateKeyword or 
                           SyntaxKind.ProtectedKeyword or 
                           SyntaxKind.InternalKeyword or 
                           SyntaxKind.FileKeyword))
        .Should().BeTrue();
    }
    
    // Case 2: Mutator modifier rewriter
    var schemaRegistry = CreateSchemaRegistry();
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var mutationRewriter =
      new MutatorAstRewriter(comp.sutAssembly, comp.model, schemaRegistry, SyntaxRewriterMode.Mutate, optimise: false);
    var mutationRewrittenNode = mutationRewriter.Visit(ast.GetCompilationUnitRoot());
    
    _testOutputHelper.WriteLine(mutationRewrittenNode.ToString());

    var mutationClassDecl = mutationRewrittenNode.DescendantNodesAndSelf()
      .OfType<ClassDeclarationSyntax>();
    
    foreach (var classDecl in mutationClassDecl)
    {
      classDecl.Modifiers
        .Any(modifier => modifier.IsKind(SyntaxKind.PublicKeyword)
                         && modifier.Kind() is not 
                           (SyntaxKind.PrivateKeyword or 
                           SyntaxKind.ProtectedKeyword or 
                           SyntaxKind.InternalKeyword or 
                           SyntaxKind.FileKeyword))
        .Should().BeTrue();
    }
  }
}