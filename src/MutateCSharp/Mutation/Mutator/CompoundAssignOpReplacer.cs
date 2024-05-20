using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.Mutator;

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
  SemanticModel semanticModel,
  FrozenDictionary<SyntaxKind,
    ImmutableArray<CodeAnalysisUtil.MethodSignature>> builtInOperatorSignatures)
  :
    AbstractBinaryMutationOperator<AssignmentExpressionSyntax>(sutAssembly,
      semanticModel, builtInOperatorSignatures)
{
  protected override bool CanBeApplied(AssignmentExpressionSyntax originalNode)
  {
    Log.Debug("Processing compound assignment: {SyntaxNode}",
      originalNode.GetText().ToString());

    // The return type of the compound assignment expression
    // of the form x (op)= y is the same as the left operand
    SyntaxNode[] nodes = [originalNode.Left, originalNode.Right];

    // Ignore: Cannot obtain type information
    if (nodes.Any(node =>
          !SyntaxRewriterUtil.IsTypeResolvableLogged(in SemanticModel,
            in node)))
      return false;

    var types = nodes.Select(node =>
      SemanticModel.ResolveTypeSymbol(node)!.GetNullableUnderlyingType());

    // Exclude node from mutation:
    // 1) If type contains generic type parameter;
    // 2) If type is private (and thus inaccessible).
    return types.All(type =>
      !SyntaxRewriterUtil.ContainsGenericTypeParameterLogged(in type)
      && type.GetVisibility() is not CodeAnalysisUtil.SymbolVisibility.Private
    ) && SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return kind.IsSyntaxKindLiteral() 
      ? SupportedOperators[kind].ToString() 
      : $"{{0}} {SupportedOperators[kind]} {{1}}";
  }

  protected override ExpressionRecord OriginalExpression(
    AssignmentExpressionSyntax originalNode, ImmutableArray<ExpressionRecord> _,
    ITypeSymbol? requiredReturnType)
  {
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind()));
  }

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(AssignmentExpressionSyntax originalNode,
      ITypeSymbol? requiredReturnType)
  {
    var validMutants = ValidMutants(originalNode, requiredReturnType);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return
    [
      ..attachIdToMutants.Select(entry =>
        (entry.id,
          new ExpressionRecord(entry.op, ExpressionTemplate(entry.op))
        )
      )
    ];
  }

  /*
   * A special case of equality expressions are of the form x == null, which
   * we assign the null type to object. Any reference type inherits the object
   * type, and there is an overload for the operator== and operator!= for the
   * object class that calls the type equals operator in the default implementation,
   * which allows the compilation to succeed.
   *
   * Since either left or right operand can be a value type, and that the
   * operator== is not defined between any value type V and an object type, we
   * take care to define the null type as V? where there *is* an operator==
   * defined for the two V? operands, when x is of value type.
   */
  protected override CodeAnalysisUtil.MethodSignature?
    NonMutatedTypeSymbols(AssignmentExpressionSyntax originalNode,
      ITypeSymbol? requiredReturnType)
  {
    // It is guaranteed that the updateVariableType is the exact type, as it
    // is a variable and not a literal
    var updateVariableType =
      SemanticModel.ResolveTypeSymbol(originalNode.Left)!;
    var operandType = SemanticModel.ResolveTypeSymbol(originalNode.Right)!;

    var varAbsoluteType = updateVariableType.GetNullableUnderlyingType();
    var operandAbsoluteType = operandType.GetNullableUnderlyingType();

    // It is guaranteed the left operand is an assignable variable, not a literal
    if (varAbsoluteType.IsNumeric() && operandAbsoluteType.IsNumeric())
    {
      // If right operand is literal, check for narrowing
      if (originalNode.Right.IsKind(SyntaxKind.NumericLiteralExpression)
          && SemanticModel.CanImplicitlyConvertNumericLiteral(
            originalNode.Right, varAbsoluteType.SpecialType))
      {
        operandType = varAbsoluteType;
      }
      
      // Construct method signature
      var paramTypes = new CodeAnalysisUtil.MethodSignature(updateVariableType,
        [updateVariableType, operandType]);

      if (SemanticModel.ResolveOverloadedPredefinedBinaryOperator(
            BuiltInOperatorSignatures,
            originalNode.Kind(), paramTypes) is not { } result)
        return null; // Fail to resolve overloads

      return new CodeAnalysisUtil.MethodSignature(result.returnSymbol,
        [result.leftSymbol, result.rightSymbol]);
    }

    // Check if null keyword exists in binary expression
    // Since null does not have a type in C#, the helper method resolves it to
    // the object type by default. We intercept the resolved type and change it
    // to the corresponding nullable type if the other operand is a value type.
    // Addendum: any compound assignment operation with null yields null;
    // it is thus redundant so we filter this out
    if (updateVariableType.IsValueType &&
        originalNode.Right.IsKind(SyntaxKind.NullLiteralExpression))
    {
      return null;
    }

    // Either left or right operand is reference type; null can be of object type
    // Remove nullable since a reference type T? can be cast to T
    if (!updateVariableType.IsValueType)
      updateVariableType = updateVariableType.GetNullableUnderlyingType();

    if (!operandType.IsValueType)
      operandType = operandType.GetNullableUnderlyingType();

    return new CodeAnalysisUtil.MethodSignature(updateVariableType,
      [updateVariableType, operandType]);
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(
    AssignmentExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } typeSymbols) return [];
    return
    [
      $"ref {typeSymbols.ReturnType.ToDisplayString()}",
      typeSymbols.OperandTypes[1].ToDisplayString()
    ];
  }

  protected override string SchemaReturnTypeDisplay(
    AssignmentExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    return NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
      { } typeSymbols
      ? string.Empty
      : typeSymbols.ReturnType.ToDisplayString();
  }

  protected override string SchemaBaseName()
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
            WellKnownMemberNames.AdditionOperatorName)
        },
        {
          SyntaxKind.SubtractAssignmentExpression,
          new(SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MinusEqualsToken,
            WellKnownMemberNames.SubtractionOperatorName)
        },
        {
          SyntaxKind.MultiplyAssignmentExpression,
          new(SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.AsteriskEqualsToken,
            WellKnownMemberNames.MultiplyOperatorName)
        },
        {
          SyntaxKind.DivideAssignmentExpression,
          new(SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.SlashEqualsToken,
            WellKnownMemberNames.DivisionOperatorName)
        },
        {
          SyntaxKind.ModuloAssignmentExpression,
          new(SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.PercentEqualsToken,
            WellKnownMemberNames.ModulusOperatorName)
        },
        // Supported boolean/integral bitwise logical operations (&=, |=, ^=)
        {
          SyntaxKind.AndAssignmentExpression,
          new(SyntaxKind.AndAssignmentExpression,
            SyntaxKind.AmpersandEqualsToken,
            WellKnownMemberNames.BitwiseAndOperatorName)
        },
        {
          SyntaxKind.OrAssignmentExpression,
          new(SyntaxKind.OrAssignmentExpression,
            SyntaxKind.BarEqualsToken,
            WellKnownMemberNames.BitwiseOrOperatorName)
        },
        {
          SyntaxKind.ExclusiveOrAssignmentExpression,
          new(SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.CaretEqualsToken,
            WellKnownMemberNames.ExclusiveOrOperatorName)
        },
        // Supported integral bitwise shift operations (<<=, >>=, >>>=)
        {
          SyntaxKind.LeftShiftAssignmentExpression,
          new(SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.LessThanLessThanEqualsToken,
            WellKnownMemberNames.LeftShiftOperatorName)
        },
        {
          SyntaxKind.RightShiftAssignmentExpression,
          new(SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.GreaterThanGreaterThanEqualsToken,
            WellKnownMemberNames.RightShiftOperatorName)
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


  protected override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedBinaryOperators() => SupportedOperators;

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}