using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Mutator;
using MutateCSharp.Util;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation.Mutator;

public class MutatorIntegrationTest(ITestOutputHelper testOutputHelper)
{
  [Fact]
  public void
    ReturnTypeOfMutatedChildNodeShouldBeAssignableToParamTypeOfMutatedNode()
  {
    // Example encountered in the wild.
    var inputUnderMutation =
      """
      using System;

      public class A
      {
        public static void Main()
        {
          var x = new System.Numerics.BigInteger();
          var y = x == 1;
        }
      }
      """;

    var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var compilation = TestUtil.GetAstSemanticModelAndAssembly(inputAst);

    var binExprReplacer =
      new BinExprOpReplacer(compilation.sutAssembly, compilation.model, 
        compilation.model.BuildBinaryNumericOperatorMethodSignature());
    var numericLitReplacer =
      new NumericConstantReplacer(compilation.sutAssembly, compilation.model);
    
    var binExpr = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<BinaryExpressionSyntax>().First();
    var litExpr = inputAst.GetCompilationUnitRoot().DescendantNodes()
      .OfType<LiteralExpressionSyntax>().First();

    var binaryExprMutationGroup = 
      binExprReplacer.CreateMutationGroup(binExpr, default);
    var litExprMutationGroup =
      numericLitReplacer.CreateMutationGroup(litExpr, default);
    
    var literalReturnType = litExprMutationGroup.ReturnTypeSymbol;
    var literalParamType = binaryExprMutationGroup.ParameterTypeSymbols[1];
    
    testOutputHelper.WriteLine(literalParamType.ToDisplayString());
    testOutputHelper.WriteLine(literalReturnType.ToDisplayString());

    compilation.model.Compilation
      .HasImplicitConversion(literalReturnType, literalParamType).Should()
      .BeTrue();
  }
}