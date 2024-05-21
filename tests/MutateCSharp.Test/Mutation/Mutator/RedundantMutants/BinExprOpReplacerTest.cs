using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Mutator;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Mutator.RedundantMutants;

public class BinExprOpReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .BinaryGetValidMutationGroup<BinExprOpReplacer, BinaryExpressionSyntax>(
        inputUnderMutation, optimise: true);
  
  private static MutationGroup[] GetAllMutationGroups(string inputUnderMutation)
    => TestUtil
      .BinaryGetAllValidMutationGroups<BinExprOpReplacer, BinaryExpressionSyntax>(
        inputUnderMutation, optimise: true);

  private static void ShouldNotHaveValidMutationGroup(string inputUnderMutation)
  {
    TestUtil
      .BinaryShouldNotHaveValidMutationGroup<BinExprOpReplacer,
        BinaryExpressionSyntax>(inputUnderMutation, optimise: true);
  }

  [Fact]
  public void ShouldOmitGeneratingRedundantMutantsForAddition()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = 0 + 0;
        }
      }
      """;
    
    ShouldNotHaveValidMutationGroup(inputUnderMutation);
  }
  
  [Fact]
  public void ShouldOmitGeneratingRedundantMutantsForNonTrivialArithmeticMutants()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = 1 + 0;
          var y = 0 % 1;
          var z = 1 / 1;
        }
      }
      """;

    var mutationGroups = GetAllMutationGroups(inputUnderMutation);

    foreach (var group in mutationGroups)
    {
      var templates = group.SchemaMutantExpressions.Select(mutant => mutant.ExpressionTemplate);
      testOutputHelper.WriteLine(string.Join(",", templates));
    }

    mutationGroups[0].SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate).Should()
      .BeEquivalentTo("{0} | {1}", "{0} & {1}", "{0} ^ {1}");

    mutationGroups[1].SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate).Should()
      .BeEquivalentTo("{0} - {1}");

    mutationGroups[2].SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate).Should()
      .BeEquivalentTo("{0} + {1}", "{0} - {1}", "{0} % {1}", "{0} << {1}", "{0} >> {1}",
        "{0} | {1}", "{0} & {1}", "{0} ^ {1}");
  }

  [Fact]
  public void ShouldOmitGeneratingRedundantMutantsForBooleanOperators()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = true;
          var y = false;
          var z = x && y;
        }
      }
      """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);
    
    var templates = mutationGroup.SchemaMutantExpressions.Select(mutant => mutant.ExpressionTemplate);
    testOutputHelper.WriteLine(string.Join(",", templates));

    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate)
      .Should().BeEquivalentTo("false", "{0}()", "{1}()", "{0}() == {1}()");
  }
  
  [Fact]
  public void ShouldOmitGeneratingRedundantMutantsForRelationalOperators()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = 12;
          var y = 13;
          var z = x > y;
        }
      }
      """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);
    
    var templates = mutationGroup.SchemaMutantExpressions.Select(mutant => mutant.ExpressionTemplate);
    testOutputHelper.WriteLine(string.Join(",", templates));

    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate)
      .Should().BeEquivalentTo("false", "{0} >= {1}", "{0} != {1}");
  }
}