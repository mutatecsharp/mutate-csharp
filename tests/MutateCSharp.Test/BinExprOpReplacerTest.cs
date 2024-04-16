using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using Xunit.Abstractions;

namespace MutateCSharp.Test;

public class BinExprOpReplacerTest
{
  private readonly ITestOutputHelper _testOutputHelper;

  public BinExprOpReplacerTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }

  private MutationGroup? GetMutationGroup(string inputUnderMutation)
  {
    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    TestUtil.TestForSyntacticErrors(inputAst);

    var compilation = TestUtil.GetAstCompilation(inputAst);
    var model = compilation.GetSemanticModel(inputAst);
    var mutationOperator = new BinExprOpReplacer(model);
    var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<BinaryExpressionSyntax>().FirstOrDefault();

    constructUnderTest.Should().NotBeNull();
    
    _testOutputHelper.WriteLine(constructUnderTest.Kind().ToString());

    return mutationOperator.CreateMutationGroup(constructUnderTest);
  }

  public static IEnumerable<object[]> BooleanMutations =
    TestUtil.GenerateMutationTestCases(["&", "&&", "|", "||", "^", "==", "!="]);

  [Theory]
  [MemberData(nameof(BooleanMutations))]
  public void ShouldReplaceBitwiseLogicalAndEqualityOperatorsForBooleanExpressions(string originalOperator, string[] expectedReplacementOperators)
  {
    var inputUnderMutation = $"bool b = true; b = b {originalOperator} false;";
    var mutationGroup = GetMutationGroup(inputUnderMutation);
    
    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo(["bool", "bool"]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("bool");

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{{0}} {originalOperator} {{1}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Count.Should()
      .Be(expectedReplacementOperators.Length);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{{0}} {op} {{1}}")
        .ToHashSet();

    mutationGroup.SchemaMutantExpressionsTemplate.ToHashSet()
      .SetEquals(validMutantExpressionsTemplate).Should().BeTrue();
  }
  
  private static IDictionary<string, string> IntegralTypes = new Dictionary<string, string>
  {
    {"char", "'a'"}, 
    {"sbyte", "((sbyte) 1)"}, 
    {"int", "1"}, 
    {"long", "42l"}, 
    {"byte", "((byte) 0b11)"}, 
    {"ushort", "((ushort) 11)"}, 
    {"uint", "((uint) 10)"}, 
    {"ulong", "11ul"}
  };

  private static ISet<string> SupportedIntegralOperators =
    new HashSet<string> { "+", "-", "*", "/", "%", ">>", "<<", ">>>", "^", "&", "|" };

  public static IEnumerable<object[]> IntegralTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(IntegralTypes.Keys,
      SupportedIntegralOperators);

  [Theory]
  [MemberData(nameof(IntegralTypedMutations))]
  public void
    ShouldReplaceArithmeticBitwiseOperatorsForIntegralTypesAndReturnIntegralType(
      string integralType, string originalOperator,
      string[] expectedReplacementOperators)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{integralType}} x = {{IntegralTypes[integralType]}};
            {{integralType}} y = x {{originalOperator}} {{IntegralTypes[integralType]}};
          }
        }
        """;
    
    _testOutputHelper.WriteLine(inputUnderMutation);
    
    var mutationGroup = GetMutationGroup(inputUnderMutation);
    
    // Sanity check
    mutationGroup.Should().NotBeNull();

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo([integralType, integralType]);
    
    // Note: we avoid checking return type due to the complicated binary
    // numeric promotion rules set by the C# language specification:
    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12473-binary-numeric-promotions
    // This is fine as the type-level semantics are preserved when the operator
    // is mutated, which allows compilation to occur as it is syntactically valid
    // (desired behaviour)

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{{0}} {originalOperator} {{1}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .NotContain(originalOperator);
    mutationGroup.SchemaMutantExpressionsTemplate.Count.Should()
      .Be(expectedReplacementOperators.Length);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{{0}} {op} {{1}}")
        .ToHashSet();

    _testOutputHelper.WriteLine(string.Join(", ", mutationGroup.SchemaMutantExpressionsTemplate));
    _testOutputHelper.WriteLine(string.Join(", ", validMutantExpressionsTemplate));
    
    mutationGroup.SchemaMutantExpressionsTemplate.ToHashSet()
      .SetEquals(validMutantExpressionsTemplate).Should().BeTrue();
  }
}
