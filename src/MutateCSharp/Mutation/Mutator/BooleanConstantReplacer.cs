using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.Mutator;

public class BooleanConstantReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(sutAssembly,
    semanticModel)
{
  private static readonly ImmutableArray<(int id, ExpressionRecord expr)>
    MutantExpressions =
      [(1, new ExpressionRecord(SyntaxKind.LogicalNotExpression, "!{0}"))];

  private readonly ITypeSymbol _booleanTypeSymbol =
    semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);

  private readonly ImmutableArray<ITypeSymbol> _booleanOperandSymbol =
    [semanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean)];

  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    Log.Debug("Processing boolean constant: {SyntaxNode}",
      originalNode.GetText().ToString());
    return originalNode.IsKind(SyntaxKind.TrueLiteralExpression) ||
           originalNode.IsKind(SyntaxKind.FalseLiteralExpression);
  }

  protected override ExpressionRecord OriginalExpression(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _,
    ITypeSymbol? requiredReturnType)
  {
    return new ExpressionRecord(originalNode.Kind(), "{0}");
  }

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(LiteralExpressionSyntax _,
      ITypeSymbol? requiredReturnType)
  {
    return MutantExpressions;
  }

  protected override CodeAnalysisUtil.MethodSignature?
    NonMutatedTypeSymbols(LiteralExpressionSyntax originalNode,
      ITypeSymbol? requiredReturnType)
  {
    return new CodeAnalysisUtil.MethodSignature(_booleanTypeSymbol,
      _booleanOperandSymbol);
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(LiteralExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType)
  {
    return ["bool"];
  }

  protected override string SchemaReturnTypeDisplay(
    LiteralExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    return "bool";
  }

  protected override string SchemaBaseName()
  {
    return "ReplaceBooleanConstant";
  }
}