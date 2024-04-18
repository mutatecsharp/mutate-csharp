using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class NumericConstantReplacer(SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(semanticModel)
{
  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type?.SpecialType;
    return type.HasValue && SupportedNumericTypesToSuffix.ContainsKey(type.Value);
  }

  protected override string OriginalExpressionTemplate(
    LiteralExpressionSyntax originalNode)
  {
    return "{0}";
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    LiteralExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type?.SpecialType!;
    var typeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(type.Value);
    var result = new List<(int, string)>();

    // Mutation: value => 0
    result.Add((1, "0"));
    // Mutation: value => -value
    // (Unary negative operator cannot be applied to unsigned or char types.)
    if (typeClassification is not CodeAnalysisUtil.SupportedType.UnsignedIntegral)
      result.Add((2, "-{0}"));
    // Mutation: value => value - 1
    result.Add((3, "{0} - 1"));
    // Mutation: value => value + 1
    result.Add((4, "{0} + 1"));
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
    var type = SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
    return $"ReplaceNumericConstant_{type}";
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
        {SpecialType.System_Int32, ""}, 
        {SpecialType.System_Int64, "L"},
        // Unsigned numeric types
        {SpecialType.System_UInt32, "U"}, 
        {SpecialType.System_UInt64, "UL"},
        // Floating point types
        {SpecialType.System_Single, "f"}, 
        {SpecialType.System_Double, "d"},
        {SpecialType.System_Decimal, "m"}
      }.ToFrozenDictionary();
}