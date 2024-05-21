using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Mutator;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Mutator.RedundantMutants;

public class PrefixUnaryExprOpReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .UnaryGetValidMutationGroup<PrefixUnaryExprOpReplacer, PrefixUnaryExpressionSyntax>(
        inputUnderMutation, optimise: true);

  private static MutationGroup[] GetAllMutationGroups(string inputUnderMutation)
    => TestUtil
      .UnaryGetAllValidMutationGroups<PrefixUnaryExprOpReplacer,
        PrefixUnaryExpressionSyntax>(inputUnderMutation, optimise: true);
  
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
          var y = {{fromOperator}}x;
        }
      }
      """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);
    
    var templates = mutationGroup.SchemaMutantExpressions.Select(mutant => mutant.ExpressionTemplate);
    testOutputHelper.WriteLine(string.Join(",", templates));

    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate)
      .Should().BeEquivalentTo($"{toOperator}{{0}}", "{0}");
  }
}