using System.Collections.Frozen;
using System.Reflection;
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
public sealed partial class PrefixUnaryExprOpReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractUnaryMutationOperator<PrefixUnaryExpressionSyntax>(
    sutAssembly, semanticModel)
{
  protected override bool CanBeApplied(PrefixUnaryExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }

  public static string ExpressionTemplate(SyntaxKind kind)
    => $"{SupportedOperators[kind]}{{0}}";

  protected override string OriginalExpressionTemplate(
    PrefixUnaryExpressionSyntax originalNode)
    => ExpressionTemplate(originalNode.Kind());

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    PrefixUnaryExpressionSyntax originalNode)
  {
    // Perform additional filtering for assignable variables
    var validMutants = ValidMutants(originalNode).ToHashSet();
    if (!IsOperandAssignable(originalNode))
    {
      validMutants.Remove(SyntaxKind.PreIncrementExpression);
      validMutants.Remove(SyntaxKind.PreDecrementExpression);
    }

    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return attachIdToMutants.Select(entry =>
      (entry.Item1, ExpressionTemplate(entry.Item2))).ToList();
  }

  protected override IList<string> ParameterTypes(
    PrefixUnaryExpressionSyntax originalNode)
  {
    var operandType = SemanticModel.GetTypeInfo(originalNode.Operand).Type!
      .ToDisplayString();

    // Check if operand is updatable
    return IsOperandAssignable(originalNode)
      ? [$"ref {operandType}"] : [operandType];
  }

  protected override string ReturnType(PrefixUnaryExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
  }

  protected override string SchemaBaseName(
    PrefixUnaryExpressionSyntax originalNode)
  {
    return $"ReplacePrefixUnaryExprOpReturn{ReturnType(originalNode)}";
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
    SupportedUnaryOperators()
  {
    return SupportedOperators;
  }

  private bool IsOperandAssignable(PrefixUnaryExpressionSyntax originalNode)
  {
    return originalNode.Kind()
             is SyntaxKind.PreIncrementExpression
             or SyntaxKind.PreDecrementExpression
           || (SemanticModel.GetSymbolInfo(originalNode.Operand).Symbol?
             .IsSymbolVariable() ?? false);
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
          new(ExprKind: SyntaxKind.UnaryPlusExpression,
            TokenKind: SyntaxKind.PlusToken,
            MemberName: WellKnownMemberNames.AdditionOperatorName,
            TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.UnaryMinusExpression, // -x
          new(ExprKind: SyntaxKind.UnaryMinusExpression,
            TokenKind: SyntaxKind.MinusToken,
            MemberName: WellKnownMemberNames.SubtractionOperatorName,
            TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.BitwiseNotExpression, // ~x
          new(ExprKind: SyntaxKind.BitwiseNotExpression,
            TokenKind: SyntaxKind.TildeToken,
            MemberName: WellKnownMemberNames.OnesComplementOperatorName,
            TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
        },
        {
          SyntaxKind.LogicalNotExpression, // !x
          new(ExprKind: SyntaxKind.LogicalNotExpression,
            TokenKind: SyntaxKind.ExclamationToken,
            MemberName: WellKnownMemberNames.LogicalNotOperatorName,
            TypeSignatures: CodeAnalysisUtil.BooleanLogicalTypeSignature)
        },
        {
          SyntaxKind.PreIncrementExpression, // ++x
          new(ExprKind: SyntaxKind.PreIncrementExpression,
            TokenKind: SyntaxKind.PlusPlusToken,
            MemberName: WellKnownMemberNames.IncrementOperatorName,
            TypeSignatures: CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        },
        {
          SyntaxKind.PreDecrementExpression, // --x
          new(ExprKind: SyntaxKind.PreDecrementExpression,
            TokenKind: SyntaxKind.MinusMinusToken,
            MemberName: WellKnownMemberNames.DecrementOperatorName,
            TypeSignatures: CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}