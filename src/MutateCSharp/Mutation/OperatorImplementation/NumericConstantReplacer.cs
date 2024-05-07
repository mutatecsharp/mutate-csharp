using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class NumericConstantReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(sutAssembly,
    semanticModel)
{
  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    Log.Debug("Processing numeric constant: {SyntaxNode}", 
      originalNode.GetText().ToString());
    var type = SemanticModel.GetTypeInfo(originalNode).ResolveType()?.SpecialType;
    return type.HasValue &&
           SupportedNumericTypesToSuffix.ContainsKey(type.Value);
  }

  protected override ExpressionRecord OriginalExpression(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
  {
    return new ExpressionRecord(originalNode.Kind(), "{0}");
  }

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(LiteralExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).ConvertedType?.SpecialType!;
    var typeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(type.Value);
    var result = new List<(int, ExpressionRecord)>();

    // Mutation: value => 0
    result.Add((1,
      new ExpressionRecord(SyntaxKind.NumericLiteralExpression, "0")));
    // Mutation: value => -value
    // (Unary negative operator cannot be applied to unsigned or char types,
    // as the type of resulting expression is different from that of the operand.)
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

  protected override ImmutableArray<string> ParameterTypes(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
  {
    return [ReturnType(originalNode)];
  }

  /*
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types
   * If the literal has no suffix, its type is the first of the following types
   * in which its value can be represented: int, uint, long, ulong.
   *
   * Since we can declare literals that are differently typed, and we do not
   * have mutations that perform type conversions, we handle numeric literals
   * specially by narrowing the type of the literal.
   */
  protected override string ReturnType(LiteralExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).ConvertedType!.ToDisplayString();
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