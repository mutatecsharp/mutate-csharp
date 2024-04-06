using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation;

/*
 * Discover target nodes that are eligible to undergo mutation, and apply mutation.
 */
public class MutatorAstRewriter(SemanticModel semanticModel)
  : CSharpSyntaxRewriter
{
  public override SyntaxNode? VisitLiteralExpression(
    LiteralExpressionSyntax node)
  {
    if (node.IsKind(SyntaxKind.TrueLiteralExpression)
        || node.IsKind(SyntaxKind.FalseLiteralExpression))
    {
      return SyntaxFactoryUtil.CreateMethodCall(
        "MutateCSharp", "Schemata", "ReplaceBooleanConstant",
        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
          SyntaxFactory.Literal(0)),
        node
      );
    }

    if (node.IsKind(SyntaxKind.NumericLiteralExpression))
    {
      var type = semanticModel.GetTypeInfo(node).Type?.SpecialType;
      if (type == SpecialType.System_Int32)
      {
        return SyntaxFactoryUtil.CreateMethodCall(
          "MutateCSharp", "Schemata", "ReplaceInt32Constant",
          SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
            SyntaxFactory.Literal(0)),
          node);
      }

      if (type == SpecialType.System_Double)
      {
        return SyntaxFactoryUtil.CreateMethodCall(
          "MutateCSharp", "Schemata", "ReplaceDoubleConstant",
          SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
            SyntaxFactory.Literal(0)),
          node);
      }
    }

    return base.VisitLiteralExpression(node);
  }
}