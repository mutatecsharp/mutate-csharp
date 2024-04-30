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
  Assembly sutAssembly,
  SemanticModel semanticModel) :
  AbstractBinaryMutationOperator<AssignmentExpressionSyntax>(sutAssembly,
    semanticModel)
{
  protected override bool CanBeApplied(AssignmentExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return $"{{0}} {SupportedOperators[kind]} {{1}}";
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
    SupportedBinaryOperators()
  {
    return SupportedOperators;
  }

  protected override ExpressionRecord OriginalExpression(
    AssignmentExpressionSyntax originalNode, IList<ExpressionRecord> _)
  {
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind()));
  }

  protected override IList<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(
      AssignmentExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return attachIdToMutants.Select(entry =>
      (entry.Item1,
        new ExpressionRecord(entry.Item2, ExpressionTemplate(entry.Item2))
      )
    ).ToList();
  }

  protected override IList<string> ParameterTypes(
    AssignmentExpressionSyntax originalNode, IList<ExpressionRecord> _)
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
    return "ReplaceCompoundAssignOp";
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
          new(SyntaxKind.AddAssignmentExpression,
            SyntaxKind.PlusEqualsToken,
            WellKnownMemberNames.AdditionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.SubtractAssignmentExpression,
          new(SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MinusEqualsToken,
            WellKnownMemberNames.SubtractionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.MultiplyAssignmentExpression,
          new(SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.AsteriskEqualsToken,
            WellKnownMemberNames.MultiplyOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.DivideAssignmentExpression,
          new(SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.SlashEqualsToken,
            WellKnownMemberNames.DivisionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.ModuloAssignmentExpression,
          new(SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.PercentEqualsToken,
            WellKnownMemberNames.ModulusOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        // Supported boolean/integral bitwise logical operations (&=, |=, ^=)
        {
          SyntaxKind.AndAssignmentExpression,
          new(SyntaxKind.AndAssignmentExpression,
            SyntaxKind.AmpersandEqualsToken,
            WellKnownMemberNames.BitwiseAndOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        {
          SyntaxKind.OrAssignmentExpression,
          new(SyntaxKind.OrAssignmentExpression,
            SyntaxKind.BarEqualsToken,
            WellKnownMemberNames.BitwiseOrOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        {
          SyntaxKind.ExclusiveOrAssignmentExpression,
          new(SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.CaretEqualsToken,
            WellKnownMemberNames.ExclusiveOrOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        // Supported integral bitwise shift operations (<<=, >>=, >>>=)
        {
          SyntaxKind.LeftShiftAssignmentExpression,
          new(SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.LessThanLessThanEqualsToken,
            WellKnownMemberNames.LeftShiftOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature)
        },
        {
          SyntaxKind.RightShiftAssignmentExpression,
          new(SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.GreaterThanGreaterThanEqualsToken,
            WellKnownMemberNames.RightShiftOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature)
        },
        // .NET 6.0 does not support unsigned right shift assignment operator
        // {
        //   SyntaxKind.UnsignedRightShiftAssignmentExpression,
        //   new(SyntaxKind.UnsignedRightShiftAssignmentExpression,
        //     SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken,
        //     WellKnownMemberNames.UnsignedRightShiftOperatorName,
        //     CodeAnalysisUtil.BitwiseShiftTypeSignature)
        // }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}