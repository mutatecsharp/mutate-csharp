using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class PostfixUnaryExprOpReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractUnaryMutationOperator<PostfixUnaryExpressionSyntax>(
    sutAssembly, semanticModel)
{
  protected override bool CanBeApplied(
    PostfixUnaryExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }

  public static string ExpressionTemplate(SyntaxKind kind)
  {
    return $"{{0}}{SupportedOperators[kind]}";
  }

  protected override ExpressionRecord OriginalExpression(
    PostfixUnaryExpressionSyntax originalNode,
    IList<ExpressionRecord> mutantExpressions)
  {
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind()));
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
    SupportedUnaryOperators()
  {
    return SupportedOperators;
  }

  protected override IList<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(PostfixUnaryExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
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
    PostfixUnaryExpressionSyntax originalNode, IList<ExpressionRecord> _)
  {
    // Since the supported postfix unary expressions can be either 
    // postincrement or postdecrement, they are guaranteed to be updatable
    var operandType = SemanticModel.GetTypeInfo(originalNode.Operand).Type!
      .ToDisplayString();

    return [$"ref {operandType}"];
  }

  protected override string ReturnType(
    PostfixUnaryExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
  }

  protected override string SchemaBaseName(
    PostfixUnaryExpressionSyntax originalNode)
  {
    return $"ReplacePostfixUnaryExprOpReturn{ReturnType(originalNode)}";
  }
}

public sealed partial class PostfixUnaryExprOpReplacer
{
  private static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
      {
        {
          SyntaxKind.PostIncrementExpression, // x++
          new(SyntaxKind.PostIncrementExpression,
            SyntaxKind.PlusPlusToken,
            WellKnownMemberNames.IncrementOperatorName,
            CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        },
        {
          SyntaxKind.PostDecrementExpression, //x--
          new(SyntaxKind.PostDecrementExpression,
            SyntaxKind.MinusMinusToken,
            WellKnownMemberNames.DecrementOperatorName,
            CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}