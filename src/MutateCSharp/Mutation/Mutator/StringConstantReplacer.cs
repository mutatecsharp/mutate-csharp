using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.Mutator;

// Not to be confused with interpolated string instances
public class StringConstantReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(sutAssembly,
    semanticModel)
{
  private readonly ITypeSymbol _stringTypeSymbol =
    semanticModel.Compilation.GetSpecialType(SpecialType.System_String);

  private readonly ImmutableArray<ITypeSymbol> _stringOperandSymbol =
    [semanticModel.Compilation.GetSpecialType(SpecialType.System_String)];
    
  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    Log.Debug("Processing string constant: {SyntaxNode}", 
      originalNode.GetText().ToString());
    return originalNode.IsKind(SyntaxKind.StringLiteralExpression);
  }

  protected override ExpressionRecord OriginalExpression(
    LiteralExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _, ITypeSymbol? requiredReturnType)
    => new(originalNode.Kind(), "{0}");

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(LiteralExpressionSyntax originalNode, ITypeSymbol? requiredReturnType)
  {
    var result = new List<(int, ExpressionRecord)>();

    // Mutation: non-empty string constant => empty string constant
    if (originalNode.Token.ValueText.Length > 0)
      result.Add((1,
        new ExpressionRecord(SyntaxKind.StringLiteralExpression,
          "string.Empty")));

    return [..result];
  }

  protected override CodeAnalysisUtil.MethodSignature
    NonMutatedTypeSymbols(LiteralExpressionSyntax originalNode,
      ITypeSymbol? requiredReturnType)
  {
    return new CodeAnalysisUtil.MethodSignature(_stringTypeSymbol, _stringOperandSymbol);
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(LiteralExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType)
  {
    return ["string"];
  }

  protected override string SchemaReturnTypeDisplay(
    LiteralExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    return "string";
  }

  protected override string SchemaBaseName()
  {
    return "ReplaceStringConstant";
  }
}