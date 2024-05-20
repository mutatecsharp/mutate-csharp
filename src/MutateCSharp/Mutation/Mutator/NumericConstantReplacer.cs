using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.Mutator;

/*
 * The replacer takes in the original type of the literal and returns the
 * converted type that matches the assigned return type of the variable.
 * As a numeric literal is polymorphic in form we would have to loosen
 * the type checks from LiteralExpressionSyntax to ExpressionSyntax.
 */
public sealed partial class NumericConstantReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel, 
  bool optimise)
  : AbstractMutationOperator<ExpressionSyntax>(sutAssembly,
    semanticModel)
{
  private readonly FrozenDictionary<SyntaxKind,
      ImmutableArray<CodeAnalysisUtil.MethodSignature>>
    _predefinedUnaryOperatorSignatures =
      semanticModel.BuildUnaryNumericOperatorMethodSignature();
  
  protected override bool CanBeApplied(ExpressionSyntax originalNode)
  {
    Log.Debug("Processing numeric constant: {SyntaxNode}", 
      originalNode.GetText().ToString());
    
    var type = SemanticModel.ResolveTypeSymbol(originalNode)?.SpecialType;
    var isPositiveLiteral =
      originalNode.IsKind(SyntaxKind.NumericLiteralExpression);

    return type is not null && (isPositiveLiteral || originalNode.IsNegativeLiteral());
  }

  protected override ExpressionRecord OriginalExpression(
    ExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _, ITypeSymbol? requiredReturnType)
  {
    return new ExpressionRecord(originalNode.Kind(), CodeAnalysisUtil.OperandKind.None, "{0}");
  }
  
  private bool ReplacementOperatorIsValid(
    ExpressionSyntax originalNode,
    CodeAnalysisUtil.MethodSignature typeSymbols,
    CodeAnalysisUtil.Op replacementOp)
  {
    if (replacementOp.ExprKind is SyntaxKind.NumericLiteralExpression)
    {
      return !optimise ||
             int.TryParse(originalNode.ToString(), out var value) &&
             value != 0;
    }

    var returnType = typeSymbols.ReturnType;
    var operandType = typeSymbols.OperandTypes[0];
    
    if (PrefixUnaryExprOpReplacer.SupportedOperators.ContainsKey(replacementOp
          .ExprKind))
    {
      if (optimise && int.TryParse(originalNode.ToString(), out var value) && value == 0) 
        return false;
      
      var mutantExpressionType =
        SemanticModel.ResolveOverloadedPredefinedUnaryOperator(
          _predefinedUnaryOperatorSignatures,
          replacementOp.ExprKind, typeSymbols);
      if (!mutantExpressionType.HasValue) return false;

      var resolvedSignature = mutantExpressionType.Value;
    
      // A mutation is only valid if the mutant expression type is assignable to
      // the mutant schema return type, determined by the narrower type between the
      // original expression type and converted expression type.
      return SemanticModel.Compilation.HasImplicitConversion(
        operandType, resolvedSignature.operandSymbol)
        && SemanticModel.Compilation.HasImplicitConversion(
        resolvedSignature.returnSymbol, returnType);
    }
    
    return true;
  }
  
  protected override
    ImmutableArray<ExpressionRecord>
    ValidMutantExpressions(ExpressionSyntax originalNode, ITypeSymbol? requiredReturnType)
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
        new ExpressionRecord(entry.op,
          CodeAnalysisUtil.OperandKind.None, 
          SupportedOperators[entry.op].Template)
      )
    ];
  }

  /* The parameter type must always be a numeric type to allow arithmetic
   * mutations.
   * 
   * If the determined type of an integer literal is int and the value
   * represented by the literal is within the range of the destination type,
   * the value can be implicitly converted to sbyte, byte, short, ushort,
   * uint, ulong, nint or nuint.
   */
  protected override CodeAnalysisUtil.MethodSignature? NonMutatedTypeSymbols(
    ExpressionSyntax originalNode, ITypeSymbol? requiredReturnType)
  {
    // Positive literal possible determined type: int, uint, long, ulong
    // Negative literal possible determined type: int, long
    var literalAbsoluteType = semanticModel.ResolveTypeSymbol(originalNode)!;
    
    // Determine if there is an implicit conversion from the literal to the
    // required type; otherwise do not convert
    if (requiredReturnType is not null)
    {
      var absoluteRequiredReturnType =
        requiredReturnType.GetNullableUnderlyingType();
      
      if (SemanticModel.CanImplicitlyConvertNumericLiteral(
            originalNode, absoluteRequiredReturnType.SpecialType))
      {
        var narrowerType = SemanticModel.DetermineNarrowerNumericType(
          absoluteRequiredReturnType, literalAbsoluteType); 
          
        return new CodeAnalysisUtil.MethodSignature(absoluteRequiredReturnType, [narrowerType]);
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
    
    // Narrow the numeric literal type if the determined type is int but
    // the converted type is narrower than int, and that the literal can be
    // implicitly converted to the narrower numeric type
    if (SemanticModel.CanImplicitlyConvertNumericLiteral(
          originalNode, absoluteConvertedTypeSymbol.SpecialType))
    {
      var narrowerType = SemanticModel.DetermineNarrowerNumericType(
        absoluteConvertedTypeSymbol, literalAbsoluteType); 
        
      return new CodeAnalysisUtil.MethodSignature(narrowerType, [narrowerType]);
    }

    return null;
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(ExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } result)
      return [];

    return [result.OperandTypes[0].ToDisplayString()];
  }

  protected override string SchemaReturnTypeDisplay(
    ExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
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
}

/*
 * Supported numeric types.
 *
 * More on supported C# value types (signed integral/unsigned integral/char):
 * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-types
 */
public sealed partial class NumericConstantReplacer
{
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