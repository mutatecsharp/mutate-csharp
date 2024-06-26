using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Mutator;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Mutator;

public class StringConstantReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetValidMutationGroup(string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<StringConstantReplacer, LiteralExpressionSyntax>(
        inputUnderMutation);

  private static MutationGroup[] GetAllValidMutationGroups(
    string inputUnderMutation)
    => TestUtil
      .GetAllValidMutationGroups<StringConstantReplacer,
        LiteralExpressionSyntax>(inputUnderMutation);

  private static void ShouldNotHaveValidMutationGroup(string inputUnderMutation)
    => TestUtil
      .ShouldNotHaveValidMutationGroup<StringConstantReplacer,
        LiteralExpressionSyntax>(inputUnderMutation);

  [Theory]
  [InlineData("string s = \"abc\";")]
  [InlineData("string s = \"42\";")]
  [InlineData("var s = \"42\";")]
  [InlineData("String a = \"abc\";")]
  public void ShouldReplaceForStringConstants(string constructUnderMutation)
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
    mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo(["string"]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("string");
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(["string.Empty"]);
  }

  [Theory]
  [InlineData("var x = 1 + 2;")]
  [InlineData("var b1 = 1 == 1; var b2 = b1;")]
  [InlineData("bool a = true;")]
  [InlineData("var y = 1 - int.MaxValue;")]
  public void ShouldNotReplaceForNonStringConstants(string constructUnderMutation)
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
  [InlineData("var x = \"abc\" ==\"abc\" ? \"def\" : \"ghi\";", 4)]
  [InlineData("var y = false ? \"42\" : \"24\";", 2)]
  public void ShouldReplaceMultipleStringConstants(
    string constructUnderMutation, int stringCount)
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
    
    mutationGroups.Length.Should().Be(stringCount);

    foreach (var mutationGroup in mutationGroups)
    {
      mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo(["string"]);
      mutationGroup.SchemaReturnType.Should().BeEquivalentTo("string");
      mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
        .BeEquivalentTo("{0}");
      TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
        .BeEquivalentTo(["string.Empty"]);
    }
  }
}