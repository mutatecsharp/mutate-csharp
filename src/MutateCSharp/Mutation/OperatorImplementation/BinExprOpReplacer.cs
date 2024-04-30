using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class BinExprOpReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractBinaryMutationOperator<BinaryExpressionSyntax>(sutAssembly,
    semanticModel)
{
  protected override bool CanBeApplied(BinaryExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return $"{{0}} {SupportedOperators[kind]} {{1}}";
  }

  protected override ExpressionRecord OriginalExpression(
    BinaryExpressionSyntax originalNode)
  {
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind()));
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
    SupportedBinaryOperators()
  {
    return SupportedOperators;
  }

  protected override IList<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(BinaryExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return attachIdToMutants.Select(entry =>
        (entry.Item1,
          new ExpressionRecord(entry.Item2, ExpressionTemplate(entry.Item2))))
      .ToList();
  }

  protected override IList<string> ParameterTypes(
    BinaryExpressionSyntax originalNode)
  {
    var firstVariableType =
      SemanticModel.GetTypeInfo(originalNode.Left).Type!.ToDisplayString();
    var secondVariableType =
      SemanticModel.GetTypeInfo(originalNode.Right).Type!.ToDisplayString();
    return [firstVariableType, secondVariableType];
  }

  protected override string ReturnType(BinaryExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
  }

  protected override string SchemaBaseName(BinaryExpressionSyntax originalNode)
  {
    return $"ReplaceBinExprOpReturn{ReturnType(originalNode)}";
  }
}

/* Supported binary operators.
 *
 * More on C# operators and expressions:
 * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/
 */
public sealed partial class BinExprOpReplacer
{
  // Both ExprKind and TokenKind represents the operator and are equivalent
  // ExprKind is used for Roslyn's Syntax API to determine the node expression kind
  // TokenKind is used by the lexer and to retrieve the string representation

  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
      {
        // Supported arithmetic operations (+, -, *, /, %)
        {
          SyntaxKind.AddExpression,
          new(SyntaxKind.AddExpression,
            SyntaxKind.PlusToken,
            WellKnownMemberNames.AdditionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.SubtractExpression,
          new(SyntaxKind.SubtractExpression,
            SyntaxKind.MinusToken,
            WellKnownMemberNames.SubtractionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.MultiplyExpression,
          new(SyntaxKind.MultiplyExpression,
            SyntaxKind.AsteriskToken,
            WellKnownMemberNames.MultiplyOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.DivideExpression,
          new(SyntaxKind.DivideExpression,
            SyntaxKind.SlashToken,
            WellKnownMemberNames.DivisionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.ModuloExpression,
          new(SyntaxKind.ModuloExpression,
            SyntaxKind.PercentToken,
            WellKnownMemberNames.ModulusOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        // Supported boolean/integral bitwise logical operations (&, |, ^)
        {
          SyntaxKind.BitwiseAndExpression,
          new(SyntaxKind.BitwiseAndExpression,
            SyntaxKind.AmpersandToken,
            WellKnownMemberNames.BitwiseAndOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        {
          SyntaxKind.BitwiseOrExpression,
          new(SyntaxKind.BitwiseOrExpression,
            SyntaxKind.BarToken,
            WellKnownMemberNames.BitwiseOrOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        {
          SyntaxKind.ExclusiveOrExpression,
          new(SyntaxKind.ExclusiveOrExpression,
            SyntaxKind.CaretToken,
            WellKnownMemberNames.ExclusiveOrOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        // Supported boolean logical operations (&&, ||)
        {
          SyntaxKind.LogicalAndExpression,
          new(SyntaxKind.LogicalAndExpression,
            SyntaxKind.AmpersandAmpersandToken,
            WellKnownMemberNames.LogicalAndOperatorName,
            CodeAnalysisUtil.BooleanLogicalTypeSignature)
        },
        {
          SyntaxKind.LogicalOrExpression,
          new(SyntaxKind.LogicalOrExpression,
            SyntaxKind.BarBarToken,
            WellKnownMemberNames.LogicalOrOperatorName,
            CodeAnalysisUtil.BooleanLogicalTypeSignature)
        },
        // Supported integral bitwise shift operations (<<, >>, >>>)
        {
          SyntaxKind.LeftShiftExpression,
          new(SyntaxKind.LeftShiftExpression,
            SyntaxKind.LessThanLessThanToken,
            WellKnownMemberNames.LeftShiftOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature)
        },
        {
          SyntaxKind.RightShiftExpression,
          new(SyntaxKind.RightShiftExpression,
            SyntaxKind.GreaterThanGreaterThanToken,
            WellKnownMemberNames.RightShiftOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature)
        },
        // Note: .NET 6.0 does not support unsigned right shift operator
        // {
        //   SyntaxKind.UnsignedRightShiftExpression,
        //   new(SyntaxKind.UnsignedRightShiftExpression,
        //     SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
        //     WellKnownMemberNames.UnsignedRightShiftOperatorName,
        //     CodeAnalysisUtil.BitwiseShiftTypeSignature)
        // },
        // Supported equality comparison operators (==, !=)
        {
          SyntaxKind.EqualsExpression,
          new(SyntaxKind.EqualsExpression,
            SyntaxKind.EqualsEqualsToken,
            WellKnownMemberNames.EqualityOperatorName,
            CodeAnalysisUtil.EqualityTypeSignature)
        },
        {
          SyntaxKind.NotEqualsExpression,
          new(SyntaxKind.NotEqualsExpression,
            SyntaxKind.ExclamationEqualsToken,
            WellKnownMemberNames.InequalityOperatorName,
            CodeAnalysisUtil.EqualityTypeSignature)
        },
        // Supported inequality comparison operators (<, <=, >, >=)
        {
          SyntaxKind.LessThanExpression,
          new(SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanToken,
            WellKnownMemberNames.LessThanOperatorName,
            CodeAnalysisUtil.InequalityTypeSignature)
        },
        {
          SyntaxKind.LessThanOrEqualExpression,
          new(SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.LessThanEqualsToken,
            WellKnownMemberNames.LessThanOrEqualOperatorName,
            CodeAnalysisUtil.InequalityTypeSignature)
        },
        {
          SyntaxKind.GreaterThanExpression,
          new(SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanToken,
            WellKnownMemberNames.GreaterThanOperatorName,
            CodeAnalysisUtil.InequalityTypeSignature)
        },
        {
          SyntaxKind.GreaterThanOrEqualExpression,
          new(SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanEqualsToken,
            WellKnownMemberNames.GreaterThanOrEqualOperatorName,
            CodeAnalysisUtil.InequalityTypeSignature)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}