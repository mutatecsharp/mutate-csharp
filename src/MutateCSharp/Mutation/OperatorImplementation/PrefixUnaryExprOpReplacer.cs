using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.OperatorImplementation;

/*
 * Note: we handle updatable parameters differently from non-updatable parameters.
 * By default, since value types are passed by value in C# whereas
 * user-defined types are passed by reference, we take care to pass value types
 * by reference instead
 */
public sealed partial class PrefixUnaryExprOpReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractUnaryMutationOperator<PrefixUnaryExpressionSyntax>(
    sutAssembly, semanticModel)
{
  protected override bool CanBeApplied(PrefixUnaryExpressionSyntax originalNode)
  {
    Log.Debug("Processing prefix unary expression: {SyntaxNode}",
      originalNode.GetText().ToString());
    
    SyntaxNode[] nodes = [originalNode, originalNode.Operand];

    // Ignore: Cannot obtain type information
    if (nodes.Any(node =>
          !SyntaxRewriterUtil.IsTypeResolvableLogged(in SemanticModel, in node)))
      return false;

    var types = nodes.Select(node =>
      SemanticModel.ResolveTypeSymbol(node).GetNullableUnderlyingType()!);

    // Ignore: type contains generic type parameter
    return types.All(type =>
      !SyntaxRewriterUtil.ContainsGenericTypeParameterLogged(in type) 
      && type.GetVisibility() is not CodeAnalysisUtil.SymbolVisibility.Private
    ) && SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return $"{SupportedOperators[kind]}{{0}}";
  }

  protected override ExpressionRecord OriginalExpression(
    PrefixUnaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> _)
  {
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind()));
  }

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(PrefixUnaryExpressionSyntax originalNode)
  {
    // Perform additional filtering for assignable variables
    // TODO: move assignable logic validation to abstract class
    var validMutants = ValidMutants(originalNode).ToHashSet();
    if (!IsOperandAssignable(originalNode))
    {
      validMutants.Remove(SyntaxKind.PreIncrementExpression);
      validMutants.Remove(SyntaxKind.PreDecrementExpression);
    }

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
    PrefixUnaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions)
  {
    var operandType = SemanticModel.ResolveTypeSymbol(originalNode.Operand)!;
    if (!operandType.IsValueType)
      operandType = operandType.GetNullableUnderlyingType()!;

    // Check if any of original or mutant expressions update the argument
    return CodeAnalysisUtil.VariableModifyingOperators.Contains(
             originalNode.Kind())
           || mutantExpressions.Any(op =>
             CodeAnalysisUtil.VariableModifyingOperators.Contains(op.Operation))
      ? [$"ref {operandType.ToDisplayString()}"]
      : [operandType.ToDisplayString()];
  }

  protected override string ReturnType(PrefixUnaryExpressionSyntax originalNode)
  {
    return SemanticModel.ResolveTypeSymbol(originalNode)!.ToDisplayString();
  }

  protected override string SchemaBaseName(
    PrefixUnaryExpressionSyntax originalNode)
  {
    return $"ReplacePrefixUnaryExprOpReturn{ReturnType(originalNode)}";
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedUnaryOperators()
  {
    return SupportedOperators;
  }

  private bool IsOperandAssignable(PrefixUnaryExpressionSyntax originalNode)
  {
    return originalNode.Kind()
             is SyntaxKind.PreIncrementExpression
             or SyntaxKind.PreDecrementExpression
           || (SemanticModel.GetSymbolInfo(originalNode.Operand).Symbol?
             .IsSymbolVariable() ?? false);
  }
}

public sealed partial class PrefixUnaryExprOpReplacer
{
  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.Op>
      {
        {
          SyntaxKind.UnaryPlusExpression, // +x
          new(SyntaxKind.UnaryPlusExpression,
            SyntaxKind.PlusToken,
            WellKnownMemberNames.UnaryPlusOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.UnaryMinusExpression, // -x
          new(SyntaxKind.UnaryMinusExpression,
            SyntaxKind.MinusToken,
            WellKnownMemberNames.UnaryNegationOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            // https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/arithmetic-operators
            // ulong type does not support unary - operator.
            PrimitiveTypesToExclude: type => type is SpecialType.System_UInt64)
        },
        {
          SyntaxKind.BitwiseNotExpression, // ~x
          new(SyntaxKind.BitwiseNotExpression,
            SyntaxKind.TildeToken,
            WellKnownMemberNames.OnesComplementOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.LogicalNotExpression, // !x
          new(SyntaxKind.LogicalNotExpression,
            SyntaxKind.ExclamationToken,
            WellKnownMemberNames.LogicalNotOperatorName,
            CodeAnalysisUtil.BooleanLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.PreIncrementExpression, // ++x
          new(SyntaxKind.PreIncrementExpression,
            SyntaxKind.PlusPlusToken,
            WellKnownMemberNames.IncrementOperatorName,
            CodeAnalysisUtil.IncrementOrDecrementTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.PreDecrementExpression, // --x
          new(SyntaxKind.PreDecrementExpression,
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