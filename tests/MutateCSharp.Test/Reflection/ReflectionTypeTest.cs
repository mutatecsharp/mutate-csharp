using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Reflection;

public class ReflectionTypeTest(ITestOutputHelper testOutputHelper)
{
  [Theory]
  // Primitive types
  [InlineData(typeof(int), "10")]
  // Generic types
  [InlineData(typeof(Dictionary<int, int>), "new Dictionary<int, int>()")]
  // Nested generic types
  [InlineData(typeof(List<Tuple<int, string, bool>>), "new List<Tuple<int, string, bool>>()")]
  // Array types
  [InlineData(typeof(Type[]), "new Type[] {};")]
  // Nested array types
  [InlineData(typeof(int[][]), "new int[][] {}")]
  public void ShouldBeAbleToConstructRuntimeTypeFromCompilationTypeSymbol(Type runtimeType, string typeInstantiationExpression)
  {
    var inputUnderMutation =
      $$"""
      using System;
      using System.Collections.Generic;

      public class A
      {
        public static void Main()
        {
          var x = {{typeInstantiationExpression}};
        }
      }
      """;

    var ast = CSharpSyntaxTree.ParseText(inputUnderMutation);
    var compilation = TestUtil.GetAstSemanticModelAndAssembly(ast);
    var construct = ast.GetCompilationUnitRoot().DescendantNodes()
      .OfType<VariableDeclarationSyntax>().First();
    var variable = construct.Variables.First().Initializer!.Value;
    var genericCompileType = compilation.model.ResolveTypeSymbol(variable);
    var resolvedRuntimeType =
      genericCompileType?.GetRuntimeType(compilation.sutAssembly);

    runtimeType.Should().Be(resolvedRuntimeType);
  }
}