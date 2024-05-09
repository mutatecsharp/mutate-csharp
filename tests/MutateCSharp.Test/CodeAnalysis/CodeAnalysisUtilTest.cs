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
}