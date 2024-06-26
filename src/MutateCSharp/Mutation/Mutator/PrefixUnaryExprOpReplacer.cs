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
 * Note: we handle updatable parameters differently from non-updatable parameters.
 * By default, since value types are passed by value in C# whereas
 * user-defined types are passed by reference, we take care to pass value types
 * by reference instead
 */
public sealed partial class PrefixUnaryExprOpReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel,
  FrozenDictionary<SyntaxKind,
    ImmutableArray<CodeAnalysisUtil.MethodSignature>> builtInOperatorSignatures,
  bool optimise)
  : AbstractUnaryMutationOperator<PrefixUnaryExpressionSyntax>(
    sutAssembly, semanticModel, builtInOperatorSignatures)
{
  protected override bool CanBeApplied(PrefixUnaryExpressionSyntax originalNode)
  {
    Log.Debug("Processing prefix unary expression: {SyntaxNode}",
      originalNode.GetText().ToString());
    
    SyntaxNode[] nodes = [originalNode, originalNode.Operand];

    // Ignore: Cannot obtain type information
    if (nodes.Any(node =>
          !SyntaxRewriterUtil.IsTypeResolvableLogged(in SemanticModel, in node)))
      return false;

    var types = nodes.Select(node =>
      SemanticModel.ResolveTypeSymbol(node)!.GetNullableUnderlyingType());

    // Exclude node from mutation:
    // 1) If type contains generic type parameter;
    // 2) If type is private (and thus inaccessible).
    return types.All(type =>
      !SyntaxRewriterUtil.ContainsGenericTypeParameterLogged(in type) 
      && type.GetVisibility() is not CodeAnalysisUtil.SymbolVisibility.Private
    ) && SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return kind.IsSyntaxKindLiteral() 
      ? SupportedOperators[kind].ToString() 
      : $"{SupportedOperators[kind]}{{0}}";
  }

  protected override ExpressionRecord OriginalExpression(
    PrefixUnaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> _, 
    ITypeSymbol? requiredReturnType)
  {
    return new ExpressionRecord(originalNode.Kind(),
      CodeAnalysisUtil.OperandKind.None,
      ExpressionTemplate(originalNode.Kind()));
  }

  protected override ImmutableArray<ExpressionRecord>
    ValidMutantExpressions(PrefixUnaryExpressionSyntax originalNode, 
      ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } methodSignature) return [];
    
    var validMutants = 
      ValidOperatorReplacements(originalNode, methodSignature, optimise);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(
        OperatorIds, validMutants);
    var sortedMutants = attachIdToMutants.Select(entry =>
        new ExpressionRecord(entry.op, CodeAnalysisUtil.OperandKind.None, ExpressionTemplate(entry.op))
    ).ToList();
    
    // Append operand as mutant
    var validOperand =
      ValidOperandReplacement(originalNode, methodSignature, optimise);
    if (validOperand.Contains(CodeAnalysisUtil.OperandKind.UnaryOperand))
    {
      var operand = new ExpressionRecord(SyntaxKind.Argument,
        CodeAnalysisUtil.OperandKind.UnaryOperand,
        "{0}");
      sortedMutants.Add(operand);
    }

    return [..sortedMutants];
  }
  
  // check for required return type and operator for type compatability
  protected override CodeAnalysisUtil.MethodSignature?
    NonMutatedTypeSymbols(PrefixUnaryExpressionSyntax originalNode,
      ITypeSymbol? requiredReturnType)
  {
    var operandType = SemanticModel.ResolveTypeSymbol(originalNode.Operand)!;
    var returnType = SemanticModel.ResolveTypeSymbol(originalNode)!;
    var operandAbsoluteType = operandType.GetNullableUnderlyingType();
    var returnAbsoluteType = returnType.GetNullableUnderlyingType();
    // Don't have to check for the null keyword since no unary operator is
    // applicable to the null keyword (null has no type in C#)
    if (SemanticModel.IsNumeric(operandAbsoluteType))
    {
      if (originalNode.Operand.IsKind(SyntaxKind.NumericLiteralExpression)
          && SemanticModel.CanImplicitlyConvertNumericLiteral(
            originalNode.Operand, returnAbsoluteType.SpecialType))
      {
        operandType = returnAbsoluteType;
      }
      
      // Construct method
      var methodSignature =
        new CodeAnalysisUtil.MethodSignature(returnType, [operandType]);
      
      if (SemanticModel.ResolveOverloadedPredefinedUnaryOperator(
            BuiltInOperatorSignatures, originalNode.Kind(), methodSignature) 
          is { } result)
      {
        return new CodeAnalysisUtil.MethodSignature(result.returnSymbol,
          [result.operandSymbol]);
      }
      
      return null;
    }
    
    // Remove nullable if operand is of reference type, since reference type
    // T? can be cast to T
    if (!operandType.IsValueType)
      operandType = operandType.GetNullableUnderlyingType();

    return new CodeAnalysisUtil.MethodSignature(returnType, [operandType]);
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(
    PrefixUnaryExpressionSyntax originalNode, 
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } methodSignature) return [];
    
    // Check if any of original or mutant expressions update the argument
    return CodeAnalysisUtil.VariableModifyingOperators.Contains(
             originalNode.Kind())
           || mutantExpressions.Any(op =>
             CodeAnalysisUtil.VariableModifyingOperators.Contains(op.Operation))
      ? [$"ref {methodSignature.OperandTypes[0].ToDisplayString()}"]
      : [methodSignature.OperandTypes[0].ToDisplayString()];
  }

  protected override string SchemaReturnTypeDisplay(
    PrefixUnaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    return NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
      { } typeSignature ? string.Empty : typeSignature.ReturnType.ToDisplayString();
  }

  protected override string SchemaBaseName()
  {
    return "ReplacePrefixUnaryExprOp";
  }

  protected override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedUnaryOperators()
  {
    return SupportedOperators;
  }
}

