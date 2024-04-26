using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class NumericConstantReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(sutAssembly,
    semanticModel)
{
  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type?.SpecialType;
    return type.HasValue &&
           SupportedNumericTypesToSuffix.ContainsKey(type.Value);
  }

  protected override ExpressionRecord OriginalExpression(
    LiteralExpressionSyntax originalNode)
  {
    return new ExpressionRecord(originalNode.Kind(), "{0}");
  }

  protected override IList<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(
      LiteralExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type?.SpecialType!;
    var typeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(type.Value);
    var result = new List<(int, ExpressionRecord)>();

    // Mutation: value => 0
    result.Add((1,
      new ExpressionRecord(SyntaxKind.NumericLiteralExpression, "0")));
    // Mutation: value => -value
    // (Unary negative operator cannot be applied to unsigned or char types.)
    if (typeClassification is not CodeAnalysisUtil.SupportedType.UnsignedIntegral)
      result.Add((2,
        new ExpressionRecord(SyntaxKind.UnaryMinusExpression, "-{0}")));
    // Mutation: value => value - 1
    result.Add((3,
      new ExpressionRecord(SyntaxKind.SubtractExpression, "{0} - 1")));
    // Mutation: value => value + 1
    result.Add((4, new ExpressionRecord(SyntaxKind.AddExpression, "{0} + 1")));
    return result.ToImmutableArray();
  }

  protected override IList<string> ParameterTypes(
    LiteralExpressionSyntax originalNode)
  {
    return ImmutableArray.Create(ReturnType(originalNode));
  }

  protected override string ReturnType(LiteralExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
  }

  protected override string
    SchemaBaseName(LiteralExpressionSyntax originalNode)
  {
    return $"ReplaceNumericConstantReturn{ReturnType(originalNode)}";
  }
}

/*
 * Supported numeric types.
 *
 * More on supported C# value types (signed integral/unsigned integral/char):
 * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-types
 */
public sealed partial class NumericConstantReplacer
{
  // C# does not support specifying short, ushort, byte, and sbyte literals
  // These types have to be obtained through casting / explicit conversion / assignment
  private static readonly FrozenDictionary<SpecialType, string>
    SupportedNumericTypesToSuffix =
      new Dictionary<SpecialType, string>
      {
        // Signed numeric types
        { SpecialType.System_Int32, "" },
        { SpecialType.System_Int64, "L" },
        // Unsigned numeric types
        { SpecialType.System_UInt32, "U" },
        { SpecialType.System_UInt64, "UL" },
        // Floating point types
        { SpecialType.System_Single, "f" },
        { SpecialType.System_Double, "d" },
        { SpecialType.System_Decimal, "m" }
      }.ToFrozenDictionary();
}