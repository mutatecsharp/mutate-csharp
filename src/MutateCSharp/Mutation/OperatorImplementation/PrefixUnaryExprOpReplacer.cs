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
  {
    return $"{SupportedOperators[kind]}{{0}}";
  }

  protected override ExpressionRecord OriginalExpression(
    PrefixUnaryExpressionSyntax originalNode, IList<ExpressionRecord> _)
  {
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind()));
  }

  protected override IList<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(
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
      (entry.Item1,
        new ExpressionRecord(entry.Item2, ExpressionTemplate(entry.Item2))
      )
    ).ToList();
  }

  protected override IList<string> ParameterTypes(
    PrefixUnaryExpressionSyntax originalNode, IList<ExpressionRecord> _)
  {
    var operandType = SemanticModel.GetTypeInfo(originalNode.Operand).Type!
      .ToDisplayString();

    // Check if operand is updatable
    return IsOperandAssignable(originalNode)
      ? [$"ref {operandType}"]
      : [operandType];
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
          new(SyntaxKind.UnaryPlusExpression,
            SyntaxKind.PlusToken,
            WellKnownMemberNames.AdditionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.UnaryMinusExpression, // -x
          new(SyntaxKind.UnaryMinusExpression,
            SyntaxKind.MinusToken,
            WellKnownMemberNames.SubtractionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.BitwiseNotExpression, // ~x
          new(SyntaxKind.BitwiseNotExpression,
            SyntaxKind.TildeToken,
            WellKnownMemberNames.OnesComplementOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature)
        },
        {
          SyntaxKind.LogicalNotExpression, // !x
          new(SyntaxKind.LogicalNotExpression,
            SyntaxKind.ExclamationToken,
            WellKnownMemberNames.LogicalNotOperatorName,
            CodeAnalysisUtil.BooleanLogicalTypeSignature)
        },
        {
          SyntaxKind.PreIncrementExpression, // ++x
          new(SyntaxKind.PreIncrementExpression,
            SyntaxKind.PlusPlusToken,
            WellKnownMemberNames.IncrementOperatorName,
            CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        },
        {
          SyntaxKind.PreDecrementExpression, // --x
          new(SyntaxKind.PreDecrementExpression,
            SyntaxKind.MinusMinusToken,
            WellKnownMemberNames.DecrementOperatorName,
            CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}