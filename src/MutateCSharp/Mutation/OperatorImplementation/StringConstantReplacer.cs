using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serilog;

namespace MutateCSharp.Mutation.OperatorImplementation;

// Not to be confused with interpolated string instances
public class StringConstantReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(sutAssembly,
    semanticModel)
{
  private static readonly ImmutableArray<string> ParameterType = ["string"];
  
  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    Log.Debug("Processing string constant: {SyntaxNode}", 
      originalNode.GetText().ToString());
    return originalNode.IsKind(SyntaxKind.StringLiteralExpression);
  }

  protected override ExpressionRecord OriginalExpression(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
    => new(originalNode.Kind(), "{0}");

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(LiteralExpressionSyntax originalNode)
  {
    var result = new List<(int, ExpressionRecord)>();

    // Mutation: non-empty string constant => empty string constant
    if (originalNode.Token.ValueText.Length > 0)
      result.Add((1,
        new ExpressionRecord(SyntaxKind.StringLiteralExpression,
          "string.Empty")));

    return [..result];
  }

  protected override ImmutableArray<string> ParameterTypes(
    LiteralExpressionSyntax _, ImmutableArray<ExpressionRecord> __)
  {
    return ParameterType;
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