public sealed partial class PrefixUnaryExprOpReplacer
{
  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.Op>
      {
        {
          SyntaxKind.UnaryPlusExpression, // +x
          new(SyntaxKind.UnaryPlusExpression,
            SyntaxKind.PlusToken,
            WellKnownMemberNames.UnaryPlusOperatorName)
        },
        {
          SyntaxKind.UnaryMinusExpression, // -x
          new(SyntaxKind.UnaryMinusExpression,
            SyntaxKind.MinusToken,
            WellKnownMemberNames.UnaryNegationOperatorName)
        },
        {
          SyntaxKind.BitwiseNotExpression, // ~x
          new(SyntaxKind.BitwiseNotExpression,
            SyntaxKind.TildeToken,
            WellKnownMemberNames.OnesComplementOperatorName)
        },
        {
          SyntaxKind.LogicalNotExpression, // !x
          new(SyntaxKind.LogicalNotExpression,
            SyntaxKind.ExclamationToken,
            WellKnownMemberNames.LogicalNotOperatorName)
        },
        {
          SyntaxKind.PreIncrementExpression, // ++x
          new(SyntaxKind.PreIncrementExpression,
            SyntaxKind.PlusPlusToken,
            WellKnownMemberNames.IncrementOperatorName)
        },
        {
          SyntaxKind.PreDecrementExpression, // --x
          new(SyntaxKind.PreDecrementExpression,
            SyntaxKind.MinusMinusToken,
            WellKnownMemberNames.DecrementOperatorName)
        },
        // Boolean literals (true, false)
        // Redundant mutants
        // {
        //   SyntaxKind.TrueLiteralExpression,
        //   new(SyntaxKind.TrueLiteralExpression,
        //     SyntaxKind.TrueKeyword,
        //     WellKnownMemberNames.TrueOperatorName)
        // },
        // {
        //   SyntaxKind.FalseLiteralExpression,
        //   new(SyntaxKind.FalseLiteralExpression,
        //     SyntaxKind.FalseKeyword,
        //     WellKnownMemberNames.FalseOperatorName)
        // }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}