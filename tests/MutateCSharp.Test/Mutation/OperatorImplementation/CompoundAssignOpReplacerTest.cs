using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.OperatorImplementation;

public class CompoundAssignOpReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetMutationGroup(string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<CompoundAssignOpReplacer,
        AssignmentExpressionSyntax>(inputUnderMutation);
  
  private static void ShouldNotHaveValidMutationGroup(string inputUnderMutation)
  {
    TestUtil.ShouldNotHaveValidMutationGroup<CompoundAssignOpReplacer, AssignmentExpressionSyntax>(inputUnderMutation);
  }
  
  public static IEnumerable<object[]> BooleanMutations =
    TestUtil.GenerateMutationTestCases(["&=", "^=", "|="]);

  [Theory]
  [MemberData(nameof(BooleanMutations))]
  public void ShouldReplaceForBooleanTypes(
      string originalOperator, string[] expectedReplacementOperators)
  {
    var inputUnderMutation = $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            bool b = true;
            b {{originalOperator}} false;
          }
        }
        """;
    var mutationGroup = GetMutationGroup(inputUnderMutation);

    // Sanity check
    mutationGroup.Should().NotBeNull();

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .Equal("ref bool", "bool");
    mutationGroup.SchemaReturnType.Should().Be("void");

    // Expression template checks
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{{0}} {originalOperator} {{1}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    var mutantExpressions = TestUtil.GetMutantExpressionTemplates(mutationGroup).ToArray();
    mutantExpressions.Length.Should().Be(expectedReplacementOperators.Length);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{{0}} {op} {{1}}");

    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should().BeEquivalentTo(validMutantExpressionsTemplate);
  }

  private static IDictionary<string, string> IntegralTypes = new Dictionary<string, string>
  {
    {"char", "'a'"},
    {"short", "((short) 12)"},
    {"sbyte", "((sbyte) 1)"}, 
    {"int", "1"}, 
    {"long", "42l"}, 
    {"byte", "((byte) 0b11)"}, 
    {"ushort", "((ushort) 11)"}, 
    {"uint", "((uint) 10)"}, 
    {"ulong", "11ul"}
  };

  private static ISet<string> SupportedIntegralOperators =
    new HashSet<string>
      { "+=", "-=", "*=", "/=", "%=", ">>=", "<<=", "^=", "&=", "|=" };
  
  public static IEnumerable<object[]> IntegralTypedMutations =
    TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(IntegralTypes.Keys,
      SupportedIntegralOperators);

  [Theory]
  [MemberData(nameof(IntegralTypedMutations))]
  public void
    ShouldReplaceArithmeticAndBitwiseOperatorsForIntegralTypes(
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
            x {{originalOperator}} {{IntegralTypes[integralType]}};
          }
        }
        """;
    
    // Skip test if the input does not compile
    if (TestUtil.GetCompilation(CSharpSyntaxTree.ParseText(inputUnderMutation))
        .GetDiagnostics()
        .Any(d => d.Severity == DiagnosticSeverity.Error))
    {
      testOutputHelper.WriteLine("We can safely skip the test as the original input does not compile");
      return;
    }
    
    var mutationGroup = GetMutationGroup(inputUnderMutation);
    
    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .Equal($"ref {integralType}", integralType);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("void");

    // Expression template checks
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo($"{{0}} {originalOperator} {{1}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    var mutantExpressions = TestUtil.GetMutantExpressionTemplates(mutationGroup).ToArray();
    mutantExpressions.Length.Should().Be(expectedReplacementOperators.Length);

    // The expressions should match (regardless of order)
    var validMutantExpressionsTemplate
      = expectedReplacementOperators.Select(op => $"{{0}} {op} {{1}}");
    mutantExpressions.Should().BeEquivalentTo(validMutantExpressionsTemplate);
  }
  
  private static IDictionary<string, string> FloatingPointTypes = new Dictionary<string, string>
  {
    {"float", "10.0f"},
    {"double", "10.0d"},
    {"decimal", "2323.232323m"}
  };
  
  private static ISet<string> SupportedFloatingPointOperators =
    new HashSet<string> { "+=", "-=", "*=", "/=", "%=" };
  
  public static IEnumerable<object[]> FloatingPointTypedMutations
    = TestUtil.GenerateTestCaseCombinationsBetweenTypeAndMutations(FloatingPointTypes.Keys,
      SupportedFloatingPointOperators);
  
   [Theory]
   [MemberData(nameof(FloatingPointTypedMutations))]
   public void
     ShouldReplaceArithmeticOperatorsForFloatingPointTypes(
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
             x {{originalOperator}} {{FloatingPointTypes[numericType]}};
           }
         }
         """;
     
     var mutationGroup = GetMutationGroup(inputUnderMutation);

     // Type checks
     mutationGroup.SchemaParameterTypes.Should()
       .Equal($"ref {numericType}", numericType);
     mutationGroup.SchemaReturnType.Should().BeEquivalentTo("void");

     // Expression template checks
     mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
       .BeEquivalentTo($"{{0}} {originalOperator} {{1}}");
     // The mutation operator should not be able to mutate the compound assignment
     // operator to itself
     var mutantExpressions =
       TestUtil.GetMutantExpressionTemplates(mutationGroup).ToArray();
     mutantExpressions.Length.Should().Be(expectedReplacementOperators.Length);

     // The expressions should match (regardless of order)
     var validMutantExpressionsTemplate
       = expectedReplacementOperators.Select(op => $"{{0}} {op} {{1}}");

     mutantExpressions.Should().BeEquivalentTo(validMutantExpressionsTemplate);
   }

  [Theory]
  // Arithmetic operators
  [InlineData("+", "-")]
  [InlineData("-", "+")]
  [InlineData("/", "*")]
  [InlineData("%", "*")]
  // Bitwise operators
  [InlineData("<<", ">>")]
  // [InlineData("<<", ">>>")]
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

    // Type checks
    mutationGroup.SchemaParameterTypes.Should()
      .Equal("ref A", "int");
    mutationGroup.SchemaReturnType.Should().Be("void");

    // Expression template checks
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .Be($"{{0}} {originalOperator}= {{1}}");
    // The mutation operator should not be able to mutate the compound assignment
    // operator to itself
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
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
  // [InlineData(">>>")]
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
            A a = new A();
            a {{originalOperator}}= 1;
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
  // [InlineData("<<", ">>>")]
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
            a {{originalOperator}}= 1;
          }
        }
        """;
    
    // No mutation groups should be generated:
    // given original operator op1 and replacement operator op2,
    // a op1= b is equivalent to a = a op1 b which type checks,
    // but a op2= b is equivalent to a = a op2 b but op2 returns int instead of A
    // which is not assignable to A
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
        public static B operator +(B b, A a) => new B();
        public static B operator -(B b, D d) => new B();
      }
      
      public class C: A
      {
        public static B operator +(D d, C c) => new B();
      }
      
      public class E
      {
        public static void Main()
        { 
          var b = new B();
          var d = new D();
          b -= d;
        }
      }
      """;
    
    ShouldNotHaveValidMutationGroup(inputUnderMutation);
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
            var x = Convert.To{{predefinedType}}(42);
            x += Convert.To{{predefinedType}}(42);
          }
        }
        """;
    
    ShouldNotHaveValidMutationGroup(inputUnderMutation);
  }
}