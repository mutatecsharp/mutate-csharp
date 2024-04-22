using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.OperatorImplementation;

public class BinExprOpReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<BinExprOpReplacer, BinaryExpressionSyntax>(
        inputUnderMutation);

  private static void ShouldNotHaveValidMutationGroup(string inputUnderMutation)
  {
    TestUtil
      .ShouldNotHaveValidMutationGroup<BinExprOpReplacer,
        BinaryExpressionSyntax>(inputUnderMutation);
  }

  public static IEnumerable<object[]> BooleanMutations =
    TestUtil.GenerateMutationTestCases(["&", "&&", "|", "||", "^", "==", "!="]);

  [Theory]
  [MemberData(nameof(BooleanMutations))]
  public void
    ShouldReplaceBitwiseLogicalAndEqualityOperatorsForBooleanExpressions(
      string originalOperator, string[] expectedReplacementOperators)
  {
    var inputUnderMutation = 
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            bool b = true;
            var c = b {{originalOperator}} false;
          }
        }
       """;
    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .Equal("bool", "bool");
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

  private static ISet<string> SupportedIntegralOperators =
    new HashSet<string>
      { "+", "-", "*", "/", "%", ">>", "<<", ">>>", "^", "&", "|" };

  public static IEnumerable<object[]> IntegralTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      IntegralTypes.Keys,
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
            var y = x {{originalOperator}} {{IntegralTypes[integralType]}};
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo([integralType, integralType]);

    // Note: we omit checking return type due to the complicated binary
    // numeric promotion rules set by the C# language specification:
    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12473-binary-numeric-promotions
    //
    // Example:
    // var x = 'a' + 'a'; // returns int type
    //
    // This is fine as the return type is preserved when the operator
    // is mutated. As the code compiles before the mutation is applied, we
    // can be certain that applying mutated operators that return the same type
    // as the original return type will compile; the semantics of implicit
    // type conversion after the expression is returned will be maintained if
    // applicable

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

    mutationGroup.SchemaMutantExpressionsTemplate.ToHashSet()
      .SetEquals(validMutantExpressionsTemplate).Should().BeTrue();
  }

  private static IDictionary<string, string> FloatingPointTypes =
    new Dictionary<string, string>
    {
      { "float", "10.0f" },
      { "double", "10.0d" },
      { "decimal", "2323.232323m" }
    };

  private static ISet<string> SupportedFloatingPointOperators =
    new HashSet<string> { "+", "-", "*", "/", "%" };

  public static IEnumerable<object[]> FloatingPointTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      FloatingPointTypes.Keys,
      SupportedFloatingPointOperators);

  [Theory]
  [MemberData(nameof(FloatingPointTypedMutations))]
  public void ShouldReplaceArithmeticOperatorsForFloatingPointTypes(
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
            var x = {{FloatingPointTypes[numericType]}};
            var y = x {{originalOperator}} {{FloatingPointTypes[numericType]}};
          }
        }
        """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo([numericType, numericType]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo(numericType);

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
      = expectedReplacementOperators.Select(op => $"{{0}} {op} {{1}}");

    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo(validMutantExpressionsTemplate);
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
    ShouldReplaceOperatorForUserDefinedClassIfReplacementOperatorExistsInClass(
      string originalOperator, string replacementOperator)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class B {}

        public class A
        {
          public static B operator{{originalOperator}}(A a1, int b)
          {
            return new B();
          }
          
          public static B operator{{replacementOperator}}(A a1, int b)
          {
            return new B();
          }
          
          public static void Main()
          {
            A a = new A();
            B b = a {{originalOperator}} 1;
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .BeEquivalentTo(["A", "int"]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("B");

    // Expression template checks
    mutationGroup.SchemaOriginalExpressionTemplate.Should()
      .BeEquivalentTo($"{{0}} {originalOperator} {{1}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo([$"{{0}} {replacementOperator} {{1}}"]);
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
    ShouldNotReplaceOperatorIfNoViableCandidatesExist(
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
            var a = new A();
            var a2 = a {{originalOperator}} 1;
          }
        }
        """;

    ShouldNotHaveValidMutationGroup(inputUnderMutation);
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
  public void ShouldNotReplaceOperatorIfReplacementOperatorReturnTypeDiffers(
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
            var a = new A();
            var a2 = a {{originalOperator}} 1;
          }
        }
        """;

    // No mutation groups should be generated since return type of
    // replacement operator is not assignable to original return type
    ShouldNotHaveValidMutationGroup(inputUnderMutation);
  }

  [Fact]
  public void ShouldNotReplaceIfReplacementOperatorAssignmentAmbiguous()
  {
    const string inputUnderMutation =
      """
      public class D {}

      public class A {}

      public class B: D
      {
        public static A operator +(B b, A a) => new A();
        public static A operator -(B b, D d) => new A();
      }

      public class C: A
      {
        public static A operator +(D d, C c) => new A();
      }

      public class E
      {
        public static void Main()
        {
          var b = new B();
          var d = new D();
          var x = b - d;
        }
      }
      """;

    ShouldNotHaveValidMutationGroup(inputUnderMutation);
  }

  [Fact]
  public void ShouldReplaceAndResolveOverloadedReplacementOperator()
  {
    var inputUnderMutation =
      """
      public class A
      {
        public static A operator -(A a, B b) => new B();
        public static A operator +(A a, A b) => new A();
      }

      public class B: A
      {
        public static B operator +(A a, B b) => new B();
      }

      public class C
      {
        public static void Main()
        {
          var a = new A();
          var b = new B();
          var c = a - b; // Returns instance of type A
        }
      }
      """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Return type A is the original return type
    // mutant expression qualifies as return type B is assignable to A 
    mutationGroup.SchemaReturnType.Should().Be("A");
    mutationGroup.SchemaParameterTypes.Should().Equal("A", "B");
    mutationGroup.SchemaOriginalExpressionTemplate.Should().Be("{0} - {1}");
    mutationGroup.SchemaMutantExpressionsTemplate.Should()
      .BeEquivalentTo(["{0} + {1}"]);
  }

  [Theory]
  [InlineData("String")]
  public void ShouldNotReplaceUnsupportedPredefinedTypes(string predefinedType)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            var x = Convert.To{{predefinedType}}(42) + Convert.To{{predefinedType}}(42);
          }
        }
        """;

    ShouldNotHaveValidMutationGroup(inputUnderMutation);
  }
}