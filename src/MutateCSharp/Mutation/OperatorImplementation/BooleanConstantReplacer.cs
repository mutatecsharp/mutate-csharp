using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serilog;

namespace MutateCSharp.Mutation.OperatorImplementation;

public class BooleanConstantReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(sutAssembly,
    semanticModel)
{
  private static readonly ImmutableArray<string> ParameterType = ["bool?"];
  private static readonly ImmutableArray<(int id, ExpressionRecord expr)> 
    MutantExpressions = [(1, new ExpressionRecord(SyntaxKind.LogicalNotExpression, "!{0}"))];
  
  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    Log.Debug("Processing boolean constant: {SyntaxNode}",
      originalNode.GetText().ToString());
    return originalNode.IsKind(SyntaxKind.TrueLiteralExpression) ||
           originalNode.IsKind(SyntaxKind.FalseLiteralExpression);
  }

  protected override ExpressionRecord OriginalExpression(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
  {
    return new ExpressionRecord(originalNode.Kind(), "{0}");
  }

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(LiteralExpressionSyntax _)
  {
    return MutantExpressions;
  }

  protected override ImmutableArray<string> ParameterTypes(
    LiteralExpressionSyntax _, ImmutableArray<ExpressionRecord> __)
  {
    return ParameterType;
  }

  protected override string ReturnType(LiteralExpressionSyntax _)
  {
    return "bool";
  }

  protected override string SchemaBaseName(LiteralExpressionSyntax _)
  {
    return "ReplaceBooleanConstant";
  }
}