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
    Log.Debug("Processing compound assignment: {SyntaxNode}",
      originalNode.GetText().ToString());

    SyntaxNode[] nodes = [originalNode.Left, originalNode.Right];

    // Ignore: Cannot obtain type information
    if (nodes.Any(node =>
          !SyntaxRewriterUtil.IsTypeResolvableLogged(in SemanticModel, in node)))
    return false;

    var types = nodes.Select(node =>
      SemanticModel.ResolveTypeSymbol(node).GetNullableUnderlyingType()!);

    // Ignore: type contains generic type parameter / is private
    return types.All(type => type.GetVisibility()
             is not CodeAnalysisUtil.SymbolVisibility.Private)
           && SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return $"{{0}} {SupportedOperators[kind]} {{1}}";
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedBinaryOperators()
  {
    return SupportedOperators;
  }

  protected override ExpressionRecord OriginalExpression(
    AssignmentExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
  {
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind()));
  }

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(AssignmentExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
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
    AssignmentExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _)
  {
    var updateVariableAbsoluteType =
      SemanticModel.ResolveTypeSymbol(originalNode.Left)
        .GetNullableUnderlyingType()!.ToDisplayString();
    var operandAbsoluteType =
      SemanticModel.ResolveTypeSymbol(originalNode.Right)
        .GetNullableUnderlyingType()!.ToDisplayString();
    return [$"ref {updateVariableAbsoluteType}", $"{operandAbsoluteType}"];
  }

  protected override string ReturnType(AssignmentExpressionSyntax originalNode)
  {
    return SemanticModel.ResolveTypeSymbol(originalNode)!.ToDisplayString();
  }

  protected override string SchemaBaseName(
    AssignmentExpressionSyntax originalNode)
  {
    return $"ReplaceCompoundAssignOp{ReturnType(originalNode)}";
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
  /*
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators#shift-count-of-the-shift-operators
   * For the built-in shift operators <<, >>, and >>>, the type of the
   * right-hand operand must be int or a type that has a predefined implicit \
   * numeric conversion to int.
   *
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/bitwise-and-shift-operators#shift-count-of-the-shift-operators
   * sbyte, byte, short, ushort, int has predefined implicit conversions to the
   * int type.
   */
  private static readonly Func<SpecialType, bool>
    ExcludeIfRightOperandNotImplicitlyConvertableToInt
      = specialType =>
        specialType is not
          (SpecialType.System_Char or
          SpecialType.System_SByte or 
          SpecialType.System_Byte or 
          SpecialType.System_Int16 or 
          SpecialType.System_UInt16 or
          SpecialType.System_Int32);
  
  // Both ExprKind and TokenKind represents the operator and are equivalent
  // ExprKind is used for Roslyn's Syntax API to determine the node expression kind
  // TokenKind is used by the lexer and to retrieve the string representation
  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.Op>
      {
        // Supported arithmetic assignment operations (+=, -=, *=, /=, %=)
        {
          SyntaxKind.AddAssignmentExpression,
          new(SyntaxKind.AddAssignmentExpression,
            SyntaxKind.PlusEqualsToken,
            WellKnownMemberNames.AdditionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.SubtractAssignmentExpression,
          new(SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MinusEqualsToken,
            WellKnownMemberNames.SubtractionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.MultiplyAssignmentExpression,
          new(SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.AsteriskEqualsToken,
            WellKnownMemberNames.MultiplyOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.DivideAssignmentExpression,
          new(SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.SlashEqualsToken,
            WellKnownMemberNames.DivisionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.ModuloAssignmentExpression,
          new(SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.PercentEqualsToken,
            WellKnownMemberNames.ModulusOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        // Supported boolean/integral bitwise logical operations (&=, |=, ^=)
        {
          SyntaxKind.AndAssignmentExpression,
          new(SyntaxKind.AndAssignmentExpression,
            SyntaxKind.AmpersandEqualsToken,
            WellKnownMemberNames.BitwiseAndOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.OrAssignmentExpression,
          new(SyntaxKind.OrAssignmentExpression,
            SyntaxKind.BarEqualsToken,
            WellKnownMemberNames.BitwiseOrOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.ExclusiveOrAssignmentExpression,
          new(SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.CaretEqualsToken,
            WellKnownMemberNames.ExclusiveOrOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        // Supported integral bitwise shift operations (<<=, >>=, >>>=)
        {
          SyntaxKind.LeftShiftAssignmentExpression,
          new(SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.LessThanLessThanEqualsToken,
            WellKnownMemberNames.LeftShiftOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature,
            PrimitiveTypesToExclude: ExcludeIfRightOperandNotImplicitlyConvertableToInt)
        },
        {
          SyntaxKind.RightShiftAssignmentExpression,
          new(SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.GreaterThanGreaterThanEqualsToken,
            WellKnownMemberNames.RightShiftOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature,
            PrimitiveTypesToExclude: ExcludeIfRightOperandNotImplicitlyConvertableToInt)
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
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}