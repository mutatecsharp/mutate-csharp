using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
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
    var group =
      TestUtil
        .GetValidMutationGroup<BooleanConstantReplacer,
          LiteralExpressionSyntax>(ExampleProgram);
    var schemata = MutantSchemataGenerator.GenerateSchemata([group]);
    var ast = CSharpSyntaxTree.ParseText(schemata.ToString());
    var className = ExtractMutantSchemataClassName(ast);
    var envVar = ExtractMutantSchemataEnvVarName(ast);
    
    var anotherGroup =
      TestUtil
        .GetValidMutationGroup<BooleanConstantReplacer,
          LiteralExpressionSyntax>(AnotherExampleProgram);
    var anotherSchemata =
      MutantSchemataGenerator.GenerateSchemata([anotherGroup]);
    var anotherAst = CSharpSyntaxTree.ParseText(anotherSchemata.ToString());
    var anotherClassName = ExtractMutantSchemataClassName(anotherAst);
    var anotherEnvVar = ExtractMutantSchemataEnvVarName(anotherAst);

    className.Should().NotBeEquivalentTo(anotherClassName);
    envVar.Should().NotBeEquivalentTo(anotherEnvVar);
  }
}