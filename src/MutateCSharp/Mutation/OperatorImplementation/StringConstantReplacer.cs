using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Mutation.OperatorImplementation;

// Not to be confused with interpolated string instances
public class StringConstantReplacer(Assembly sutAssembly, SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(sutAssembly, semanticModel)
{
  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    return originalNode.IsKind(SyntaxKind.StringLiteralExpression);
  }

  protected override string OriginalExpressionTemplate(
    LiteralExpressionSyntax originalNode)
  {
    return "{0}";
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    LiteralExpressionSyntax originalNode)
  {
    var result = new List<(int, string)>();

    // Mutation: non-empty string constant => empty string constant
    if (originalNode.Token.ValueText.Length > 0)
      result.Add((1, "string.Empty"));

    return result;
  }

  protected override IList<string> ParameterTypes(
    LiteralExpressionSyntax originalNode)
  {
    return ImmutableArray.Create(ReturnType(originalNode));
  }

  protected override string ReturnType(LiteralExpressionSyntax _)
  {
    return "string";
  }

  protected override string SchemaBaseName(LiteralExpressionSyntax _)
  {
    return "ReplaceStringConstant";
  }
}