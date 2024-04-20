// using FluentAssertions;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using MutateCSharp.Mutation;
// using MutateCSharp.Mutation.OperatorImplementation;
// using Xunit.Abstractions;
//
// namespace MutateCSharp.Test;
//
// public class MutantSchemataGeneratorTest
// {
//   private readonly ITestOutputHelper _testOutputHelper;
//
//   public MutantSchemataGeneratorTest(ITestOutputHelper testOutputHelper)
//   {
//     _testOutputHelper = testOutputHelper;
//   }
//
//   private static SemanticModel GetSemanticModel(SyntaxTree inputAst)
//   {
//     var compilation = TestUtil.GetAstCompilation(inputAst);
//     var model = compilation.GetSemanticModel(inputAst);
//     model.Should().NotBeNull();
//     return model;
//   }
//
//   private void GeneratedSchemaShouldMatch(MutationGroup mutationGroup,
//     string expectedMethod)
//   {
//     var output =
//       MutantSchemataGenerator.GenerateIndividualSchema(mutationGroup);
//     var expectedAst = CSharpSyntaxTree.ParseText(expectedMethod);
//     var outputAst = CSharpSyntaxTree.ParseText(output.ToString());
//     _testOutputHelper.WriteLine(outputAst.ToString());
//     outputAst.IsEquivalentTo(expectedAst).Should().BeTrue();
//   }
//
//   [Theory]
//   [InlineData("true")]
//   [InlineData("false")]
//   [InlineData("bool b = true;")]
//   
//   public void TestMutateBooleanConstant_GeneratedMethodContainsAllPossibleCases(string inputUnderMutation)
//   {
//     const string expectedMethod =
//       """
//       public static bool ReplaceBooleanConstant1(int mutantId, bool argument1)
//       {
//         if (!ActivatedInRange(mutantId, mutantId + 0)) return argument1;
//         if (_activatedMutantId == mutantId + 0) return !argument1;
//         throw new System.Diagnostics.UnreachableException("Mutant ID out of range");
//       }
//       """;
//
//     var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
//     var model = GetSemanticModel(inputAst);
//     var mutationOperator = new BooleanConstantReplacer(model);
//     var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
//       .OfType<LiteralExpressionSyntax>().FirstOrDefault();
//
//     constructUnderTest.Should().NotBeNull();
//
//     var mutationGroup =
//       mutationOperator.CreateMutationGroup(constructUnderTest);
//     mutationGroup.Should().NotBeNull();
//     
//     GeneratedSchemaShouldMatch(mutationGroup!, expectedMethod);
//   }
//
//   [Theory]
//   [InlineData("1 + 2")]
//   [InlineData("1")]
//   [InlineData("bool b = a;")]
//   public void TestMutateInvalidBooleanConstant_DoesNotGenerateMethod(string inputUnderMutation)
//   {
//     var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
//     var model = GetSemanticModel(inputAst);
//     var mutationOperator = new BooleanConstantReplacer(model);
//     var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
//       .OfType<LiteralExpressionSyntax>().FirstOrDefault();
//
//     constructUnderTest.Should().BeNull();
//     
//     var mutationGroup =
//       mutationOperator.CreateMutationGroup(constructUnderTest);
//     mutationGroup.Should().BeNull();
//   }
//   
//   [Theory]
//   [InlineData("\"abc\"")]
//   [InlineData("\"42\"")]
//   [InlineData("string a = \"abc\"")]
//   public void TestMutateStringConstant_GeneratedMethodContainsAllPossibleCases(string inputUnderMutation)
//   {
//     const string expectedMethod =
//       """
//       public static string ReplaceStringConstant1(int mutantId, string argument1)
//       {
//         if (!ActivatedInRange(mutantId, mutantId + 0)) return argument1;
//         if (_activatedMutantId == mutantId + 0) return string.Empty;
//         throw new System.Diagnostics.UnreachableException("Mutant ID out of range");
//       }
//       """;
//
//     var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
//     var model = GetSemanticModel(inputAst);
//     var mutationOperator = new StringConstantReplacer(model);
//     var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
//       .OfType<LiteralExpressionSyntax>().FirstOrDefault();
//
//     constructUnderTest.Should().NotBeNull();
//
//     var mutationGroup =
//       mutationOperator.CreateMutationGroup(constructUnderTest);
//     mutationGroup.Should().NotBeNull();
//     
//     GeneratedSchemaShouldMatch(mutationGroup!, expectedMethod);
//   }
//   
//   [Theory]
//   [InlineData("42")]
//   [InlineData("bool a = true;")]
//   [InlineData("string s = s1;")]
//   public void TestMutateInvalidStringConstant_DoesNotGenerateMethod(string inputUnderMutation)
//   {
//     var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
//     var model = GetSemanticModel(inputAst);
//     var mutationOperator = new StringConstantReplacer(model);
//     var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
//       .OfType<LiteralExpressionSyntax>().FirstOrDefault();
//
//     constructUnderTest.Should().BeNull();
//     
//     var mutationGroup =
//       mutationOperator.CreateMutationGroup(constructUnderTest);
//     mutationGroup.Should().BeNull();
//   }
//   
//   // Implicit conversion happens when literal is expressed without suffix, which
//   // has int type by default.
//   // C# does not support sbyte, short, byte, ushort literals.
//   [Theory]
//   [InlineData("Int32", "int x = -42;")]
//   [InlineData("Int64", "long x = 2323232L;")]
//   [InlineData("Single", "float x = 1.234f;")]
//   [InlineData("Double", "double x = 23.45678d;")]
//   [InlineData("Decimal", "decimal x = 12.123213M")]
//   public void TestMutateNumericConstant_GeneratedMethodContainsAllPossibleCases(string numericType, string inputUnderMutation)
//   {
//     // All cases 1, 2, 3, 4, 5
//     var expectedMethod =
//       $$"""
//         public static {{numericType}} Replace{{numericType}}Constant12345(int mutantId, {{numericType}} argument1)
//         {
//           if (!ActivatedInRange(mutantId, mutantId + 4)) return argument1;
//           if (_activatedMutantId == mutantId + 0) return 0;
//           if (_activatedMutantId == mutantId + 1) return -argument1;
//           if (_activatedMutantId == mutantId + 2) return -1;
//           if (_activatedMutantId == mutantId + 3) return argument1 - 1;
//           if (_activatedMutantId == mutantId + 4) return argument1 + 1;
//           throw new System.Diagnostics.UnreachableException("Mutant ID out of range");
//         }
//         """;
//
//     _testOutputHelper.WriteLine(expectedMethod);
//     var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
//     var model = GetSemanticModel(inputAst);
//     var mutationOperator = new NumericConstantReplacer(model);
//     var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
//       .OfType<LiteralExpressionSyntax>().FirstOrDefault();
//
//     // There should be at least one construct that matches the type under test
//     constructUnderTest.Should().NotBeNull();
//     
//     // The literal expression should contain token type specified by the test case
//     var name = model.GetConversion(constructUnderTest!).GetType().ToString();
//     
//     _testOutputHelper.WriteLine(name);
//
//     var mutationGroup =
//       mutationOperator.CreateMutationGroup(constructUnderTest);
//     mutationGroup.Should().NotBeNull();
//     
//     GeneratedSchemaShouldMatch(mutationGroup!, expectedMethod);
//   }
//   
//   [Theory]
//   [InlineData("UInt32", "uint x = 42U;")]
//   public void TestMutateUIntConstant_GeneratedMethodShouldContainSomePossibleCases(string numericType, string inputUnderMutation)
//   {
//     // Cases 1, 2, 4, 5 only
//     // Case 3 not included since signed value cannot be cast as unsigned 
//     var expectedMethod =
//       $$"""
//         public static {{numericType}} Replace{{numericType}}Constant1245(int mutantId, {{numericType}} argument1)
//         {
//           if (!ActivatedInRange(mutantId, mutantId + 3)) return argument1;
//           if (_activatedMutantId == mutantId + 0) return 0;
//           if (_activatedMutantId == mutantId + 1) return -argument1;
//           if (_activatedMutantId == mutantId + 2) return argument1 - 1;
//           if (_activatedMutantId == mutantId + 3) return argument1 + 1;
//           throw new System.Diagnostics.UnreachableException("Mutant ID out of range");
//         }
//         """;
//
//     _testOutputHelper.WriteLine(expectedMethod);
//     var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
//     var model = GetSemanticModel(inputAst);
//     var mutationOperator = new NumericConstantReplacer(model);
//     var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
//       .OfType<LiteralExpressionSyntax>().FirstOrDefault();
//
//     // There should be at least one construct that matches the type under test
//     constructUnderTest.Should().NotBeNull();
//     
//     // The literal expression should contain token type specified by the test case
//     var name = model.GetConversion(constructUnderTest!).GetType().ToString();
//     
//     _testOutputHelper.WriteLine(name);
//
//     var mutationGroup =
//       mutationOperator.CreateMutationGroup(constructUnderTest);
//     mutationGroup.Should().NotBeNull();
//     
//     GeneratedSchemaShouldMatch(mutationGroup!, expectedMethod);
//   }
//   
//   [Theory]
//   [InlineData("UInt64", "ulong x = 323232UL;")]
//   public void TestMutateULongConstant_GeneratedMethodShouldContainSomePossibleCases(string numericType, string inputUnderMutation)
//   {
//     // Cases 1, 4, 5 only
//     // Case 2 not included as unary - operator cannot apply to ulong
//     // Case 3 not included since signed value cannot be cast as unsigned 
//     var expectedMethod =
//       $$"""
//         public static {{numericType}} Replace{{numericType}}Constant145(int mutantId, {{numericType}} argument1)
//         {
//           if (!ActivatedInRange(mutantId, mutantId + 2)) return argument1;
//           if (_activatedMutantId == mutantId + 0) return 0;
//           if (_activatedMutantId == mutantId + 1) return argument1 - 1;
//           if (_activatedMutantId == mutantId + 2) return argument1 + 1;
//           throw new System.Diagnostics.UnreachableException("Mutant ID out of range");
//         }
//         """;
//
//     _testOutputHelper.WriteLine(expectedMethod);
//     var inputAst = CSharpSyntaxTree.ParseText(inputUnderMutation);
//     var model = GetSemanticModel(inputAst);
//     var mutationOperator = new NumericConstantReplacer(model);
//     var constructUnderTest = inputAst.GetCompilationUnitRoot().DescendantNodes()
//       .OfType<LiteralExpressionSyntax>().FirstOrDefault();
//
//     // There should be at least one construct that matches the type under test
//     constructUnderTest.Should().NotBeNull();
//     
//     // The literal expression should contain token type specified by the test case
//     var name = model.GetConversion(constructUnderTest!).GetType().ToString();
//     
//     _testOutputHelper.WriteLine(name);
//
//     var mutationGroup =
//       mutationOperator.CreateMutationGroup(constructUnderTest);
//     mutationGroup.Should().NotBeNull();
//     
//     GeneratedSchemaShouldMatch(mutationGroup!, expectedMethod);
//   }
// }