using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.OperatorImplementation;

public class BooleanConstantReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetValidMutationGroup(string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<BooleanConstantReplacer, LiteralExpressionSyntax>(
        inputUnderMutation);

  private static MutationGroup[] GetAllValidMutationGroups(
    string inputUnderMutation)
    => TestUtil
      .GetAllValidMutationGroups<BooleanConstantReplacer,
        LiteralExpressionSyntax>(inputUnderMutation);
  
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
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
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

    var mutationGroups = GetAllValidMutationGroups(inputUnderMutation);
    
    mutationGroups.Length.Should().Be(boolCount);

    foreach (var mutationGroup in mutationGroups)
    {
      mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo(["bool"]);
      mutationGroup.SchemaReturnType.Should().BeEquivalentTo("bool");
      mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
        .BeEquivalentTo("{0}");
      TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
        .BeEquivalentTo(["!{0}"]);
    }
  }
}