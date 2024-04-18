using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

/*
 * Note: we handle updatable parameters differently from non-updatable parameters.
 * By default, since value types are passed by value in C# whereas
 * user-defined types are passed by reference, we take care to pass value types
 * by reference instead
 */
public sealed partial class PrefixUnaryExprOpReplacer(SemanticModel semanticModel)
: AbstractUnaryMutationOperator<PrefixUnaryExpressionSyntax>(semanticModel)
{
  protected override bool CanBeApplied(PrefixUnaryExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
   => $"{SupportedOperators[kind]}{{0}}";

  protected override string OriginalExpressionTemplate(
    PrefixUnaryExpressionSyntax originalNode)
  => ExpressionTemplate(originalNode.Kind());
  
  protected override IList<(int, string)> ValidMutantExpressionsTemplate(PrefixUnaryExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return attachIdToMutants.Select(entry =>
      (entry.Item1, ExpressionTemplate(entry.Item2))).ToList();
  }

  protected override IList<string> ParameterTypes(PrefixUnaryExpressionSyntax originalNode)
  {
    var operandType = SemanticModel.GetTypeInfo(originalNode.Operand).Type!
      .ToDisplayString();
      
    // Updatable type
    if (originalNode.Kind() 
        is SyntaxKind.PreIncrementExpression
        or SyntaxKind.PreDecrementExpression)
    {
      return [$"ref {operandType}"];
    }
    
    return [operandType];
  }

  protected override string ReturnType(PrefixUnaryExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
  }

  protected override string SchemaBaseName(PrefixUnaryExpressionSyntax originalNode)
  {
    return "ReplacePrefixUnaryExpressionOperator";
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp> SupportedUnaryOperators()
  {
    return SupportedOperators;
  }
}

public sealed partial class PrefixUnaryExprOpReplacer
{
  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
      {
        {
          SyntaxKind.UnaryPlusExpression, // +x
          new (ExprKind: SyntaxKind.UnaryPlusExpression,
          TokenKind: SyntaxKind.PlusToken,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.UnaryMinusExpression, // -x
          new(ExprKind: SyntaxKind.UnaryMinusExpression,
            TokenKind: SyntaxKind.MinusToken,
            TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.BitwiseNotExpression, // ~x
          new(ExprKind: SyntaxKind.BitwiseNotExpression,
            TokenKind: SyntaxKind.TildeToken,
            TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        {
          SyntaxKind.LogicalNotExpression, // !x
          new(ExprKind: SyntaxKind.LogicalNotExpression,
            TokenKind: SyntaxKind.ExclamationToken,
            TypeSignatures: CodeAnalysisUtil.BooleanLogicalTypeSignature)
        },
        {
          SyntaxKind.PreIncrementExpression, // ++x
          new(ExprKind: SyntaxKind.PreIncrementExpression,
            TokenKind: SyntaxKind.PlusPlusToken,
            TypeSignatures: CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        },
        {
          SyntaxKind.PreDecrementExpression, // --x
          new(ExprKind: SyntaxKind.PreDecrementExpression,
            TokenKind: SyntaxKind.MinusMinusToken,
            TypeSignatures: CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}