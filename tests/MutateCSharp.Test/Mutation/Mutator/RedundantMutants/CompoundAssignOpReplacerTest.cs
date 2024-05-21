using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Mutator;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Mutator.RedundantMutants;

public class CompoundAssignOpReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .BinaryGetValidMutationGroup<CompoundAssignOpReplacer,
        AssignmentExpressionSyntax>(inputUnderMutation, optimise: true);
  
  private static void ShouldNotHaveValidMutationGroup(string inputUnderMutation)
  {
    TestUtil.BinaryShouldNotHaveValidMutationGroup<CompoundAssignOpReplacer, AssignmentExpressionSyntax>(inputUnderMutation, optimise: true);
  }

  [Fact]
  public void ShouldOmitGeneratingRedundantMutantsForArithmeticOperators()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = 12;
          x += 0;
        }
      }
      """;
    
    var mutationGroup = GetMutationGroup(inputUnderMutation);
    
    var templates = 
      mutationGroup.SchemaMutantExpressions
        .Select(mutant => mutant.ExpressionTemplate);
    
    testOutputHelper.WriteLine(string.Join(",", templates));

    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate)
      .Should().BeEquivalentTo("{0} &= {1}", "{0} ^= {1}", "{0} |= {1}");
  }
}