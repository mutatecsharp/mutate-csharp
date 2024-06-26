using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.Mutator;

public sealed partial class PostfixUnaryExprOpReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel,
  FrozenDictionary<SyntaxKind,
    ImmutableArray<CodeAnalysisUtil.MethodSignature>> builtInOperatorSignatures,
  bool optimise)
  : AbstractUnaryMutationOperator<PostfixUnaryExpressionSyntax>(
    sutAssembly, semanticModel, builtInOperatorSignatures)
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
      SemanticModel.ResolveTypeSymbol(node)!.GetNullableUnderlyingType()!);

    // Ignore: type contains generic type parameter / is private
    return types.All(type =>
      !SyntaxRewriterUtil.ContainsGenericTypeParameterLogged(in type) 
      && type.GetVisibility() is not CodeAnalysisUtil.SymbolVisibility.Private
    ) && SupportedOperators.ContainsKey(originalNode.Kind());
  }

  public static string ExpressionTemplate(SyntaxKind kind)
  {
    if (kind.IsSyntaxKindLiteral()) return SupportedOperators[kind].ToString();
    if (kind.IsSyntaxKindPrefixOperator()) return $"{SupportedOperators[kind]}{{0}}";
    return $"{{0}}{SupportedOperators[kind]}";
  }

  protected override ExpressionRecord OriginalExpression(
    PostfixUnaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, 
    ITypeSymbol? requiredReturnType)
  {
    return new ExpressionRecord(originalNode.Kind(),
      CodeAnalysisUtil.OperandKind.None,
      ExpressionTemplate(originalNode.Kind()));
  }

  protected override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedUnaryOperators()
  {
    return SupportedOperators;
  }

  protected override
    ImmutableArray<ExpressionRecord>
    ValidMutantExpressions(PostfixUnaryExpressionSyntax originalNode, 
      ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } methodSignature) return [];
    
    var validMutants = ValidOperatorReplacements(originalNode, methodSignature, optimise);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    var sortedMutants = attachIdToMutants.Select(entry =>
      new ExpressionRecord(entry.op, CodeAnalysisUtil.OperandKind.None, ExpressionTemplate(entry.Item2))
    ).ToList();
    
    // Append operand as mutant
    var validOperand =
      ValidOperandReplacement(originalNode, methodSignature, optimise);
    if (validOperand.Contains(CodeAnalysisUtil.OperandKind.UnaryOperand))
    {
      var operand = new ExpressionRecord(SyntaxKind.Argument,
        CodeAnalysisUtil.OperandKind.UnaryOperand,
        "{0}");
      sortedMutants.Add(operand);
    }

    return [..sortedMutants];
  }

  protected override CodeAnalysisUtil.MethodSignature
    NonMutatedTypeSymbols(PostfixUnaryExpressionSyntax originalNode,
      ITypeSymbol? requiredReturnType)
  {
    var operandType = SemanticModel.ResolveTypeSymbol(originalNode.Operand)!;
    var returnType = SemanticModel.ResolveTypeSymbol(originalNode)!;
    // Don't have to check for the null keyword since no unary operator is
    // applicable to the null keyword (null has no type in C#)
    
    // Don't have to resolve for numeric literals; it is guaranteed the operand
    // is assignable variable
    
    // Remove nullable if operand is of reference type, since reference type
    // T? can be cast to T
    if (!operandType.IsValueType)
      operandType = operandType.GetNullableUnderlyingType();

    return new CodeAnalysisUtil.MethodSignature(returnType, [operandType]);
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(
    PostfixUnaryExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    // Since the supported postfix unary expressions can be either 
    // postincrement or postdecrement, they are guaranteed to be updatable
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } methodSignature) return [];
    return [$"ref {methodSignature.OperandTypes[0]}"];
  }

  protected override string SchemaReturnTypeDisplay(
    PostfixUnaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    return NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
      { } typeSignature ? string.Empty : typeSignature.ReturnType.ToDisplayString();
  }

  protected override string SchemaBaseName()
  {
    return "ReplacePostfixUnaryExprOp";
  }
}

public sealed partial class PostfixUnaryExprOpReplacer
{
  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.Op>
      {
        {
          SyntaxKind.PostIncrementExpression, // x++
          new(SyntaxKind.PostIncrementExpression,
            SyntaxKind.PlusPlusToken,
            WellKnownMemberNames.IncrementOperatorName)
        },
        {
          SyntaxKind.PostDecrementExpression, //x--
          new(SyntaxKind.PostDecrementExpression,
            SyntaxKind.MinusMinusToken,
            WellKnownMemberNames.DecrementOperatorName)
        },
        // Prefix unary operators
        {
          SyntaxKind.UnaryPlusExpression, // +x
          new(SyntaxKind.UnaryPlusExpression,
            SyntaxKind.PlusToken,
            WellKnownMemberNames.UnaryPlusOperatorName)
        },
        {
          SyntaxKind.UnaryMinusExpression, // -x
          new(SyntaxKind.UnaryMinusExpression,
            SyntaxKind.MinusToken,
            WellKnownMemberNames.UnaryNegationOperatorName)
        },
        {
          SyntaxKind.BitwiseNotExpression, // ~x
          new(SyntaxKind.BitwiseNotExpression,
            SyntaxKind.TildeToken,
            WellKnownMemberNames.OnesComplementOperatorName)
        }
        // Note: this will never materialise because boolean variables cannot
        // be updated, so it is omitted
        // {
        //   SyntaxKind.LogicalNotExpression, // !x
        //   new(SyntaxKind.LogicalNotExpression,
        //     SyntaxKind.ExclamationToken,
        //     WellKnownMemberNames.LogicalNotOperatorName)
        // },
        // Note: this will never materialise because boolean variables cannot
        // be updated, so it is omitted
        // Boolean literals (true, false)
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}