using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

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
    Log.Debug("Processing postfix unary expression: {SyntaxNode}",
      originalNode.GetText().ToString());
    
    SyntaxNode[] nodes = [originalNode, originalNode.Operand];

    // Ignore: Cannot obtain type information
    if (nodes.Any(node =>
          !SyntaxRewriterUtil.IsTypeResolvableLogged(in SemanticModel, in node)))
      return false;

    var types = nodes.Select(node =>
      SemanticModel.GetTypeInfo(node).ResolveType()
        .GetNullableUnderlyingType()!);

    // Ignore: type contains generic type parameter
    return !types.Any(type => SyntaxRewriterUtil.ContainsGenericTypeParameterLogged(in type)) 
           && SupportedOperators.ContainsKey(originalNode.Kind());
  }

  public static string ExpressionTemplate(SyntaxKind kind)
  {
    return $"{{0}}{SupportedOperators[kind]}";
  }

  protected override ExpressionRecord OriginalExpression(
    PostfixUnaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions)
  {
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind()));
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
    SupportedUnaryOperators()
  {
    return SupportedOperators;
  }

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(PostfixUnaryExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return [
      ..attachIdToMutants.Select(entry =>
        (entry.Item1,
          new ExpressionRecord(entry.Item2, ExpressionTemplate(entry.Item2))
        )
      )
    ];
  }

  protected override ImmutableArray<string> ParameterTypes(
    PostfixUnaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> _)
  {
    // Since the supported postfix unary expressions can be either 
    // postincrement or postdecrement, they are guaranteed to be updatable
    var operandAbsoluteType = SemanticModel.GetTypeInfo(originalNode.Operand)
      .ResolveType().GetNullableUnderlyingType()!
      .ToDisplayString();

    return [$"ref {operandAbsoluteType}"];
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
            CodeAnalysisUtil.IncrementOrDecrementTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.PostDecrementExpression, //x--
          new(SyntaxKind.PostDecrementExpression,
            SyntaxKind.MinusMinusToken,
            WellKnownMemberNames.DecrementOperatorName,
            CodeAnalysisUtil.IncrementOrDecrementTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}