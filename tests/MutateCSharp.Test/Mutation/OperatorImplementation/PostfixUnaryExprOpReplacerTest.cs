using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Mutator;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.OperatorImplementation;

public class PostfixUnaryExprOpReplacerTest
{
  private readonly ITestOutputHelper _testOutputHelper;

  public PostfixUnaryExprOpReplacerTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }

  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .UnaryGetValidMutationGroup<PostfixUnaryExprOpReplacer,
        PostfixUnaryExpressionSyntax>(inputUnderMutation);
  
  [Fact]
  public void
    ShouldReplaceArithmeticBitwiseOperatorsForAssignableSignedIntegralTypes()
  {
    var inputUnderMutation =
      """
        using System;

        public class A
        {
          public static void Main()
          {
            long x = 12;
            var y = x++;
          }
        }
        """;

    _testOutputHelper.WriteLine(inputUnderMutation);
    
    var mutationGroup = GetMutationGroup(inputUnderMutation);
    // Type checks (Should take a reference to the assignable value)
    mutationGroup.SchemaParameterTypes.Should().Equal("ref long");
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}++");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(["{0}--", "+{0}", "-{0}", "~{0}"]);
  }
}