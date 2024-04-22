using FluentAssertions;
using FluentAssertions.Formatting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using MutateCSharp.Util;
using Xunit.Abstractions;
using Formatter = Microsoft.CodeAnalysis.Formatting.Formatter;

namespace MutateCSharp.Test.Mutation.OperatorImplementation;

public class PrefixUnaryExprOpReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<PrefixUnaryExprOpReplacer, PrefixUnaryExpressionSyntax>(
        inputUnderMutation);

  private static MutationGroup[] GetAllMutationGroups(string inputUnderMutation)
    => TestUtil
      .GetAllValidMutationGroups<PrefixUnaryExprOpReplacer,
        PrefixUnaryExpressionSyntax>(inputUnderMutation);
  private static void ShouldNotHaveValidMutationGroup(string inputUnderMutation)
  {
    TestUtil.ShouldNotHaveValidMutationGroup<PrefixUnaryExprOpReplacer, PrefixUnaryExpressionSyntax>(inputUnderMutation);
  }

  public static IEnumerable<object[]> BooleanMutations =
    TestUtil.GenerateMutationTestCases(["!"]);

  [Theory]
  [MemberData(nameof(BooleanMutations))]
  // No other unary operators can serve as replacement for boolean variables that
  // are already wrapped in unary operator; the assumption that a unary operator
  // must be replaced with another one will be alleviated later
  public void ShouldNotReplaceBooleanExpressions(string originalOperator, string[] expectedReplacementOperators)
  {
    var inputUnderMutation = 
      $$"""
       using System;

       public class A
       {
         public static void Main()
         {
           bool b = true;
           var c = {{originalOperator}}b;
         }
       }
       """;
    
    ShouldNotHaveValidMutationGroup(inputUnderMutation);
  }
  
  private static IDictionary<string, string> IntegralTypes =
    new Dictionary<string, string>
    {
      { "char", "'a'" },
      { "short", "((short) 12)" },
      { "sbyte", "((sbyte) 1)" },
      { "int", "1" },
      { "long", "42l" },
      { "byte", "((byte) 0b11)" },
      { "ushort", "((ushort) 11)" },
      { "uint", "((uint) 10)" },
      { "ulong", "11ul" }
    };

  private static ISet<string> SupportedNonAssignableIntegralOperators =
    new HashSet<string> { "+", "-", "~" };

  public static IEnumerable<object[]> NonAssignableIntegralTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      IntegralTypes.Keys,
      SupportedNonAssignableIntegralOperators);
  
  [Theory]
  [MemberData(nameof(NonAssignableIntegralTypedMutations))]
  public void
    ShouldReplaceArithmeticBitwiseOperatorsForNonAssignableIntegralTypes(
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
            var y = {{originalOperator}}{{IntegralTypes[integralType]}};
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .Equal(integralType);
    mutationGroup.SchemaReturnType.Should().Be(integralType);

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .NotContain(originalOperator);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");

    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo(validMutantExpressionsTemplate);
  }
  
  private static ISet<string> SupportedAssignableIntegralOperators =
    new HashSet<string> { "+", "-", "~", "++", "--" };

  public static IEnumerable<object[]> AssignableIntegralTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      IntegralTypes.Keys,
      SupportedAssignableIntegralOperators);
  
  [Theory]
  [MemberData(nameof(AssignableIntegralTypedMutations))]
  public void
    ShouldReplaceArithmeticBitwiseOperatorsForAssignableIntegralTypes(
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
            var y = {{originalOperator}}x;
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks (Should take a reference to the assignable value)
    mutationGroup.SchemaParameterTypes.Should()
      .Equal($"ref {integralType}");
    mutationGroup.SchemaReturnType.Should().Be(integralType);

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .NotContain(originalOperator);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");

    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo(validMutantExpressionsTemplate);
  }
  
  private static IDictionary<string, string> FloatingPointTypes =
    new Dictionary<string, string>
    {
      { "float", "10.0f" },
      { "double", "10.0d" },
      { "decimal", "2323.232323m" }
    };
  
  private static ISet<string> SupportedNonAssignableFloatingPointOperators =
    new HashSet<string> { "+", "-" };
  
  public static IEnumerable<object[]> NonAssignableFPTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      FloatingPointTypes.Keys,
      SupportedNonAssignableFloatingPointOperators);
  
  [Theory]
  [MemberData(nameof(NonAssignableFPTypedMutations))]
  public void
    ShouldReplaceArithmeticOperatorsForNonAssignableFloatingPointTypes(
      string fpType, string originalOperator,
      string[] expectedReplacementOperators)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            var y = {{originalOperator}}{{FloatingPointTypes[fpType]}};
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .Equal(fpType);
    mutationGroup.SchemaReturnType.Should().Be(fpType);

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .NotContain(originalOperator);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");

    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo(validMutantExpressionsTemplate);
  }

  private static ISet<string> SupportedAssignableFloatingPointOperators =
    new HashSet<string> { "+", "-", "++", "--" };

  public static IEnumerable<object[]> AssignableFPTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      FloatingPointTypes.Keys,
      SupportedAssignableFloatingPointOperators);
  
  [Theory]
  [MemberData(nameof(AssignableFPTypedMutations))]
  public void
    ShouldReplaceArithmeticOperatorsAssignableIntegralTypes(
      string fpType, string originalOperator,
      string[] expectedReplacementOperators)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{fpType}} x = {{FloatingPointTypes[fpType]}};
            var y = {{originalOperator}}x;
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks (Should take a reference to the assignable value)
    mutationGroup.SchemaParameterTypes.Should()
      .Equal($"ref {fpType}");
    mutationGroup.SchemaReturnType.Should().Be(fpType);

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .NotContain(originalOperator);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");

    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo(validMutantExpressionsTemplate);
  }
  
  [Theory]
  [InlineData("++", "--")]
  [InlineData("--", "++")]
  [InlineData("+", "-")]
  [InlineData("-", "+")]
  [InlineData("+", "~")]
  [InlineData("-", "~")]
  [InlineData("^", "&")]
  public void
    ShouldReplaceOperatorForUserDefinedClassIfReplacementOperatorExistsInClass(
      string originalOperator, string replacementOperator)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class B {}

        public class A
        {
          public static B operator{{originalOperator}}(A a1)
          {
            return new B();
          }
          
          public static B operator{{replacementOperator}}(A a1)
          {
            return new B();
          }
          
          public static void Main()
          {
            A a = new A();
            var b = {{originalOperator}}a;
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo(["ref A"]);
    mutationGroup.SchemaReturnType.Should().Be("B");

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo([$"{replacementOperator}{{0}}"]);
  }
  
  [Fact]
  public void ShouldReplaceAndResolveOverloadedReplacementOperator()
  {
    var inputUnderMutation =
      """
      public class A
      {
        public static A operator -(A a) => new B();
        public static A operator +(A a) => new A();
        public static int operator +(B b, A a) => 0; // noise 
        public static int operator -(B b, D d) => 0; // noise
      }

      public class B: A
      {
        public static B operator +(B b) => new B();
      }

      public class C
      {
        public static void Main()
        {
          var a = new A();
          var b = +a;
        }
      }
      """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Return type A is the original return type
    // mutant expression qualifies as return type B is assignable to A 
    mutationGroup.SchemaReturnType.Should().Be("A");
    mutationGroup.SchemaParameterTypes.Should().Equal("A");
    mutationGroup.SchemaOriginalExpressionTemplate.Should().Be("+{0}");
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo(["-{0}"]);
  }

  [Fact]
  public void ReplaceAssignableVariablesInVariousScenariosWithUpdateOperators()
  {
    var inputUnderMutation =
      """
      public class A 
      { 
        public int f1 = 0;
        public int p1 { get; set; }
        
        public int foo(ref int x, out int y)
        { 
          y = 0;
          var local1 = +f1;
          var local2 = +x;
          var local3 = -y;
          var local4 = +local1;
          var local5 = +p1;
          return 0;
        }
        
        public static void Main() {}
      }
      """;

    var mutationGroups = GetAllMutationGroups(inputUnderMutation);
    mutationGroups.Length.Should().Be(5);
    
    foreach (var group in mutationGroups)
    {
      group.SchemaParameterTypes.Should().Equal("ref int");
      group.SchemaMutantExpressionsTemplate.Should().Contain("++{0}");
      group.SchemaMutantExpressionsTemplate.Should().Contain("--{0}");
    }
  }
  
  [Fact]
  public void DoNotReplaceNonAssignableVariablesInVariousScenarios()
  {
    var inputUnderMutation =
      """
      public class A
      {
        public const int f1 = 0;
        public readonly int f2 = 0;
        public int p1 { get; }
        
        public int foo(in int x)
        {
          const int local1 = +f1;
          var local2 = ~x;
          var local3 = -f2;
          var local4 = +p1;
          var local5 = +local1;
          return 0;
        }
        
        public static void Main() {}
      }
      """;

    var mutationGroups = GetAllMutationGroups(inputUnderMutation);
    mutationGroups.Length.Should().Be(5);
    
    foreach (var group in mutationGroups)
    {
      group.SchemaParameterTypes.Should().Equal("int");
      group.SchemaMutantExpressionsTemplate.Should().NotContain("++{0}");
      group.SchemaMutantExpressionsTemplate.Should().NotContain("--{0}");
    }
  }
}