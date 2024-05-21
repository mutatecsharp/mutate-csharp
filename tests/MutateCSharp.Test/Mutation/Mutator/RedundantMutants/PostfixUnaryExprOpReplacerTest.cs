using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Mutator;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Mutator.RedundantMutants;

public class PostfixUnaryExprOpReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .UnaryGetValidMutationGroup<PostfixUnaryExprOpReplacer,
        PostfixUnaryExpressionSyntax>(inputUnderMutation, optimise: true);
  
  [Theory]
  [InlineData("++", "--")]
  [InlineData("--", "++")]
  public void ShouldOmitGeneratingRedundantMutantsForUpdatableArithmeticOperators(
    string fromOperator, string toOperator)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            var x = 13;
            var y = x{{fromOperator}};
          }
        }
        """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);
    
    var templates = mutationGroup.SchemaMutantExpressions.Select(mutant => mutant.ExpressionTemplate);
    testOutputHelper.WriteLine(string.Join(",", templates));

    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate)
      .Should().BeEquivalentTo($"{{0}}{toOperator}", "{0}");
  }
}