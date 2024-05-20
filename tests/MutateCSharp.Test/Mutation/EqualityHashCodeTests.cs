using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using MutateCSharp.Mutation;
using MutateCSharp.Util;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation;

public class EqualityHashCodeTests
{
  private readonly ITestOutputHelper _testOutputHelper;

  public EqualityHashCodeTests(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }

  private MutationGroup CreateExampleMutationGroup()
  {
    return new MutationGroup
    {
      SchemaReturnType = "int?",
      SchemaParameterTypes = ["int?", "int?"],
      SchemaOriginalExpression = 
        new ExpressionRecord(SyntaxKind.SubtractExpression, CodeAnalysisUtil.OperandKind.None, "{0} - {1}"),
      SchemaMutantExpressions = 
        [
          new ExpressionRecord(SyntaxKind.AddExpression, CodeAnalysisUtil.OperandKind.None, "{0} + {1}")
        ],
      ReturnTypeSymbol = default,
      ParameterTypeSymbols = default,
      SchemaName = "ReplaceBinExprOp",
      OriginalLocation = default
    };
  }
  
  [Fact]
  // Refer to FileLevelMutantSchemaRegistry.cs
  public void ShouldFindSameMutationGroupInDictionary()
  {
    var dict = new Dictionary<MutationGroup, int>();

    var group = CreateExampleMutationGroup();
    var groupWithSameContents = CreateExampleMutationGroup();
    
    // Override equality check validation
    group.Equals(groupWithSameContents).Should().BeTrue();
    
    // Override hashcode check validation
    dict[group] = 1;
    dict.ContainsKey(groupWithSameContents).Should().BeTrue();
  }
}