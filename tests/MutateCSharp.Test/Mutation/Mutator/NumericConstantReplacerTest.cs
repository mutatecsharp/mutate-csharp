using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation;
using MutateCSharp.Mutation.Mutator;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Mutator;

public class NumericConstantReplacerTest(ITestOutputHelper testOutputHelper)
{
  private static MutationGroup GetValidMutationGroup(string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<NumericConstantReplacer, LiteralExpressionSyntax>(
        inputUnderMutation);

  private static MutationGroup GetNegativeLiteralValidMutationGroup(
    string inputUnderMutation)
    => TestUtil
      .GetValidMutationGroup<NumericConstantReplacer,
        PrefixUnaryExpressionSyntax>(inputUnderMutation);

  private static MutationGroup[] GetAllValidMutationGroups(
    string inputUnderMutation)
    => TestUtil
      .GetAllValidMutationGroups<NumericConstantReplacer,
        LiteralExpressionSyntax>(inputUnderMutation);

  private static MutationGroup[] GetAllNegativeLiteralMutationGroups(
    string inputUnderMutation)
    => TestUtil
      .GetAllValidMutationGroups<NumericConstantReplacer,
        PrefixUnaryExpressionSyntax>(inputUnderMutation); 

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
  [InlineData("byte", "byte x = 12;")]
  [InlineData("short", "short x = 12;")]
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
  [InlineData("float", float.MaxValue)]
  [InlineData("double", double.MaxValue)]
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
  
  [Theory]
  // C# treats the negative expr and the const expr in -(constants) as two entities
  // in this case, the constant is treated as an unsigned int because the absolute
  // value of int.MinValue is outside the int range
  // This applies similarly to long -> ulong
  [InlineData("int", "-0")]
  [InlineData("int", "-1")]
  [InlineData("int", "-2147483648")]  
  [InlineData("long", "-1L")]
  [InlineData("long", "-2147483649")]
  [InlineData("long", "-9223372036854775808")]
  [InlineData("float", "-1.1f")]
  [InlineData("float", "-3.40282347E+38f")]
  [InlineData("double", "-1.1")]
  [InlineData("double", "-1.1d")]
  [InlineData("double", "-1.7976931348623157E+308")]
  [InlineData("double", "-1.7976931348623157E+308d")]
  [InlineData("decimal", "-1.1m")]
  public void ShouldReplaceForNegativeConstants(
    string numericType, string value)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            {{numericType}} x = {{value}};
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetNegativeLiteralValidMutationGroup(inputUnderMutation);
    mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo([numericType]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo(numericType);
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(["0", "-{0}", "{0} - 1", "{0} + 1"]);
  }
  
  [Theory]
  // C# treats the negative expr and the const expr in -(constants) as two entities
  // in this case, the constant is treated as an unsigned int because the absolute
  // value of int.MinValue is outside the int range
  // This applies similarly to long -> ulong
  [InlineData("int", "-2147483648")]
  [InlineData("long", "-2147483649")]
  public void ShouldReplaceForNegativeConstantsForInferredType(
    string numericType, string value)
  {
    var inputUnderMutation =
      $$"""
        using System;

        public class A
        {
          public static void Main()
          {
            var x = {{value}};
          }
        }
        """;

    testOutputHelper.WriteLine(inputUnderMutation);

    var mutationGroup = GetNegativeLiteralValidMutationGroup(inputUnderMutation);
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
    // string.Format takes in object types.
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
    
    mutationGroups[0].SchemaReturnType.Should().Be("int");
    mutationGroups[0].SchemaParameterTypes.Should().Equal("int");
  }
  
  [Theory]
  [InlineData("0xFF")]
  [InlineData("0b1010")]
  [InlineData("0123")]
  [InlineData("0xFF_AA")]
  [InlineData("1_000")]
  [InlineData("0b1010_1100")]
  public void CanImplicitlyConvertVariousFormattedNumericLiteral(string value)
  {
    var inputUnderMutation =
      $$"""
        using System;

        class A
        {
          public static void Main()
          {
            ulong x = {{value}};
          }
        }
        """;
    
    var mutationGroup = GetValidMutationGroup(inputUnderMutation);
    mutationGroup.SchemaParameterTypes.Should().BeEquivalentTo(["ulong"]);
    mutationGroup.SchemaReturnType.Should().BeEquivalentTo("ulong");
    mutationGroup.SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup).Should()
      .BeEquivalentTo(["0", "{0} - 1", "{0} + 1"]);
  }
  
  [Theory]
  [InlineData("-0xF")]
  [InlineData("-0b1010")]
  [InlineData("-0123")]
  [InlineData("-0x0_A")]
  [InlineData("-1_00")]
  [InlineData("-0b1010_1")]
  public void CanImplicitlyConvertVariousFormattedNegativeNumericLiteral(string value)
  {
    var inputUnderMutation =
      $$"""
        using System;

        class A
        {
          public static void Main()
          {
            short x = {{value}};
            sbyte y = {{value}};
          }
        }
        """;
    
    var mutationGroup = GetAllNegativeLiteralMutationGroups(inputUnderMutation);
    mutationGroup[0].SchemaParameterTypes.Should().BeEquivalentTo(["short"]);
    mutationGroup[0].SchemaReturnType.Should().BeEquivalentTo("short");
    mutationGroup[0].SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup[0]).Should()
      .BeEquivalentTo(["0", "{0} - 1", "{0} + 1"]);
    
    mutationGroup[1].SchemaParameterTypes.Should().BeEquivalentTo(["sbyte"]);
    mutationGroup[1].SchemaReturnType.Should().BeEquivalentTo("sbyte");
    mutationGroup[1].SchemaOriginalExpression.ExpressionTemplate.Should()
      .BeEquivalentTo("{0}");
    TestUtil.GetMutantExpressionTemplates(mutationGroup[1]).Should()
      .BeEquivalentTo(["0", "{0} - 1", "{0} + 1"]);
  }
}