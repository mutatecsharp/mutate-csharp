using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using Xunit.Abstractions;

namespace MutateCSharp.Test;

public class CompoundAssignOpReplacerTest
{
  private readonly ITestOutputHelper _testOutputHelper;

  public CompoundAssignOpReplacerTest(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }

  private MutationGroup? GetMutationGroup(string inputUnderMutation)
  {
    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    TestUtil.TestForSyntacticErrors(inputAst);

    var compilation = TestUtil.GetAstCompilation(inputAst);
    var model = compilation.GetSemanticModel(inputAst);
    var mutationOperator = new CompoundAssignOpReplacer(model);
    var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<AssignmentExpressionSyntax>().FirstOrDefault();

    constructUnderTest.Should().NotBeNull();

    return mutationOperator.CreateMutationGroup(constructUnderTest);
  }

  private static IEnumerable<object[]>
    GenerateMutationTestCases(IEnumerable<string> operators)
  {
    var opSet = operators.ToHashSet();
    return opSet.Select(op => new object[]
      { op, opSet.Except([op]).ToArray() });
  }

  private static IEnumerable<object[]>
    GenerateTestCaseCombinationsBetweenTypeAndMutations(
      IEnumerable<string> types, IEnumerable<string> operators)
  {
    var opSet = operators.ToHashSet();
    var mutations = GenerateMutationTestCases(opSet);
    
    // Get unique pairs between types and operators
    return types.SelectMany(type =>
      mutations.Select(group => new[]
        { type, group[0], group[1] }));
  }
  

  // Set of supported boolean operators
  // [
  //   ["&", new []{"^", "|"}],
  //   ["|", new []{"&", "^"}],
  //   ["^", new []{"&", "|"}],
  // ];
  public static IEnumerable<object[]> BooleanMutations =
    GenerateMutationTestCases(["&", "^", "|"]);

