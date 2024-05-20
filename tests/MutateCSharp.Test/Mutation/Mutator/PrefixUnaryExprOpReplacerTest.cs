using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Mutator;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Mutator;

public class PrefixUnaryExprOpReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .UnaryGetValidMutationGroup<PrefixUnaryExprOpReplacer, PrefixUnaryExpressionSyntax>(
        inputUnderMutation);

  private static MutationGroup[] GetAllMutationGroups(string inputUnderMutation)
    => TestUtil
      .UnaryGetAllValidMutationGroups<PrefixUnaryExprOpReplacer,
        PrefixUnaryExpressionSyntax>(inputUnderMutation);
  
  public static IEnumerable<object[]> BooleanMutations =
    TestUtil.GenerateMutationTestCases(["!"]);

  [Theory]
  [MemberData(nameof(BooleanMutations))]
  // No other unary operators can serve as replacement for boolean variables that
  // are already wrapped in unary operator; the assumption that a unary operator
  // must be replaced with another one will be alleviated later
  public void ShouldReplaceBooleanExpressions(string originalOperator, string[] expectedReplacementOperators)
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

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    mutationGroup.SchemaParameterTypes.Should().Equal("bool");
    mutationGroup.SchemaReturnType.Should().Be("bool");
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should().Be($"{originalOperator}{{0}}");
    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate).Should()
      .Equal("true", "false");
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
  
  private static IEnumerable<object[]> _nonAssignableIntegralTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      ["char", "short", "sbyte", "int", "long", "byte", "ushort", "uint"],
      ["+", "-", "~"]);
  
  // Operator '-' cannot be applied to operand of type 'ulong'
  private static IEnumerable<object[]> _nonAssignableUlongMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      ["ulong"], ["+", "~"]);

  public static IEnumerable<object[]> NonAssignableIntegralMutations =
    _nonAssignableIntegralTypedMutations.Concat(_nonAssignableUlongMutations);
  
  [Theory]
  [MemberData(nameof(NonAssignableIntegralMutations))]
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
    
    // Note: we omit checking return type due to the complicated unary
    // numeric promotion rules set by the C# language specification:
    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12472-unary-numeric-promotions
    // Example:
    // byte y = 1;
    // var x = +y; // returns int type
    //
    // This is fine as the return type is preserved when the operator
    // is mutated. As the code compiles before the mutation is applied, we
    // can be certain that applying mutated operators that return the same type
    // as the original return type will compile; the semantics of implicit
    // type conversion after the expression is returned will be maintained if
    // applicable

    // Expression template checks
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .NotContain(originalOperator);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");

    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(validMutantExpressionsTemplate);
  }
  
  // unary -, +, ~ cannot be applied to byte, sbyte, ushort, short, uint, char
  // and return its own type; the type will be promoted to int
  // This set of test case allows example constructs such as:
  // byte b = 2;
  // var x = ++b; // x is of type byte
  // to accept mutant operator(s) --
  private static readonly IEnumerable<object[]>
    AssignableNotPromotedIntegralMutations
      = TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
          ["char", "short", "sbyte", "byte", "ushort", "uint"],
          ["+", "-", "~", "++", "--"]
        ).Where(test => test[1] is not ("+" or "-" or "~"))
        .Select(test =>
        {
          var expectedReplacementOperators =
            (test[2] as string[]).Where(op => op is not ("+" or "-" or "~"))
            .ToArray();

          return new[]{ test[0], test[1], expectedReplacementOperators };
        });
    
  // This set of test case allows example constructs such as:
  // byte b = 2;
  // var x = +b; // x is of type int
  // to accept mutant operator(s) -, ~, ++, --
  private static readonly IEnumerable<object[]>
    AssignableIntegralTypedMutations =
      TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
        ["char", "short", "sbyte", "int", "long", "byte", "ushort", "uint"],
        ["+", "-", "~", "++", "--"]
      ).Where(test => test[1] is ("+" or "-" or "~"));
  
  private static readonly IEnumerable<object[]> AssignableUlongMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(
      ["uint", "ulong"], ["+", "~", "++", "--"]);

  public static IEnumerable<object[]> AssignableIntegralMutations =
    AssignableNotPromotedIntegralMutations
      .Concat(AssignableIntegralTypedMutations)
      .Concat(AssignableUlongMutations);
  
  [Theory]
  [MemberData(nameof(AssignableIntegralMutations))]
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
    
    // Note: we omit checking return type due to the complicated unary
    // numeric promotion rules set by the C# language specification:
    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12472-unary-numeric-promotions
    // Example:
    // byte y = 1;
    // var x = +y; // returns int type
    //
    // This is fine as the return type is preserved when the operator
    // is mutated. As the code compiles before the mutation is applied, we
    // can be certain that applying mutated operators that return the same type
    // as the original return type will compile; the semantics of implicit
    // type conversion after the expression is returned will be maintained if
    // applicable
    
    // Expression template checks
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .NotContain(originalOperator);
    
    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");
    
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
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
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .NotContain(originalOperator);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");

    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
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
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    var mutantExpressions =
      TestUtil.GetMutantExpressionTemplates(mutationGroup).ToHashSet();
    mutantExpressions.Should().NotContain(originalOperator);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");

    mutantExpressions.Should().BeEquivalentTo(validMutantExpressionsTemplate);
  }
  
  [Theory]
  [InlineData("+", "-")]
  [InlineData("-", "+")]
  [InlineData("+", "~")]
  [InlineData("-", "~")]
  [InlineData("~", "-")]
  public void
    ShouldReplaceOperatorForUserDefinedClassIfReplacementOperatorExistsInClassThatDoesNotModifyVariable(
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
    mutationGroup.SchemaParameterTypes.Should().Equal(["A"]);
    mutationGroup.SchemaReturnType.Should().Be("B");

    // Expression template checks
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo([$"{replacementOperator}{{0}}"]);
  }
  
  [Theory]
  [InlineData("++", "--")]
  [InlineData("--", "++")]
  [InlineData("++", "+")]
  [InlineData("+", "++")]
  public void
    ShouldReplaceOperatorForUserDefinedClassIfReplacementOperatorExistsInClassThatModifiesVariable(
      string originalOperator, string replacementOperator)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static A operator{{originalOperator}}(A a1) => new A();
          
          public static A operator{{replacementOperator}}(A a1) => new A();
          
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
    mutationGroup.SchemaParameterTypes.Should().Equal(["ref A"]);
    mutationGroup.SchemaReturnType.Should().Be("A");

    // Expression template checks
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
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
        public static int operator -(B b, A a) => 0; // noise
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
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should().Be("+{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
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
      var mutantExpressions = TestUtil.GetMutantExpressionTemplates(group).ToHashSet();
      mutantExpressions.Should().Contain("++{0}");
      mutantExpressions.Should().Contain("--{0}");
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
      var mutantExpressions = TestUtil.GetMutantExpressionTemplates(group).ToHashSet();
      mutantExpressions.Should().NotContain("++{0}");
      mutantExpressions.Should().NotContain("--{0}");
    }
  }

  [Theory]
  [MemberData(nameof(AssignableIntegralMutations))]
  public void ShouldReplaceForNullableAssignableIntegralTypes(
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
            {{integralType}}? x = null;
            var y = {{originalOperator}}x;
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Type checks (Should take a reference to the assignable value)
    mutationGroup.SchemaParameterTypes.Should()
      .Equal($"ref {integralType}?");
    
    // Note: we omit checking return type due to the complicated unary
    // numeric promotion rules set by the C# language specification:
    // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#12472-unary-numeric-promotions
    // Example:
    // byte y = 1;
    // var x = +y; // returns int type
    //
    // This is fine as the return type is preserved when the operator
    // is mutated. As the code compiles before the mutation is applied, we
    // can be certain that applying mutated operators that return the same type
    // as the original return type will compile; the semantics of implicit
    // type conversion after the expression is returned will be maintained if
    // applicable
    
    // Here we are more interested that the schema returns nullable type
    mutationGroup.SchemaReturnType.Should().EndWith("?");

    // Expression template checks
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{originalOperator}{{0}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .NotContain(originalOperator);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{op}{{0}}");

    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(validMutantExpressionsTemplate);
  }

  [Fact]
  public void
    ShouldReplaceForNullableUserDefinedTypesWithoutModifiableOperators()
  {
    const string inputUnderMutation =
      """
      using System;

      public class A
      {
        public static A? operator+(A? a) => null;
        public static A? operator-(A? a) => null;
        
        public static void Main()
        {
          A? a = null;
          var b = +a;
        }
      }
      """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);
    mutationGroup.SchemaReturnType.Should().Be("A?");
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .Be("+{0}");
    mutationGroup.SchemaParameterTypes.Should().Equal("A");
    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate)
      .Should().BeEquivalentTo(["-{0}"]);
  }
  
  [Theory]
  [InlineData("++", "-")]
  [InlineData("-", "++")]
  [InlineData("++", "--")]
  public void
    ShouldReplaceForNullableUserDefinedTypesWithModifiableOperators(string originalOperator, string replacementOperator)
  {
    var inputUnderMutation =
      $$"""
      using System;

      public class A
      {
        public static A? operator{{originalOperator}}(A? a) => null;
        public static A? operator{{replacementOperator}}(A? a) => null;
        
        public static void Main()
        {
          A? a = null;
          var b = {{originalOperator}}a;
        }
      }
      """;

    var mutationGroup = GetMutationGroup(inputUnderMutation);
    mutationGroup.SchemaReturnType.Should().Be("A?");
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .Be($"{originalOperator}{{0}}");
    mutationGroup.SchemaParameterTypes.Should().Equal("ref A");
    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate)
      .Should().BeEquivalentTo([$"{replacementOperator}{{0}}"]);
  }

  [Fact]
  public void
    ShouldReplaceForNullableUserDefinedTypesThatAssignNullableToNonNullableType()
  { 
    const string inputUnderMutation =
      """
      using System;

      public class A
      {
        public static A? operator+(A? a) => null;
        public static A? operator-(A? a) => null;
        
        public static void Main()
        {
          A? a = null;
          A b = +a;
        }
      }
      """;
    
    var mutationGroup = GetMutationGroup(inputUnderMutation);
    mutationGroup.SchemaReturnType.Should().Be("A?");
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .Be("+{0}");
    mutationGroup.SchemaParameterTypes.Should().Equal("A");
    mutationGroup.SchemaMutantExpressions
      .Select(mutant => mutant.ExpressionTemplate)
      .Should().BeEquivalentTo(["-{0}"]);
  }
}