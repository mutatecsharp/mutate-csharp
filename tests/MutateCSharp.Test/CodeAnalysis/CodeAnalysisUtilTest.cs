using System.Globalization;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Xunit.Abstractions;

namespace MutateCSharp.Test.CodeAnalysis;

public class CodeAnalysisUtilTest(ITestOutputHelper testOutputHelper)
{
  [Fact]
  public void RefOperandsShouldNotBeDelegates()
  {
    var inputUnderMutation =
      """
      using System;
      
      public class A
      {
        public static bool foo(ref bool b)
        { 
          var a = new A();
          return a is A && !b;
        }
      
        public static void Main()
        {
        }
      }
      """;

    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var returnStat = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<ReturnStatementSyntax>().First();
    var binExpr = (BinaryExpressionSyntax)returnStat.Expression;

    var compilation = TestUtil.GetAstSemanticModelAndAssembly(ast);
    compilation.model.NodeCanBeDelegate(binExpr).Should().BeFalse();
  }

  [Fact]
  public void AddingNullableAnnotationShouldMakePrimitiveTypesNullable()
  {
    var inputUnderMutation =
      """
      using System;
      
      public class A
      {
        public static bool? foo(bool? b) 
        {
          return b;
        }
      
        public static void Main()
        {
        }
      }
      """;
    
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var compilation = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var returnStat = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<ReturnStatementSyntax>().First();

    var compilationNullableBoolType =
      compilation.model.GetTypeInfo(returnStat.Expression).Type;
    
    var nullableType = compilation.model.Compilation.GetTypeByMetadataName("System.Nullable`1");

    var customNullableBoolType =
      compilation.model.Compilation.GetSpecialType(SpecialType.System_Boolean);
    customNullableBoolType = nullableType.Construct(customNullableBoolType);
    
    testOutputHelper.WriteLine(customNullableBoolType.ToDisplayString());

    compilation.model.Compilation
      .HasImplicitConversion(customNullableBoolType,
        compilationNullableBoolType).Should().BeTrue();
  }

  [Theory]
  // [InlineData(SpecialType.System_Char)]
  [InlineData(SpecialType.System_Byte)]
  [InlineData(SpecialType.System_SByte)]
  [InlineData(SpecialType.System_UInt16)]
  [InlineData(SpecialType.System_Int16)]
  [InlineData(SpecialType.System_UInt32)]
  [InlineData(SpecialType.System_Int32)]
  [InlineData(SpecialType.System_UInt64)]
  [InlineData(SpecialType.System_Int64)]
  [InlineData(SpecialType.System_Single)]
  [InlineData(SpecialType.System_Double)]
  [InlineData(SpecialType.System_Decimal)]
  public void VerifyNumericLiteralCanBeImplicitlyConvertedToTargetType(
   SpecialType type)
  {
    var inputUnderMutation =
      """
        using System;

        public class A
        {
          public static void Main()
          {
            var x = 1;
          }
        }
        """;
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var literal = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<LiteralExpressionSyntax>().First();
    comp.model.CanImplicitlyConvertNumericLiteral(literal, type).Should()
      .BeTrue();
  }