  [Theory]
  [MemberData(nameof(BooleanMutations))]
  public void
    CompoundAssignmentReplacer_ShouldReplaceForBooleanTypes(
      string originalOperator, string[] expectedReplacementOperators)
  {
    var inputUnderMutation = $"bool b = true; b {originalOperator}= false;";
    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Sanity check
    mutationGroup.Should().NotBeNull();

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo(["ref Boolean", "Boolean"]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("void");

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{{0}} {originalOperator}= {{1}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Count.Should()
      .Be(expectedReplacementOperators.Length);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{{0}} {op}= {{1}}")
        .ToHashSet();

    mutationGroup.SchemaMutantExpressionsTemplate.ToHashSet()
      .SetEquals(validMutantExpressionsTemplate).Should().BeTrue();
  }

  private static ISet<string> IntegralTypes = new HashSet<string>
  {
    "Char", "SByte", "Int32", "Int64", "Byte", "UInt16", "UInt32", "UInt64"
  };

  private static ISet<string> SupportedIntegralOperators =
    CompoundAssignOpReplacer.SupportedArithmeticOperators.Values
      .Union(CompoundAssignOpReplacer.SupportedBitwiseOperators.Values)
      .ToHashSet();
  
  public static IEnumerable<object[]> IntegralTypedMutations =
    GenerateTestCaseCombinationsBetweenTypeAndMutations(IntegralTypes,
      SupportedIntegralOperators);

  [Theory]
  [MemberData(nameof(IntegralTypedMutations))]
  public void
    CompoundAssignmentReplacer_ShouldReplaceArithmeticAndBitwiseOperatorsForIntegralTypes(
      string numericType, string originalOperator,
      string[] expectedReplacementOperators)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{numericType}} x = 42;
            x {{originalOperator}} 42;
          }
        }
        """;

    _testOutputHelper.WriteLine(inputUnderMutation);
    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Sanity check
    mutationGroup.Should().NotBeNull();

    // Type checks
    // Left type is specified; Right type (42) is always Int32
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo([$"ref {numericType}", "Int32"]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("void");

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
  
  private static ISet<string> FloatingPointTypes = new HashSet<string>
  {
    "Single", "Double", "Decimal"
  };

  public static IEnumerable<object[]> FloatingPointTypedMutations
    = GenerateTestCaseCombinationsBetweenTypeAndMutations(FloatingPointTypes,
      CompoundAssignOpReplacer.SupportedArithmeticOperators.Values);
  
   [Theory]
   [MemberData(nameof(FloatingPointTypedMutations))]
   public void
     CompoundAssignmentReplacer_ShouldReplaceArithmeticOperatorsForFloatingPointTypes(
       string numericType, string originalOperator,
       string[] expectedReplacementOperators)
   {
     var inputUnderMutation =
       $$"""
         using System;

         public class A
         {
           public static void Main()
           {
             {{numericType}} x = 42;
             x {{originalOperator}} 42;
           }
         }
         """;

     _testOutputHelper.WriteLine(inputUnderMutation);
     var mutationGroup = GetMutationGroup(inputUnderMutation);

     // Sanity check
     mutationGroup.Should().NotBeNull();

     // Type checks
     // Left type is specified; Right type (42) is always Int32
     mutationGroup.SchemaParameterTypes.Should()
       .BeEquivalentTo([$"ref {numericType}", "Int32"]);
     mutationGroup.SchemaReturnType.Should().BeEquivalentTo("void");

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

  [Theory]
  // Arithmetic operators
  [InlineData("+", "-")]
  [InlineData("-", "+")]
  [InlineData("/", "*")]
  [InlineData("%", "*")]
  // Bitwise operators
  [InlineData("<<", ">>")]
  [InlineData("<<", ">>>")]
  [InlineData(">>", "<<")]
  [InlineData("&", "^")]
  [InlineData("^", "&")]
  // Boolean operators
  [InlineData("&", "|")]
  [InlineData("|", "&")]
  // Replacement operators from different operator group
  // (user defined types may have custom semantics)
  [InlineData("&", "+")]
  [InlineData("^", "-")]
  [InlineData("*", "|")]
  public void
    CompoundAssignmentReplacer_ShouldReplaceOperatorForUserDefinedClassIfReplacementOperatorExistsInClass(
      string originalOperator, string replacementOperator)
  {
    var inputUnderMutation =
      $$"""
        public class A
        {
          public static A operator{{originalOperator}}(A a1, int b)
          {
            return a1;
          }
          
          public static A operator{{replacementOperator}}(A a1, int b)
          {
            return a1;
          }
          
          public static void Main()
          {
            A a = new A();
            a {{originalOperator}}= 1;
          }
        }
        """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Sanity check
    mutationGroup.Should().NotBeNull();

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo(["ref A", "Int32"]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("void");

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{{0}} {originalOperator}= {{1}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Count.Should().Be(1);
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo([$"{{0}} {replacementOperator}= {{1}}"]);
  }

  [Theory]
  // Arithmetic operators
  [InlineData("+")]
  [InlineData("-")]
  [InlineData("/")]
  [InlineData("*")]
  [InlineData("%")]
  // Bitwise/boolean operators
  [InlineData(">>")]
  [InlineData("<<")]
  [InlineData(">>>")]
  [InlineData("&")]
  [InlineData("^")]
  [InlineData("|")]
  public void
    CompoundAssignmentReplacer_ShouldNotReplaceOperatorIfNoViableCandidatesExist(
      string originalOperator)
  {
    var inputUnderMutation =
      $$"""
        public class A
        {
          public static A operator{{originalOperator}}(A a1, int b)
          {
            return a1;
          }
          
          public static void Main()
          {
            A a = new A();
            a {{originalOperator}}= 1;
          }
        }
        """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // No mutation groups should be generated
    mutationGroup.Should().BeNull();
  }

  [Theory]
  // Arithmetic operators
  [InlineData("+", "-")]
  [InlineData("-", "+")]
  [InlineData("/", "*")]
  [InlineData("%", "*")]
  // Bitwise operators
  [InlineData("<<", ">>")]
  [InlineData("<<", ">>>")]
  [InlineData(">>", "<<")]
  [InlineData("&", "^")]
  [InlineData("^", "&")]
  // Boolean operators
  [InlineData("&", "|")]
  [InlineData("|", "&")]
  // Replacement operators from different operator group
  // (user defined types may have custom semantics)
  [InlineData("&", "+")]
  [InlineData("^", "-")]
  [InlineData("*", "|")]
  public void
    CompoundAssignmentReplacer_ShouldNotReplaceOperatorIfReplacementOperatorReturnTypeDiffers(
      string originalOperator, string replacementOperator)
  {
    var inputUnderMutation =
      $$"""
        public class A
        {
          public static A operator{{originalOperator}}(A a1, int b)
          {
            return a1;
          }
          
          public static int operator{{replacementOperator}}(A a1, int b)
          {
            return b;
          }
          
          public static void Main()
          {
            A a = new A();
            a {{originalOperator}}= 1;
          }
        }
        """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // No mutation groups should be generated:
    // given original operator op1 and replacement operator op2,
    // a op1= b is equivalent to a = a op1 b which type checks,
    // but a op2= b is equivalent to a = a op2 b but op2 returns int instead of A
    // which is not assignable to A
    mutationGroup.Should().BeNull();
  }
}