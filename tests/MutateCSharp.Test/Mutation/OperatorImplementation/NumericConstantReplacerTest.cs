using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.OperatorImplementation;
using MutateCSharp.Util;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.OperatorImplementation;

public class NumericConstantReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetValidMutationGroup(string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<NumericConstantReplacer, LiteralExpressionSyntax>(
        inputUnderMutation);

  private static MutationGroup[] GetAllValidMutationGroups(
    string inputUnderMutation)
    => TestUtil
      .GetAllValidMutationGroups<NumericConstantReplacer,
        LiteralExpressionSyntax>(inputUnderMutation);

  private static void ShouldNotHaveValidMutationGroup(string inputUnderMutation)
    => TestUtil
      .ShouldNotHaveValidMutationGroup<NumericConstantReplacer,
        LiteralExpressionSyntax>(inputUnderMutation);

  private static readonly IDictionary<string, string> NumericTypeSuffix
    = new Dictionary<string, string>
    {
      { "int", "" },
      { "uint", "U" },
      { "ulong", "UL" },
      { "long", "L" },
      { "float", "f" },
      { "double", "d" },
      { "decimal", "m" }
    };

  // Implicit conversion happens when literal is expressed without suffix, which
  // has int type by default.
  // We omit sbyte, byte types, as C# does not support initialisation
  // of literals of these types.
  [Theory]
  [InlineData("int", "int x = -42;")]
  [InlineData("int", "Int32 x = -42;")]
  [InlineData("float", "float x = 1.234f;")]
  [InlineData("float", "Single x = 1.234f;")]
  [InlineData("double", "double x = 23.44d;")]
  [InlineData("double", "Double x = 23.12222d;")]
  [InlineData("decimal", "decimal d = 232.232m;")]
  [InlineData("decimal", "Decimal d = 23232.2323232m;")]
  public void ShouldReplaceForNumericSignedConstants(string numericType,
    string constructUnderMutation)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{constructUnderMutation}}
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetValidMutationGroup(inputUnderMutation);
    mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo([numericType]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo(numericType);
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(["-{0}", "0", "{0} - 1", "{0} + 1"]);
  }

  // We omit ushort types, as C# does not support initialisation
  // of literals of these types.
  [Theory]
  [InlineData("uint", "uint x = 42u;")]
  [InlineData("uint", "UInt32 x = 42u;")]
  [InlineData("ulong", "ulong x = 42ul;")]
  [InlineData("ulong", "UInt64 x = 42ul;")]
  [InlineData("uint",  "uint x = 42;")]
  [InlineData("ulong", "ulong x = 42;")]
  public void ShouldReplaceForNumericUnsignedConstantsExceptNegativeOfItself(
    string returnType, string constructUnderMutation)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{constructUnderMutation}}
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetValidMutationGroup(inputUnderMutation);
    mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo([returnType]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo(returnType);
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(["0", "{0} - 1", "{0} + 1"]);
  }
  
  [Theory]
  // C# treats the negative expr and the const expr in -(constants) as two entities
  [InlineData("uint", uint.MinValue)]
  [InlineData("ulong", ulong.MinValue)]
  [InlineData("uint", uint.MaxValue)]
  [InlineData("ulong", ulong.MaxValue)]
  public void ShouldReplaceForUnsignedIntegerExceptNegativeOfItselfAtBoundaries(
    string numericType, dynamic value)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{numericType}} x = {{value}}{{NumericTypeSuffix[numericType]}};
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetValidMutationGroup(inputUnderMutation);
    mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo([numericType]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo(numericType);
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(["0", "{0} - 1", "{0} + 1"]);
  }
  
  [Theory]
  // C# treats the negative expr and the const expr in -(constants) as two entities
  // in this case, the constant is treated as an unsigned int because the absolute
  // value of int.MinValue is outside the int range
  // This applies similarly to long
  // [InlineData("int", int.MinValue)] 
  // [InlineData("long", long.MinValue)]
  [InlineData("float", float.MinValue)]
  [InlineData("float", float.MaxValue)]
  [InlineData("double", double.MinValue)]
  [InlineData("double", double.MaxValue)]
  // [InlineData("decimal", decimal.MinValue)]
  // [InlineData("decimal", decimal.MaxValue)]
    public void ShouldReplaceForFloatingPointAtBoundaries(
    string numericType, dynamic value)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{numericType}} x = {{value}}{{NumericTypeSuffix[numericType]}};
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetValidMutationGroup(inputUnderMutation);
    mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo([numericType]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo(numericType);
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(["0", "-{0}", "{0} - 1", "{0} + 1"]);
  }
  
  // C# does not treat the decimal.MinValue / decimal.MaxValue as a constant expression
  // https://codeblog.jonskeet.uk/tag/csharp-2/
  [Fact]
  public void ShouldReplaceForDecimalAtBoundaries()
  {
    string[] inputsUnderMutation =
    [
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            decimal x = {{decimal.MinValue}}m;
          }
        }
        """,
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            decimal x = {{decimal.MaxValue}}m;
          }
        }
        """
    ];

    foreach (var input in inputsUnderMutation)
    {
      var mutationGroup = GetValidMutationGroup(input);
      mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo(["decimal"]);
      mutationGroup.SchemaReturnType.Should().BeEquivalentTo("decimal");
      mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
        .BeEquivalentTo("{0}");
      TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
        .BeEquivalentTo(["0", "-{0}", "{0} - 1", "{0} + 1"]);
    }
  }

  [Theory]
  [InlineData("var b = 'a';")]
  [InlineData("var b1 = true != false; var b2 = b1;")]
  [InlineData("bool a = true;")]
  [InlineData("var y = \"abc\";")]
  public void ShouldNotReplaceForNonNumericConstants(
    string constructUnderMutation)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{constructUnderMutation}}
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);
    ShouldNotHaveValidMutationGroup(inputUnderMutation);
  }

  [Theory]
  [InlineData("var x = 11 == 11ul ? 12 : 13;", 4)]
  [InlineData("var y = false ? 2.2f : 4.4f;", 2)]
  public void ShouldReplaceMultipleNumericConstants(
    string constructUnderMutation, int numericCount)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{constructUnderMutation}}
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroups = GetAllValidMutationGroups(inputUnderMutation);

    mutationGroups.Length.Should().Be(numericCount);
  }

  [Theory]
  [InlineData("uint")]
  [InlineData("int")]
  [InlineData("ulong")]
  [InlineData("long")]
  public void ShouldReplaceBasedOnReturnType(string type)
  {
    var inputUnderMutation = 
      $$"""
        using System;
        
        public class A
        {
          public static void Main()
          {
            {{type}} x = 10;
          }
        }
        """;
    
    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetValidMutationGroup(inputUnderMutation);
    mutationGroup.SchemaReturnType.Should().Be(type);
    mutationGroup.SchemaParameterTypes.Should().Equal(type);
  }

  [Fact]
  public void ShouldAlwaysAssignNumericLiteralANumericType()
  {
    // Example encountered in the wild.
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = string.Format("{0}", 0);
        }
      }
      """;
    
    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroups = GetAllValidMutationGroups(inputUnderMutation);
    mutationGroups.Length.Should().Be(1);
    
    mutationGroups[0].SchemaReturnType.Should().Be("object");
    mutationGroups[0].SchemaParameterTypes.Should().Equal("int");
  }
}