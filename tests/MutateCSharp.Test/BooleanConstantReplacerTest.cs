using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using Xunit.Abstractions;

namespace MutateCSharp.Test;

public class BooleanConstantReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetValidMutationGroup(string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<BooleanConstantReplacer, LiteralExpressionSyntax>(
        inputUnderMutation);

  private static void ShouldNotHaveValidMutationGroup(string inputUnderMutation)
    => TestUtil
      .ShouldNotHaveValidMutationGroup<BooleanConstantReplacer,
        LiteralExpressionSyntax>(inputUnderMutation);

  [Theory]
  [InlineData("bool b = true;")]
  [InlineData("bool b = false;")]
  [InlineData("Boolean b = true;")]
  [InlineData("Boolean b = false;")]
  [InlineData("var b = true;")]
  [InlineData("var b = false;")]
  public void ShouldReplaceForBooleanConstants(string constructUnderMutation)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{constructUnderMutation}}
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetValidMutationGroup(inputUnderMutation);
    mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo(["bool"]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("bool");
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo(["!{0}"]);
  }

  [Theory]
  [InlineData("var x = 1 + 2;")]
  [InlineData("var b1 = 1 == 1; var b2 = b1;")]
  public void ShouldNotReplaceForNonBooleanConstants(string constructUnderMutation)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{constructUnderMutation}}
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    ShouldNotHaveValidMutationGroup(inputUnderMutation);
  }

  [Theory]
  [InlineData("var x = true ? false : true;", 3)]
  [InlineData("var y = false ? 2 : 1;", 1)]
  public void ShouldReplaceMultipleBooleanConstants(
    string constructUnderMutation, int boolCount)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{constructUnderMutation}}
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);
    
    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    TestUtil.TestForSyntacticErrors(inputAst);

    var compilation = TestUtil.GetAstCompilation(inputAst);
    var model = compilation.GetSemanticModel(inputAst);
    model.Should().NotBeNull();
    TestUtil.TestForSemanticErrors(model);

    var mutationOperator = new BooleanConstantReplacer(model);
    var constructsUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<LiteralExpressionSyntax>().ToList();
    var mutationGroups = constructsUnderTest
      .Select(mutationOperator.CreateMutationGroup)
      .Where(group => group != null)
      .ToList();
    
    mutationGroups.Count.Should().Be(boolCount);

    foreach (var mutationGroup in mutationGroups)
    {
      mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo(["bool"]);
      mutationGroup.SchemaReturnType.Should().BeEquivalentTo("bool");
      mutationGroup.SchemaOriginalExpressionTemplate.Should()
        .BeEquivalentTo("{0}");
      mutationGroup.SchemaMutantExpressionsTemplate.Should()
        .BeEquivalentTo(["!{0}"]);
    }
  }
}