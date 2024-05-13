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
}