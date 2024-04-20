using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class BinExprOpReplacer(Assembly sutAssembly, SemanticModel semanticModel)
  : AbstractBinaryMutationOperator<BinaryExpressionSyntax>(sutAssembly, semanticModel)
{
  protected override bool CanBeApplied(BinaryExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return $"{{0}} {SupportedOperators[kind]} {{1}}";
  }

  protected override string OriginalExpressionTemplate(
    BinaryExpressionSyntax originalNode)
  {
    return ExpressionTemplate(originalNode.Kind());
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp> SupportedBinaryOperators()
  {
    return SupportedOperators;
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    BinaryExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return attachIdToMutants.Select(entry =>
        (entry.Item1, ExpressionTemplate(entry.Item2)))
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

  protected override string SchemaBaseName(BinaryExpressionSyntax _)
  {
    return "ReplaceBinaryExpressionOperator";
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

  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp> SupportedOperators
    = new Dictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
    {
      // Supported arithmetic operations (+, -, *, /, %)
      {
        SyntaxKind.AddExpression,
        new(ExprKind: SyntaxKind.AddExpression, 
          TokenKind: SyntaxKind.PlusToken,
          MemberName: WellKnownMemberNames.AdditionOperatorName,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      {
        SyntaxKind.SubtractExpression,
        new(ExprKind: SyntaxKind.SubtractExpression, 
          TokenKind: SyntaxKind.MinusToken,
          MemberName: WellKnownMemberNames.SubtractionOperatorName,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      {
        SyntaxKind.MultiplyExpression,
        new(ExprKind: SyntaxKind.MultiplyExpression, 
          TokenKind: SyntaxKind.AsteriskToken,
          MemberName: WellKnownMemberNames.MultiplyOperatorName,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      {
        SyntaxKind.DivideExpression,
        new(ExprKind: SyntaxKind.DivideExpression, 
          TokenKind: SyntaxKind.SlashToken,
          MemberName: WellKnownMemberNames.DivisionOperatorName,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      {
        SyntaxKind.ModuloExpression,
        new(ExprKind: SyntaxKind.ModuloExpression, 
          TokenKind: SyntaxKind.PercentToken,
          MemberName: WellKnownMemberNames.ModulusOperatorName,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      // Supported boolean/integral bitwise logical operations (&, |, ^)
      {
        SyntaxKind.BitwiseAndExpression,
        new(ExprKind: SyntaxKind.BitwiseAndExpression, 
          TokenKind: SyntaxKind.AmpersandToken,
          MemberName: WellKnownMemberNames.BitwiseAndOperatorName,
          TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
      },
      {
        SyntaxKind.BitwiseOrExpression,
        new(ExprKind: SyntaxKind.BitwiseOrExpression, 
          TokenKind: SyntaxKind.BarToken,
          MemberName: WellKnownMemberNames.BitwiseOrOperatorName,
          TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
      },
      {
        SyntaxKind.ExclusiveOrExpression,
        new(ExprKind: SyntaxKind.ExclusiveOrExpression, 
          TokenKind: SyntaxKind.CaretToken,
          MemberName: WellKnownMemberNames.ExclusiveOrOperatorName,
          TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
      },
      // Supported boolean logical operations (&&, ||)
      {
        SyntaxKind.LogicalAndExpression,
        new(ExprKind: SyntaxKind.LogicalAndExpression, 
          TokenKind: SyntaxKind.AmpersandAmpersandToken,
          MemberName: WellKnownMemberNames.LogicalAndOperatorName,
          TypeSignatures: CodeAnalysisUtil.BooleanLogicalTypeSignature)
      },
      {
        SyntaxKind.LogicalOrExpression,
        new(ExprKind: SyntaxKind.LogicalOrExpression, 
          TokenKind: SyntaxKind.BarBarToken,
          MemberName: WellKnownMemberNames.LogicalOrOperatorName,
          TypeSignatures: CodeAnalysisUtil.BooleanLogicalTypeSignature)
      },
      // Supported integral bitwise shift operations (<<, >>, >>>)
      {
        SyntaxKind.LeftShiftExpression,
        new(ExprKind: SyntaxKind.LeftShiftExpression,
          TokenKind: SyntaxKind.LessThanLessThanToken,
          MemberName: WellKnownMemberNames.LeftShiftOperatorName,
          TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
      },
      {
        SyntaxKind.RightShiftExpression,
        new(ExprKind: SyntaxKind.RightShiftExpression,
          TokenKind: SyntaxKind.GreaterThanGreaterThanToken,
          MemberName: WellKnownMemberNames.RightShiftOperatorName,
          TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
      },
      {
        SyntaxKind.UnsignedRightShiftExpression,
        new(ExprKind: SyntaxKind.UnsignedRightShiftExpression,
          TokenKind: SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
          MemberName: WellKnownMemberNames.UnsignedRightShiftOperatorName,
          TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
      },
      // Supported equality comparison operators (==, !=)
      {
        SyntaxKind.EqualsExpression,
        new(ExprKind: SyntaxKind.EqualsExpression, 
          TokenKind: SyntaxKind.EqualsEqualsToken,
          MemberName: WellKnownMemberNames.EqualityOperatorName,
          TypeSignatures: CodeAnalysisUtil.EqualityTypeSignature)
      },
      {
        SyntaxKind.NotEqualsExpression,
        new(ExprKind: SyntaxKind.NotEqualsExpression, 
          TokenKind: SyntaxKind.ExclamationEqualsToken,
          MemberName: WellKnownMemberNames.InequalityOperatorName,
          TypeSignatures: CodeAnalysisUtil.EqualityTypeSignature)
      },
      // Supported inequality comparison operators (<, <=, >, >=)
      {
        SyntaxKind.LessThanExpression,
        new(ExprKind: SyntaxKind.LessThanExpression, 
          TokenKind: SyntaxKind.LessThanToken,
          MemberName: WellKnownMemberNames.LessThanOperatorName,
          TypeSignatures: CodeAnalysisUtil.InequalityTypeSignature)
      },
      {
        SyntaxKind.LessThanOrEqualExpression,
        new(ExprKind: SyntaxKind.LessThanOrEqualExpression,
          TokenKind: SyntaxKind.LessThanEqualsToken,
          MemberName: WellKnownMemberNames.LessThanOrEqualOperatorName,
          TypeSignatures: CodeAnalysisUtil.InequalityTypeSignature)
      },
      {
        SyntaxKind.GreaterThanExpression,
        new(ExprKind: SyntaxKind.GreaterThanExpression, 
          TokenKind: SyntaxKind.GreaterThanToken,
          MemberName: WellKnownMemberNames.GreaterThanOperatorName,
          TypeSignatures: CodeAnalysisUtil.InequalityTypeSignature)
      },
      {
        SyntaxKind.GreaterThanOrEqualExpression,
        new(ExprKind: SyntaxKind.GreaterThanOrEqualExpression,
          TokenKind: SyntaxKind.GreaterThanEqualsToken,
          MemberName: WellKnownMemberNames.GreaterThanOrEqualOperatorName,
          TypeSignatures: CodeAnalysisUtil.InequalityTypeSignature)
      }
    }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}