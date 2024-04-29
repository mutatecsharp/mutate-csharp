using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using MutateCSharp.Mutation.Registry;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation;

public class MutantSchemataGeneratorTest(ITestOutputHelper testOutputHelper)
{
  private const string ExampleProgram = 
      """
        using System;

        public class A
        {
          public static void Main()
          {
            bool b = true;
            b &= false;
          }
        }
        """;

  private const string AnotherExampleProgram =
    """
    
    using System;
    
    public class B
    {
      public static void Main()
      {
        bool b = false;
        b &= false;
      }
    }
    """;

  private static string ExtractMutantSchemataClassName(SyntaxTree ast)
  {
    var classDeclarations = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<ClassDeclarationSyntax>()
      .ToList();

    classDeclarations.Should().NotBeEmpty();

    var className = classDeclarations.First().Identifier.ValueText;
    className.Should().NotBeEmpty();

    return className;
  }
  
  private static string ExtractMutantSchemataEnvVarName(SyntaxTree ast)
  {
    var methodInvocations = 
      ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<InvocationExpressionSyntax>()
      .Where(inc => 
        inc.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetEnvironmentVariable" })
      .ToList();

    methodInvocations.Should().NotBeEmpty();

    var lit =
      methodInvocations.First().ArgumentList.Arguments[0].Expression as
        LiteralExpressionSyntax;
    var envVar = lit!.Token.ValueText;

    envVar.Should().NotBeEmpty();
    return envVar;
  }

  [Fact]
  public void DifferentFilesShouldHaveDifferentMutantSchemataClassAndEnvVar()
  {
    var schemaRegistry = new FileLevelMutantSchemaRegistry();
    var group =
      TestUtil
        .GetValidMutationGroup<BooleanConstantReplacer,
          LiteralExpressionSyntax>(ExampleProgram);
    schemaRegistry.RegisterMutationGroupAndGetIdAssignment(group);
    var syntax = MutantSchemataGenerator.GenerateSchemataSyntax(schemaRegistry)!;
    var className = ExtractMutantSchemataClassName(syntax.SyntaxTree);
    var envVar = ExtractMutantSchemataEnvVarName(syntax.SyntaxTree);
    
    var anotherSchemaRegistry = new FileLevelMutantSchemaRegistry();
    var anotherGroup =
      TestUtil
        .GetValidMutationGroup<BooleanConstantReplacer,
          LiteralExpressionSyntax>(AnotherExampleProgram);
    anotherSchemaRegistry.RegisterMutationGroupAndGetIdAssignment(anotherGroup);
    var anotherSyntax = MutantSchemataGenerator.GenerateSchemataSyntax(anotherSchemaRegistry)!;
    var anotherClassName = ExtractMutantSchemataClassName(anotherSyntax.SyntaxTree);
    var anotherEnvVar = ExtractMutantSchemataEnvVarName(anotherSyntax.SyntaxTree);

    className.Should().NotBeEquivalentTo(anotherClassName);
    envVar.Should().NotBeEquivalentTo(anotherEnvVar);
  }

  [Fact]
  public void MutantSchemataClassNameAndMutationRegistryClassNameShouldMatch()
  {
    var schemaRegistry = new FileLevelMutantSchemaRegistry();
    var group =
      TestUtil
        .GetValidMutationGroup<BooleanConstantReplacer,
          LiteralExpressionSyntax>(ExampleProgram);
    schemaRegistry.RegisterMutationGroupAndGetIdAssignment(group);
    var ast = MutantSchemataGenerator.GenerateSchemataSyntax(schemaRegistry)!;
    var className = ExtractMutantSchemataClassName(ast.SyntaxTree);

    className.Should().BeEquivalentTo(schemaRegistry.ClassName);
  }
  
  [Fact]
  public void MutantSchemataEnvVarAndMutationRegistryEnvVarShouldMatch()
  {
    var schemaRegistry = new FileLevelMutantSchemaRegistry();
    var group =
      TestUtil
        .GetValidMutationGroup<BooleanConstantReplacer,
          LiteralExpressionSyntax>(ExampleProgram);
    schemaRegistry.RegisterMutationGroupAndGetIdAssignment(group);
    var ast = MutantSchemataGenerator.GenerateSchemataSyntax(schemaRegistry)!;
    var envVar = ExtractMutantSchemataEnvVarName(ast.SyntaxTree);
    
    var mutationRegistry = schemaRegistry.ToMutationRegistry("some/path");
    envVar.Should().BeEquivalentTo(schemaRegistry.EnvironmentVariable);
    envVar.Should().BeEquivalentTo(mutationRegistry.EnvironmentVariable);
  }
}