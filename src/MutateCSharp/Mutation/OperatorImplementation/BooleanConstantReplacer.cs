using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Mutation.OperatorImplementation;


public class BooleanConstantReplacer(SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(semanticModel)
{
  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    return originalNode.IsKind(SyntaxKind.TrueLiteralExpression) ||
           originalNode.IsKind(SyntaxKind.FalseLiteralExpression);
  }

  protected override string OriginalExpressionTemplate(
    LiteralExpressionSyntax originalNode)
  {
    return "{0}";
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    LiteralExpressionSyntax _)
  {
    return ImmutableArray.Create((1, "!{0}"));
  }

  protected override IList<string> ParameterTypes(
    LiteralExpressionSyntax originalNode)
  {
    return ImmutableArray.Create(ReturnType(originalNode));
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