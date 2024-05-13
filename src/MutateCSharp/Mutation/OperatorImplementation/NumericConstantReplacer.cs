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
    return type.HasValue && SupportedNumericTypesToSuffix.ContainsKey(type.Value);
  }

  protected override ExpressionRecord OriginalExpression(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _, ITypeSymbol? requiredReturnType)
  {
    return new ExpressionRecord(originalNode.Kind(), "{0}");
  }
  
  private bool ReplacementOperatorIsValid(
    LiteralExpressionSyntax originalNode, 
    CodeAnalysisUtil.MethodSignature typeSymbols,
    CodeAnalysisUtil.Op replacementOp)
  {
    if (replacementOp.ExprKind is SyntaxKind.NumericLiteralExpression)
    {
      return true;
    }

    var returnType = typeSymbols.ReturnType;
    var operandType = typeSymbols.OperandTypes[0];
    
    if (PrefixUnaryExprOpReplacer.SupportedOperators.ContainsKey(replacementOp
          .ExprKind))
    {
      var mutantExpressionType =
        SemanticModel.ResolveOverloadedPredefinedUnaryOperator(
          replacementOp.ExprKind, returnType.SpecialType,
          operandType.SpecialType);
      if (!mutantExpressionType.HasValue) return false;

      var (resolvedReturnType, resolvedOperandType) =
        mutantExpressionType.Value;
    
      // A mutation is only valid if the mutant expression type is assignable to
      // the mutant schema return type, determined by the narrower type between the
      // original expression type and converted expression type.
      return SemanticModel.Compilation.HasImplicitConversion(
        operandType, resolvedOperandType)
        && SemanticModel.Compilation.HasImplicitConversion(
        resolvedReturnType, returnType);
    }
    
    return true;
  }
  
  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(LiteralExpressionSyntax originalNode, ITypeSymbol? requiredReturnType)
  {
    var typeSymbols = NonMutatedTypeSymbols(originalNode, requiredReturnType);
    if (typeSymbols is null) return [];
    var validMutants = SupportedOperators.Values
      .Where(replacement => 
        ReplacementOperatorIsValid(originalNode, typeSymbols, replacement.Op))
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

  /*
   * If the determined type of an integer literal is int and the value
   * represented by the literal is within the range of the destination type,
   * the value can be implicitly converted to sbyte, byte, short, ushort,
   * uint, ulong, nint or nuint.
   */
  protected override CodeAnalysisUtil.MethodSignature? NonMutatedTypeSymbols(
    LiteralExpressionSyntax originalNode, ITypeSymbol? requiredReturnType)
  {
    // Determine if there is an implicit conversion from the literal to the
    // required type; otherwise do not convert
    if (requiredReturnType is not null)
    {
      var absoluteRequiredReturnType =
        requiredReturnType.GetNullableUnderlyingType();
      
      if (SemanticModel.CanImplicitlyConvertNumericLiteral(
            originalNode, absoluteRequiredReturnType.SpecialType))
      {
        return new CodeAnalysisUtil.MethodSignature(absoluteRequiredReturnType,
          [absoluteRequiredReturnType]);
      }

      return null;
    }
    
    /*
     * When overwriting with syntax rewriter, the semantic model cannot infer
     * the type of the replaced nodes although it may be semantically valid to
     * modify the return type of a node with a wider type symbol between the
     * original type and the converted type, which allows for more candidate
     * mutations. We proceed with selecting the narrower type between the two
     * to avoid compilation errors.
     */
    var convertedTypeSymbol =
      SemanticModel.ResolveConvertedTypeSymbol(originalNode)!;
    var absoluteConvertedTypeSymbol =
      convertedTypeSymbol.GetNullableUnderlyingType();
    
    if (SemanticModel.CanImplicitlyConvertNumericLiteral(
          originalNode, absoluteConvertedTypeSymbol.SpecialType))
    {
      return new CodeAnalysisUtil.MethodSignature(absoluteConvertedTypeSymbol,
        [absoluteConvertedTypeSymbol]);
    }

    return null;
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(LiteralExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } result)
      return [];

    return [result.OperandTypes[0].ToDisplayString()];
  }

  protected override string SchemaReturnTypeDisplay(LiteralExpressionSyntax originalNode,
    ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } result)
      return string.Empty;

    return result.ReturnType.ToDisplayString();
  }
  
  protected override string SchemaBaseName()
  {
    return "ReplaceNumericConstant";
  }

  /*
   * https://stackoverflow.com/questions/60778208/overload-resolution-with-implicit-conversions
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#102-implicit-conversions
   * https://github.com/dotnet/roslyn/blob/main/docs/specs/CSharp%206/Better%20Betterness.md
   * Given types T1 and T2, T1 is wider if no implicit conversion from T1 to T2
   * exists, and an implicit conversion from T2 to T1 exists.
   * If an implicit conversion exists from T1 to T2 *and* T2 to T1, we select the
   * more narrow type as the result.
   */
  private ITypeSymbol? DetermineNarrowerType(ITypeSymbol convertedType, ITypeSymbol exprType)
  {
    var exprToConverted = 
      SemanticModel.Compilation.HasImplicitConversion(exprType, convertedType);
    var convertedToExpr =
      SemanticModel.Compilation.HasImplicitConversion(convertedType, exprType);
    
    // converted type is narrower than expression type
    if (convertedToExpr) return convertedType;
    // expression type is narrower than converted type
    if (exprToConverted) return exprType;
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
            string.Empty), 
            "0")
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