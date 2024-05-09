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
    
    var type = SemanticModel.ResolveTypeSymbol(originalNode)?.SpecialType;
    return type.HasValue && ReturnTypeSymbol(originalNode) is not null &&
           SupportedNumericTypesToSuffix.ContainsKey(type.Value);
  }

  protected override ExpressionRecord OriginalExpression(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
  {
    return new ExpressionRecord(originalNode.Kind(), "{0}");
  }
  
  private bool ReplacementOperatorIsValid(
    LiteralExpressionSyntax originalNode, 
    ITypeSymbol returnType,
    CodeAnalysisUtil.Op replacementOp)
  {
    if (!PrefixUnaryExprOpReplacer.SupportedOperators.ContainsKey(replacementOp
          .ExprKind))
      return true;
    
    var numericType = SemanticModel.ResolveTypeSymbol(originalNode)!;
    var resolvedReturnType = SemanticModel.ResolveUnaryPrimitiveReturnType(
      numericType.SpecialType, replacementOp.ExprKind);
    
    // A mutation is only valid if the mutant expression type is assignable to
    // the mutant schema return type, determined by the wider type between the
    // original expression type and converted expression type.
    return !replacementOp.PrimitiveTypesToExclude(numericType.SpecialType) &&
           SemanticModel.Compilation.HasImplicitConversion(
             resolvedReturnType, returnType);
  }
  
  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(LiteralExpressionSyntax originalNode)
  {
    var returnType = ReturnTypeSymbol(originalNode)!;
    var validMutants = SupportedOperators.Values
      .Where(replacement => 
        ReplacementOperatorIsValid(originalNode, returnType, replacement.Op))
      .Select(replacement => replacement.Op.ExprKind);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    
    return
    [
      ..attachIdToMutants.Select(entry =>
        (entry.id, new ExpressionRecord(entry.op, SupportedOperators[entry.op].Template))
      )
    ];
  }

  protected override ImmutableArray<string> ParameterTypes(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
  {
    var originalType = SemanticModel.ResolveTypeSymbol(originalNode)!
      .ToDisplayString();
    return [originalType];
  }

  /*
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types
   * If the literal has no suffix, its type is the first of the following types
   * in which its value can be represented: int, uint, long, ulong.
   */
  protected override string ReturnType(LiteralExpressionSyntax originalNode)
  {
    var returnType = DetermineWiderType(
      SemanticModel.ResolveConvertedTypeSymbol(originalNode)!,
      SemanticModel.ResolveTypeSymbol(originalNode)!);
    return returnType!.ToDisplayString();
  }

  protected override string
    SchemaBaseName(LiteralExpressionSyntax originalNode)
  {
    var operandType = SemanticModel.ResolveTypeSymbol(originalNode)!.ToDisplayString();
    return $"ReplaceNumeric{operandType}ConstantReturn{ReturnType(originalNode)}";
  }

  private ITypeSymbol? ReturnTypeSymbol(LiteralExpressionSyntax originalNode)
  {
    return DetermineWiderType(
      SemanticModel.ResolveConvertedTypeSymbol(originalNode)!,
      SemanticModel.ResolveTypeSymbol(originalNode)!);
  }

  /*
   * https://stackoverflow.com/questions/60778208/overload-resolution-with-implicit-conversions
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#102-implicit-conversions
   * https://github.com/dotnet/roslyn/blob/main/docs/specs/CSharp%206/Better%20Betterness.md
   * Given types T1 and T2, T1 is wider if no implicit conversion from T1 to T2
   * exists, and an implicit conversion from T2 to T1 exists.
   * If an implicit conversion exists from T1 to T2 *and* T2 to T1, we select the
   * original type as the type.
   */
  private ITypeSymbol? DetermineWiderType(ITypeSymbol convertedType, ITypeSymbol exprType)
  {
    var exprToConverted = 
      SemanticModel.Compilation.HasImplicitConversion(exprType, convertedType);
    var convertedToExpr =
      SemanticModel.Compilation.HasImplicitConversion(convertedType, exprType);
    
    if (exprToConverted && !convertedToExpr) return convertedType;
    if (!exprToConverted && convertedToExpr) return exprType;
    // retain original expression type if conversion of both types commute
    if (exprToConverted && convertedToExpr) return exprType; 
    return null;
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