  [Fact]
  public void CheckForLambdaReturnVoidType()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void ReturnVoid() {}
        public static int ReturnNonVoid() => 1;
        public static void Main()
        {
          var x = () => { ReturnVoid(); };
          var y = () => { return ReturnNonVoid(); };
        }
      }
      """;
    
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var lambdas = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<ParenthesizedLambdaExpressionSyntax>().ToArray();
    
    var voidReturningLambdaSymbol = (IMethodSymbol) comp.model.GetSymbolInfo(lambdas[0]).Symbol!;
    var nonVoidReturningLambdaSymbol = (IMethodSymbol) comp.model.GetSymbolInfo(lambdas[1]).Symbol!;

    voidReturningLambdaSymbol.ReturnsVoid.Should().BeTrue();
    nonVoidReturningLambdaSymbol.ReturnsVoid.Should().BeFalse();
  }
  
  [Fact]
  public void CheckForMethodDeclarationReturnVoidType()
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void ReturnVoid() {}
        public static int ReturnNonVoid() => 1;
        public static void Main()
        {
          var x = () => { ReturnVoid(); };
          var y = () => { return ReturnNonVoid(); };
        }
      }
      """;
    
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var methodDecl = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<MethodDeclarationSyntax>().ToArray();
    
    testOutputHelper.WriteLine(methodDecl[0].ReturnType.ToString());

    var predefinedVoid = methodDecl[0].ReturnType as PredefinedTypeSyntax;
    predefinedVoid.Should().NotBeNull();
    predefinedVoid.Keyword.IsKind(SyntaxKind.VoidKeyword).Should().BeTrue();

    if (methodDecl[1].ReturnType is PredefinedTypeSyntax notVoid)
    {
      notVoid.Keyword.IsKind(SyntaxKind.VoidKeyword).Should().BeFalse();
    }
  }
  
  [Theory]
  [InlineData("AB")]
  [InlineData("AB.C")]
  [InlineData("AB.C.D")]
  public void GetQualifiedNamespaceShouldSucceedForNamespaceDeclarationSyntax(string namespaceName)
  {
    var inputUnderMutation =
      $$"""
        using System;

        namespace {{namespaceName}}
        {
          class A
          {
            public static void Main()
            {
            }
          }
        }
        """;

    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var actualNamespaceName = ast.GetCompilationUnitRoot().GetNamespaceName();
    testOutputHelper.WriteLine(actualNamespaceName);
    actualNamespaceName.Should().Be(namespaceName);
  }
  
  [Theory]
  [InlineData("AB")]
  [InlineData("AB.C")]
  [InlineData("AB.C.D")]
  public void GetQualifiedNamespaceShouldSucceedForFileScopedNamespaceDeclarationSyntax(string namespaceName)
  {
    var inputUnderMutation =
      $$"""
        using System;
        
        namespace {{namespaceName}};

        public class A
        {
          public static void Main()
          {
          }
        }
        """;

    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var actualNamespaceName = ast.GetCompilationUnitRoot().GetNamespaceName();
    testOutputHelper.WriteLine(actualNamespaceName);
    actualNamespaceName.Should().Be(namespaceName);
  }

  // The test verifies the determined type is not what is expected.
  [Theory]
  [InlineData("-0", "int", "int", "int", "int")]
  [InlineData("-1", "int", "int", "int", "int")]
  [InlineData("-1L", "long", "long", "long", "long")]
  [InlineData("-2147483648", "int", "int", "uint", "uint")]  
  [InlineData("-2147483649", "long", "long", "uint", "long")]
  [InlineData("-9223372036854775808", "long", "long", "ulong", "ulong")]
  public void CheckNegativeTypeDeterminedByRoslyn(
    string value, string expectedDefinedType, string expectedConvertedType,
    string expectedLiteralDefinedType, string expectedLiteralConvertedType)
  {
    var inputUnderMutation =
      $$"""
        using System;

        class A
        {
          public static void Main()
          {
            var x = {{value}};
          }
        }
        """;
    
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var construct = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<PrefixUnaryExpressionSyntax>().First();
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    testOutputHelper.WriteLine(construct.ToString());

    var definedType = comp.model.GetTypeInfo(construct).Type!.ToString();
    var convertedType = comp.model.GetTypeInfo(construct).ConvertedType!.ToString();

    var literalDefinedType = comp.model.GetTypeInfo(construct.Operand).Type!.ToString();
    var literalConvertedType =
      comp.model.GetTypeInfo(construct.Operand).ConvertedType!.ToString();
    
    testOutputHelper.WriteLine(definedType);
    testOutputHelper.WriteLine(convertedType);
    testOutputHelper.WriteLine($"literal: {literalDefinedType}");
    testOutputHelper.WriteLine($"literal converted: {literalConvertedType}");
    
    // Determined type for the negative literal: int or long
    definedType.Should().Be(expectedDefinedType);
    convertedType.Should().Be(expectedConvertedType);
    
    // Determined type for the positive portion of the literal: uint, ulong
    literalDefinedType.Should().Be(expectedLiteralDefinedType);
    literalConvertedType.Should().Be(expectedLiteralConvertedType);
  }
  
  [Theory]
  [InlineData(SpecialType.System_Char, false)]
  [InlineData(SpecialType.System_Byte, false)]
  [InlineData(SpecialType.System_SByte, true)]
  [InlineData(SpecialType.System_UInt16, false)]
  [InlineData(SpecialType.System_Int16, true)]
  [InlineData(SpecialType.System_UInt32, false)]
  [InlineData(SpecialType.System_Int32, true)]
  [InlineData(SpecialType.System_UInt64, false)]
  [InlineData(SpecialType.System_Int64, true)]
  [InlineData(SpecialType.System_Single, true)]
  [InlineData(SpecialType.System_Double, true)]
  [InlineData(SpecialType.System_Decimal, true)]
  public void VerifyNegativeNumericLiteralCanBeImplicitlyConvertedToTargetType(
    SpecialType type, bool possible)
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = -1;
        }
      }
      """;
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var literal = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<PrefixUnaryExpressionSyntax>().First();
    comp.model.CanImplicitlyConvertNumericLiteral(literal, type).Should()
      .Be(possible);
  }
  
  [Theory]
  [InlineData(SpecialType.System_Char)]
  [InlineData(SpecialType.System_Byte)]
  [InlineData(SpecialType.System_SByte)]
  [InlineData(SpecialType.System_UInt16)]
  [InlineData(SpecialType.System_Int16)]
  [InlineData(SpecialType.System_UInt32)]
  [InlineData(SpecialType.System_Int32)]
  [InlineData(SpecialType.System_UInt64)]
  // The following is possible:
  // [InlineData(SpecialType.System_Int64)]
  // [InlineData(SpecialType.System_Single)]
  // [InlineData(SpecialType.System_Double)]
  // [InlineData(SpecialType.System_Decimal)]
  public void SuffixedNegativeNumericConstantCannotBeImplicitlyConvertedToNarrowerTypes(
    SpecialType type)
  {
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = -1L;
        }
      }
      """;
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var literal = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<PrefixUnaryExpressionSyntax>().First();
    comp.model.CanImplicitlyConvertNumericLiteral(literal, type).Should()
      .BeFalse();
  }
  
  [Theory]
  [InlineData(SpecialType.System_UInt32)]
  [InlineData(SpecialType.System_UInt64)]
  [InlineData(SpecialType.System_Single)]
  [InlineData(SpecialType.System_Double)]
  [InlineData(SpecialType.System_Decimal)]
  public void SuffixedUnsignedConstantCanBeImplicitlyConverted(
    SpecialType type)
  {
    const string inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = 1u;
        }
      }
      """;
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var literal = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<LiteralExpressionSyntax>().First();
    comp.model.CanImplicitlyConvertNumericLiteral(literal, type).Should()
      .BeTrue();
  }

  [Theory]
  [InlineData("0xFF")]
  [InlineData("0b1010")]
  [InlineData("0123")]
  [InlineData("0xFF_A")]
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
            var x = {{value}};
          }
        }
        """;
    
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var literal = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<LiteralExpressionSyntax>().First();
    
    testOutputHelper.WriteLine(literal.ToString());
    
    comp.model.CanImplicitlyConvertNumericLiteral(literal, SpecialType.System_Int16).Should()
      .BeTrue();
  }

  [Theory]
  [InlineData("int", "0")]
  [InlineData("ulong", "0UL")]
  [InlineData("uint", "0U")]
  [InlineData("long", "0L")]
  [InlineData("double", "0.0")]
  [InlineData("float", "0.0f")]
  [InlineData("double", "0.0d")]
  [InlineData("decimal", "0.0m")]
  [InlineData("uint", "1")]
  [InlineData("long", "1L")]
  [InlineData("ulong", "1UL")]
  [InlineData("double", "1.0")]
  [InlineData("double", "1.0d")]
  [InlineData("float", "1.0f")]
  [InlineData("decimal", "1.0m")]
  [InlineData("int", "-1")]
  [InlineData("long", "-1L")]
  [InlineData("double", "-1.0")]
  [InlineData("double", "-1.0d")]
  [InlineData("float", "-1.0f")]
  [InlineData("decimal", "-1.0m")]
  public void CheckParseValueWorks(string type, string numericValue)
  {
    var inputUnderMutation =
      $$"""
        using System;

        class A
        {
          public static void Main()
          {
            const {{type}} x = {{numericValue}};
            const {{type}} y = x;
            const {{type}} z = y;
            var a = z + y;
          }
        }
        """;
    
    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var comp = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var node = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<BinaryExpressionSyntax>().First();

    var value = comp.model.GetConstantValue(node.Left);
    value.HasValue.Should().BeTrue();
    
    var check = value.Value is 0 or 0U or 0L or 0UL or 0.0f or 0.0 or 0.0m 
      or -1 or -1L or -1.0f or -1.0 or -1.0m 
      or 1 or 1U or 1UL or 1L or 1.0f or 1.0 or 1.0m;
    check.Should().BeTrue();
  }
}