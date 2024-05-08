using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.OperatorImplementation;

/*
 * The replacer takes in the original type of the literal and returns the
 * converted type that matches the assigned return type of the variable.
 */
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
    var numericType = SemanticModel.GetTypeInfo(originalNode).Type!.SpecialType;
    var returnType = SemanticModel.GetTypeInfo(originalNode)
      .ConvertedType!.SpecialType;

    var validMutants = SupportedOperators.Values.Where(replacement =>
    {
      return replacement.Op.ExprKind switch
      {
        SyntaxKind.NumericLiteralExpression => true,
        _ when PrefixUnaryExprOpReplacer.SupportedOperators.ContainsKey(
            replacement.Op.ExprKind)
          => !replacement.Op.PrimitiveTypesToExclude(numericType) &&
            CodeAnalysisUtil.ResolveUnaryPrimitiveReturnType(numericType, replacement.Op.ExprKind)
              .SpecialType.CanBeImplicitlyConvertedTo(returnType),
        _ when BinExprOpReplacer.SupportedOperators.ContainsKey(replacement.Op
            .ExprKind)
          => true,
        _ => true
      };
    }).Select(replacement => replacement.Op.ExprKind);

    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);

    return
    [
      ..attachIdToMutants.Select(entry =>
        (entry.id, new ExpressionRecord(entry.op,
            SupportedOperators[entry.op].Template)))
    ];
  }

  protected override ImmutableArray<string> ParameterTypes(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
  {
    var originalType = SemanticModel.GetTypeInfo(originalNode).ResolveType()!
      .ToDisplayString();
    return [originalType];
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
    var operandType = SemanticModel.GetTypeInfo(originalNode)
      .ResolveType()!.ToDisplayString();
    return $"ReplaceNumeric{operandType}ConstantReturn{ReturnType(originalNode)}";
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

  private static readonly FrozenDictionary<
      SyntaxKind, (CodeAnalysisUtil.Op Op, string Template)>
    SupportedOperators
      = new Dictionary<SyntaxKind, (CodeAnalysisUtil.Op, string)>
      {
        {
          SyntaxKind.NumericLiteralExpression, // x -> 0
          (new(SyntaxKind.NumericLiteralExpression,
            // There are multiple token kinds of literal but we omit its use
            SyntaxKind.None,
            string.Empty,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude
          ), "0")
        },
        {
          SyntaxKind.UnaryMinusExpression, // x -> -x
          (PrefixUnaryExprOpReplacer.SupportedOperators[SyntaxKind.UnaryMinusExpression], "-{0}")
        },
        {
          SyntaxKind.SubtractExpression, // x -> x - 1
          (BinExprOpReplacer.SupportedOperators[SyntaxKind.SubtractExpression], "{0} - 1")
        },
        {
          SyntaxKind.AddExpression, // x -> x + 1
          (BinExprOpReplacer.SupportedOperators[SyntaxKind.AddExpression], "{0} + 1")
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}