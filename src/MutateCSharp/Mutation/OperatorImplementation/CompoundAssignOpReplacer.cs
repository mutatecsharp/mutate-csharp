using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

/*
 * Compound assignment
 * x += y;
 * is equivalent to
 * x = x + y;
 *
 * Compound assignment operators cannot be overloaded;
 * its definition will be inferred from the corresponding
 * binary operator. (eg: += from +)
 */
public sealed partial class CompoundAssignOpReplacer(
  Assembly sutAssembly, SemanticModel semanticModel) :
  AbstractBinaryMutationOperator<AssignmentExpressionSyntax>(sutAssembly, semanticModel)
{
  protected override bool CanBeApplied(AssignmentExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }
  
  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return $"{{0}} {SupportedOperators[kind]} {{1}}";
  }
  
  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp> SupportedBinaryOperators()
  {
    return SupportedOperators;
  }

  protected override string OriginalExpressionTemplate(
    AssignmentExpressionSyntax originalNode)
  {
    return ExpressionTemplate(originalNode.Kind());
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    AssignmentExpressionSyntax originalNode)
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
    AssignmentExpressionSyntax originalNode)
  {
    var firstVariableType =
      SemanticModel.GetTypeInfo(originalNode.Left).Type!.ToDisplayString();
    var secondVariableType =
      SemanticModel.GetTypeInfo(originalNode.Right).Type!.ToDisplayString();
    return [$"ref {firstVariableType}", secondVariableType];
  }
  
  // Void type since operator updates value in place
  protected override string ReturnType(AssignmentExpressionSyntax _)
  {
    return "void";
  }

  protected override string SchemaBaseName(AssignmentExpressionSyntax _)
  {
    return "ReplaceCompoundAssignmentOperator";
  }
}

/* Supported compound assignment operators.
 *
 * More on C# operators and expressions:
 * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/
 * https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.csharp.syntax.assignmentexpressionsyntax?view=roslyn-dotnet-4.7.0
 */
public sealed partial class CompoundAssignOpReplacer
{
  // Both ExprKind and TokenKind represents the operator and are equivalent
  // ExprKind is used for Roslyn's Syntax API to determine the node expression kind
  // TokenKind is used by the lexer and to retrieve the string representation
  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
      {
        // Supported arithmetic assignment operations (+=, -=, *=, /=, %=)
        {
          SyntaxKind.AddAssignmentExpression,
          new(ExprKind: SyntaxKind.AddAssignmentExpression,
          TokenKind: SyntaxKind.PlusEqualsToken,
          MemberName: WellKnownMemberNames.AdditionOperatorName,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.SubtractAssignmentExpression,
          new(ExprKind: SyntaxKind.SubtractAssignmentExpression,
            TokenKind: SyntaxKind.MinusEqualsToken,
            MemberName: WellKnownMemberNames.SubtractionOperatorName,
            TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.MultiplyAssignmentExpression,
          new(ExprKind: SyntaxKind.MultiplyAssignmentExpression,
            TokenKind: SyntaxKind.AsteriskEqualsToken,
            MemberName: WellKnownMemberNames.MultiplyOperatorName,
            TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.DivideAssignmentExpression,
          new(ExprKind: SyntaxKind.DivideAssignmentExpression,
            TokenKind: SyntaxKind.SlashEqualsToken,
            MemberName: WellKnownMemberNames.DivisionOperatorName,
            TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.ModuloAssignmentExpression,
          new(ExprKind: SyntaxKind.ModuloAssignmentExpression,
            TokenKind: SyntaxKind.PercentEqualsToken,
            MemberName: WellKnownMemberNames.ModulusOperatorName,
            TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        // Supported boolean/integral bitwise logical operations (&=, |=, ^=)
        {
          SyntaxKind.AndAssignmentExpression,
          new(ExprKind: SyntaxKind.AndAssignmentExpression,
            TokenKind: SyntaxKind.AmpersandEqualsToken,
            MemberName: WellKnownMemberNames.BitwiseAndOperatorName,
            TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        {
          SyntaxKind.OrAssignmentExpression,
          new(ExprKind: SyntaxKind.OrAssignmentExpression,
            TokenKind: SyntaxKind.BarEqualsToken,
            MemberName: WellKnownMemberNames.BitwiseOrOperatorName,
            TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        {
          SyntaxKind.ExclusiveOrAssignmentExpression,
          new(ExprKind: SyntaxKind.ExclusiveOrAssignmentExpression,
            TokenKind: SyntaxKind.CaretEqualsToken,
            MemberName: WellKnownMemberNames.ExclusiveOrOperatorName,
            TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        // Supported integral bitwise shift operations (<<=, >>=, >>>=)
        {
          SyntaxKind.LeftShiftAssignmentExpression,
          new(ExprKind: SyntaxKind.LeftShiftAssignmentExpression,
            TokenKind: SyntaxKind.LessThanLessThanEqualsToken,
            MemberName: WellKnownMemberNames.LeftShiftOperatorName,
            TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
        },
        {
          SyntaxKind.RightShiftAssignmentExpression,
          new(ExprKind: SyntaxKind.RightShiftAssignmentExpression,
            TokenKind: SyntaxKind.GreaterThanGreaterThanEqualsToken,
            MemberName: WellKnownMemberNames.RightShiftOperatorName,
            TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
        },
        {
          SyntaxKind.UnsignedRightShiftAssignmentExpression,
          new(ExprKind: SyntaxKind.UnsignedRightShiftAssignmentExpression,
            TokenKind: SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken,
            MemberName: WellKnownMemberNames.UnsignedRightShiftOperatorName,
            TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
        }
      }.ToFrozenDictionary();
  
  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys).ToFrozenDictionary();
}

// SyntaxKind.AddAssignmentExpression,
// SyntaxKind.SubtractAssignmentExpression,
// SyntaxKind.MultiplyAssignmentExpression,
// SyntaxKind.DivideAssignmentExpression,
// SyntaxKind.ModuloAssignmentExpression,
// SyntaxKind.AndAssignmentExpression,
// SyntaxKind.ExclusiveOrAssignmentExpression,
// SyntaxKind.OrAssignmentExpression,
// SyntaxKind.LeftShiftAssignmentExpression,
// SyntaxKind.RightShiftAssignmentExpression,
// SyntaxKind.UnsignedRightShiftAssignmentExpression,