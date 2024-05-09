using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class BinExprOpReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractBinaryMutationOperator<BinaryExpressionSyntax>(sutAssembly,
    semanticModel)
{
  protected override bool CanBeApplied(BinaryExpressionSyntax originalNode)
  {
    Log.Debug("Processing binary expression: {SyntaxNode}",
      originalNode.GetText().ToString());

    SyntaxNode[] nodes = [originalNode, originalNode.Left, originalNode.Right];

    // Ignore: Cannot obtain type information
    if (nodes.Any(node =>
          !SyntaxRewriterUtil.IsTypeResolvableLogged(in SemanticModel,
            in node)))
      return false;

    var types = nodes.Select(node =>
      SemanticModel.ResolveTypeSymbol(node).GetNullableUnderlyingType()!);

    // Ignore: type contains generic type parameter / is private
    return types.All(type => type.GetVisibility()
             is not CodeAnalysisUtil.SymbolVisibility.Private)
           && SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind, bool isLeftDelegate,
    bool isRightDelegate)
  {
    var insertLeftInvocation = isLeftDelegate ? "()" : string.Empty;
    var insertRightInvocation = isRightDelegate ? "()" : string.Empty;
    return
      $"{{0}}{insertLeftInvocation} {SupportedOperators[kind]} {{1}}{insertRightInvocation}";
  }

  protected override ExpressionRecord OriginalExpression(
    BinaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions)
  {
    var containsShortCircuitOperators =
      CodeAnalysisUtil.ShortCircuitOperators.Contains(originalNode.Kind())
      || mutantExpressions.Any(mutant =>
        CodeAnalysisUtil.ShortCircuitOperators.Contains(mutant.Operation));

    var leftSymbol = SemanticModel.GetSymbolInfo(originalNode.Left).Symbol!;
    var rightSymbol = SemanticModel.GetSymbolInfo(originalNode.Right).Symbol!;

    var isLeftDelegate = containsShortCircuitOperators
                         && CodeAnalysisUtil.OperandCanBeDelegate(leftSymbol);
    var isRightDelegate = containsShortCircuitOperators
                          && CodeAnalysisUtil.OperandCanBeDelegate(rightSymbol);

    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind(), isLeftDelegate, isRightDelegate));
  }

  public override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedBinaryOperators() => SupportedOperators;

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(BinaryExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode).ToArray();

    var containsShortCircuitOperators =
      CodeAnalysisUtil.ShortCircuitOperators.Contains(originalNode.Kind()) ||
      validMutants.Any(op =>
        CodeAnalysisUtil.ShortCircuitOperators.Contains(op));

    var leftSymbol = SemanticModel.GetSymbolInfo(originalNode.Left).Symbol!;
    var rightSymbol = SemanticModel.GetSymbolInfo(originalNode.Right).Symbol!;

    var isLeftDelegate = containsShortCircuitOperators
                         && CodeAnalysisUtil.OperandCanBeDelegate(leftSymbol);
    var isRightDelegate = containsShortCircuitOperators
                          && CodeAnalysisUtil.OperandCanBeDelegate(rightSymbol);

    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);

    return
    [
      ..attachIdToMutants.Select(entry =>
        (entry.id, new ExpressionRecord(entry.op,
          ExpressionTemplate(entry.op, isLeftDelegate, isRightDelegate))
        )
      )
    ];
  }

  // C# allows short-circuit evaluation for boolean && and || operators.
  // To preserve the semantics in mutant schemata, we defer the expression
  // evaluation by wrapping the expression in lambda iff any of the original
  // expression or mutant expressions involve the use of && or ||
  protected override ImmutableArray<string> ParameterTypes(
    BinaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions)
  {
    var leftOperandAbsoluteType =
      SemanticModel.ResolveTypeSymbol(originalNode.Left)
        .GetNullableUnderlyingType()!.ToDisplayString();
    var rightOperandAbsoluteType =
      SemanticModel.ResolveTypeSymbol(originalNode.Right)
        .GetNullableUnderlyingType()!.ToDisplayString();

    // Check for short circuit operators
    var containsShortCircuitOperators =
      CodeAnalysisUtil.ShortCircuitOperators.Contains(originalNode.Kind())
      || mutantExpressions.Any(mutant =>
        CodeAnalysisUtil.ShortCircuitOperators.Contains(mutant.Operation));

    // Short-circuit evaluation operators are present
    var leftOperandSymbol =
      SemanticModel.GetSymbolInfo(originalNode.Left).Symbol!;
    var rightOperandSymbol =
      SemanticModel.GetSymbolInfo(originalNode.Right).Symbol!;
    
    var isLeftDelegate = containsShortCircuitOperators
                         && CodeAnalysisUtil.OperandCanBeDelegate(leftOperandSymbol);
    var isRightDelegate = containsShortCircuitOperators
                          && CodeAnalysisUtil.OperandCanBeDelegate(rightOperandSymbol);

    return
    [
      isLeftDelegate
        ? $"System.Func<{leftOperandAbsoluteType}>"
        : leftOperandAbsoluteType,
      isRightDelegate
        ? $"System.Func<{rightOperandAbsoluteType}>"
        : rightOperandAbsoluteType
    ];
  }

  protected override string ReturnType(BinaryExpressionSyntax originalNode)
  {
    return SemanticModel.ResolveTypeSymbol(originalNode)!.ToDisplayString();
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
        // Supported arithmetic operations (+, -, *, /, %)
        {
          SyntaxKind.AddExpression,
          new(SyntaxKind.AddExpression,
            SyntaxKind.PlusToken,
            WellKnownMemberNames.AdditionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.SubtractExpression,
          new(SyntaxKind.SubtractExpression,
            SyntaxKind.MinusToken,
            WellKnownMemberNames.SubtractionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.MultiplyExpression,
          new(SyntaxKind.MultiplyExpression,
            SyntaxKind.AsteriskToken,
            WellKnownMemberNames.MultiplyOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.DivideExpression,
          new(SyntaxKind.DivideExpression,
            SyntaxKind.SlashToken,
            WellKnownMemberNames.DivisionOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.ModuloExpression,
          new(SyntaxKind.ModuloExpression,
            SyntaxKind.PercentToken,
            WellKnownMemberNames.ModulusOperatorName,
            CodeAnalysisUtil.ArithmeticTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        // Supported boolean/integral bitwise logical operations (&, |, ^)
        {
          SyntaxKind.BitwiseAndExpression,
          new(SyntaxKind.BitwiseAndExpression,
            SyntaxKind.AmpersandToken,
            WellKnownMemberNames.BitwiseAndOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.BitwiseOrExpression,
          new(SyntaxKind.BitwiseOrExpression,
            SyntaxKind.BarToken,
            WellKnownMemberNames.BitwiseOrOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.ExclusiveOrExpression,
          new(SyntaxKind.ExclusiveOrExpression,
            SyntaxKind.CaretToken,
            WellKnownMemberNames.ExclusiveOrOperatorName,
            CodeAnalysisUtil.BitwiseLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        // Supported boolean logical operations (&&, ||)
        {
          SyntaxKind.LogicalAndExpression,
          new(SyntaxKind.LogicalAndExpression,
            SyntaxKind.AmpersandAmpersandToken,
            WellKnownMemberNames.LogicalAndOperatorName,
            CodeAnalysisUtil.BooleanLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.LogicalOrExpression,
          new(SyntaxKind.LogicalOrExpression,
            SyntaxKind.BarBarToken,
            WellKnownMemberNames.LogicalOrOperatorName,
            CodeAnalysisUtil.BooleanLogicalTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        // Supported integral bitwise shift operations (<<, >>, >>>)
        {
          SyntaxKind.LeftShiftExpression,
          new(SyntaxKind.LeftShiftExpression,
            SyntaxKind.LessThanLessThanToken,
            WellKnownMemberNames.LeftShiftOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature,
            PrimitiveTypesToExclude: ExcludeIfRightOperandNotImplicitlyConvertableToInt)
        },
        {
          SyntaxKind.RightShiftExpression,
          new(SyntaxKind.RightShiftExpression,
            SyntaxKind.GreaterThanGreaterThanToken,
            WellKnownMemberNames.RightShiftOperatorName,
            CodeAnalysisUtil.BitwiseShiftTypeSignature,
            PrimitiveTypesToExclude: ExcludeIfRightOperandNotImplicitlyConvertableToInt)
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
            CodeAnalysisUtil.EqualityTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.NotEqualsExpression,
          new(SyntaxKind.NotEqualsExpression,
            SyntaxKind.ExclamationEqualsToken,
            WellKnownMemberNames.InequalityOperatorName,
            CodeAnalysisUtil.EqualityTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        // Supported inequality comparison operators (<, <=, >, >=)
        {
          SyntaxKind.LessThanExpression,
          new(SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanToken,
            WellKnownMemberNames.LessThanOperatorName,
            CodeAnalysisUtil.InequalityTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.LessThanOrEqualExpression,
          new(SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.LessThanEqualsToken,
            WellKnownMemberNames.LessThanOrEqualOperatorName,
            CodeAnalysisUtil.InequalityTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.GreaterThanExpression,
          new(SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanToken,
            WellKnownMemberNames.GreaterThanOperatorName,
            CodeAnalysisUtil.InequalityTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        },
        {
          SyntaxKind.GreaterThanOrEqualExpression,
          new(SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanEqualsToken,
            WellKnownMemberNames.GreaterThanOrEqualOperatorName,
            CodeAnalysisUtil.InequalityTypeSignature,
            PrimitiveTypesToExclude: CodeAnalysisUtil.NothingToExclude)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}