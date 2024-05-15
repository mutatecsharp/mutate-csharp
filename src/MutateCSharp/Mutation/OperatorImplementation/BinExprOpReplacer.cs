using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class BinExprOpReplacer(
  Assembly sutAssembly,
  SemanticModel semanticModel,
  FrozenDictionary<SyntaxKind,
    ImmutableArray<CodeAnalysisUtil.MethodSignature>> builtInOperatorSignatures)
  : AbstractBinaryMutationOperator<BinaryExpressionSyntax>(sutAssembly,
    semanticModel, builtInOperatorSignatures)
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
      SemanticModel.ResolveTypeSymbol(node)!.GetNullableUnderlyingType());

    SyntaxNode[] operands = [originalNode.Left, originalNode.Right];

    var shouldBeDelegateButCannot =
      CodeAnalysisUtil.ShortCircuitOperators.Contains(originalNode.Kind())
      && operands.Any(operand => !SemanticModel.NodeCanBeDelegate(operand));

    // Exclude node from mutation:
    // 1) If type contains generic type parameter;
    // 2) If type is private (and thus inaccessible);
    // 3) If expression contains short-circuiting operator and has to be wrapped
    // in a lambda expression, but contains ref which cannot be wrapped in a lambda
    return !shouldBeDelegateButCannot && types.All(type =>
      !SyntaxRewriterUtil.ContainsGenericTypeParameterLogged(in type)
      && type.GetVisibility() is not CodeAnalysisUtil.SymbolVisibility.Private
    ) && SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind, bool isDelegate,
    bool isLeftAwaitable, bool isRightAwaitable)
  {
    var insertInvocation = isDelegate ? "()" : string.Empty;
    var leftModifier = isLeftAwaitable ? "await " : string.Empty;
    var rightModifier = isRightAwaitable ? "await " : string.Empty;

    return
      $"{leftModifier}{{0}}{insertInvocation} {SupportedOperators[kind]} {rightModifier}{{1}}{insertInvocation}";
  }

  protected override ExpressionRecord OriginalExpression(
    BinaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    var containsShortCircuitOperators =
      CodeAnalysisUtil.ShortCircuitOperators.Contains(originalNode.Kind())
      || mutantExpressions.Any(mutant =>
        CodeAnalysisUtil.ShortCircuitOperators.Contains(mutant.Operation));

    // Check for awaitable operands
    var isLeftOperandAwaitable = originalNode.Left is AwaitExpressionSyntax;
    var isRightOperandAwaitable = originalNode.Right is AwaitExpressionSyntax;

    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind(), containsShortCircuitOperators,
        isLeftOperandAwaitable, isRightOperandAwaitable));
  }

  protected override
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(BinaryExpressionSyntax originalNode,
      ITypeSymbol? requiredReturnType)
  {
    var validMutants = ValidMutants(originalNode, requiredReturnType).ToArray();

    var containsShortCircuitOperators =
      CodeAnalysisUtil.ShortCircuitOperators.Contains(originalNode.Kind()) ||
      validMutants.Any(op =>
        CodeAnalysisUtil.ShortCircuitOperators.Contains(op));
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);

    // Check for awaitable operands
    var isLeftOperandAwaitable = originalNode.Left is AwaitExpressionSyntax;
    var isRightOperandAwaitable = originalNode.Right is AwaitExpressionSyntax;

    return
    [
      ..attachIdToMutants.Select(entry =>
        (entry.id, new ExpressionRecord(entry.op,
          ExpressionTemplate(entry.op, containsShortCircuitOperators,
            isLeftOperandAwaitable, isRightOperandAwaitable))
        )
      )
    ];
  }

  // C# allows short-circuit evaluation for boolean && and || operators.
  // To preserve the semantics in mutant schemata, we defer the expression
  // evaluation by wrapping the expression in lambda iff any of the original
  // expression or mutant expressions involve the use of && or ||
  //   
  // If the operand types are value types, we adhere to the C# language specification
  // by promoting the parameter types corresponding to the method signatures
  // of the predefined operators, which resolves the issue as follows:
  //   
  // Example:
  // ulong x = 10;
  // var y = (x << 10) + 1;
  // (x << 10) is recognised as ulong, and 1 as int,
  // but ulong + int does not type check
  protected override CodeAnalysisUtil.MethodSignature?
    NonMutatedTypeSymbols(BinaryExpressionSyntax originalNode,
      ITypeSymbol? requiredReturnType)
  {
    var leftOperandType = SemanticModel.ResolveTypeSymbol(originalNode.Left)!;
    var rightOperandType = SemanticModel.ResolveTypeSymbol(originalNode.Right)!;
    var returnType = SemanticModel.ResolveTypeSymbol(originalNode)!;

    var leftOperandAbsoluteType = leftOperandType.GetNullableUnderlyingType();
    var rightOperandAbsoluteType = rightOperandType.GetNullableUnderlyingType();
    var returnAbsoluteType = returnType.GetNullableUnderlyingType();

    // If the determined type of an integer literal is int and the value
    // represented by the literal is within the range of the destination type,
    // the value can be implicitly converted to sbyte, byte, short, ushort,
    // uint, ulong, nint or nuint.
    // We handle the case where the type of int literals can be implicitly narrowed
    if (leftOperandAbsoluteType.IsNumeric() &&
        rightOperandAbsoluteType.IsNumeric())
    {
      if (originalNode.Left.IsKind(SyntaxKind.NumericLiteralExpression)
          && SemanticModel.CanImplicitlyConvertNumericLiteral(
            originalNode.Left, returnAbsoluteType.SpecialType))
      {
        leftOperandType = returnAbsoluteType;
      }

      if (originalNode.Right.IsKind(SyntaxKind.NumericLiteralExpression)
          && SemanticModel.CanImplicitlyConvertNumericLiteral(
            originalNode.Right, returnAbsoluteType.SpecialType))
      {
        rightOperandType = returnAbsoluteType;
      }
      
      // Construct method signature
      var paramTypes =
        new CodeAnalysisUtil.MethodSignature(returnType,
          [leftOperandType, rightOperandType]);
      

      if (SemanticModel.ResolveOverloadedPredefinedBinaryOperator(
            BuiltInOperatorSignatures,
            originalNode.Kind(),
            paramTypes)
          is not { } result)
        return null; // Application of overload resolution failed

      return new CodeAnalysisUtil.MethodSignature(result.returnSymbol, 
        [result.leftSymbol, result.rightSymbol]);
    }

    // Check if null keyword exists in binary expression
    // Since null does not have a type in C#, the helper method resolves it to
    // the object type by default. We intercept the resolved type and change it
    // to the corresponding nullable type if the other operand is a value type.
    // This allows syntax containing equality operators to be mutated.
    // Ignore this case as the mutations are redundant
    if (leftOperandType.IsValueType &&
        originalNode.Right.IsKind(SyntaxKind.NullLiteralExpression) ||
        rightOperandType.IsValueType &&
        originalNode.Left.IsKind(SyntaxKind.NullLiteralExpression))
    {
      return null;
    }

    // Either left or right operand is reference type; null can be of object type
    // Remove nullable since a reference type T? can be cast to T
    if (!leftOperandType.IsValueType)
      leftOperandType = leftOperandType.GetNullableUnderlyingType();
    if (!rightOperandType.IsValueType)
      rightOperandType = rightOperandType.GetNullableUnderlyingType();

    return new CodeAnalysisUtil.MethodSignature(returnType,
      [leftOperandType, rightOperandType]);
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(
    BinaryExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } typeSignature) return [];

    var leftTypeDisplay = typeSignature.OperandTypes[0].ToDisplayString();
    var rightTypeDisplay = typeSignature.OperandTypes[1].ToDisplayString();

    // Check for awaitable operands
    var isLeftOperandAwaitable = originalNode.Left is AwaitExpressionSyntax;
    var isRightOperandAwaitable = originalNode.Right is AwaitExpressionSyntax;

    // Wrap operand type with Task monad if operand is awaitable
    if (isLeftOperandAwaitable)
      leftTypeDisplay = $"System.Threading.Tasks.Task<{leftTypeDisplay}>";
    if (isRightOperandAwaitable)
      rightTypeDisplay = $"System.Threading.Tasks.Task<{rightTypeDisplay}>";

    // Check for short circuit operators
    var containsShortCircuitOperators =
      CodeAnalysisUtil.ShortCircuitOperators.Contains(originalNode.Kind())
      || mutantExpressions.Any(mutant =>
        CodeAnalysisUtil.ShortCircuitOperators.Contains(mutant.Operation));

    // Wrap operand type with Func if short-circuit operators exist in mutation group
    if (containsShortCircuitOperators)
    {
      leftTypeDisplay = $"System.Func<{leftTypeDisplay}>";
      rightTypeDisplay = $"System.Func<{rightTypeDisplay}>";
    }

    return [leftTypeDisplay, rightTypeDisplay];
  }

  protected override string SchemaReturnTypeDisplay(
    BinaryExpressionSyntax originalNode,
    ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } typeSignature) return string.Empty;

    var returnTypeDisplay = typeSignature.ReturnType.ToDisplayString();

    // If there exists awaitable expression in either operand,
    // the return type should become async Task<bool>
    SyntaxNode[] operands = [originalNode.Left, originalNode.Right];
    // Check for awaitable operands
    return operands.Any(operand => operand is AwaitExpressionSyntax)
      ? $"async System.Threading.Tasks.Task<{returnTypeDisplay}>"
      : returnTypeDisplay;
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

  protected override string SchemaBaseName()
  {
    return "ReplaceBinExprOp";
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
            WellKnownMemberNames.AdditionOperatorName)
        },
        {
          SyntaxKind.SubtractExpression,
          new(SyntaxKind.SubtractExpression,
            SyntaxKind.MinusToken,
            WellKnownMemberNames.SubtractionOperatorName)
        },
        {
          SyntaxKind.MultiplyExpression,
          new(SyntaxKind.MultiplyExpression,
            SyntaxKind.AsteriskToken,
            WellKnownMemberNames.MultiplyOperatorName)
        },
        {
          SyntaxKind.DivideExpression,
          new(SyntaxKind.DivideExpression,
            SyntaxKind.SlashToken,
            WellKnownMemberNames.DivisionOperatorName)
        },
        {
          SyntaxKind.ModuloExpression,
          new(SyntaxKind.ModuloExpression,
            SyntaxKind.PercentToken,
            WellKnownMemberNames.ModulusOperatorName)
        },
        // Supported boolean/integral bitwise logical operations (&, |, ^)
        {
          SyntaxKind.BitwiseAndExpression,
          new(SyntaxKind.BitwiseAndExpression,
            SyntaxKind.AmpersandToken,
            WellKnownMemberNames.BitwiseAndOperatorName)
        },
        {
          SyntaxKind.BitwiseOrExpression,
          new(SyntaxKind.BitwiseOrExpression,
            SyntaxKind.BarToken,
            WellKnownMemberNames.BitwiseOrOperatorName)
        },
        {
          SyntaxKind.ExclusiveOrExpression,
          new(SyntaxKind.ExclusiveOrExpression,
            SyntaxKind.CaretToken,
            WellKnownMemberNames.ExclusiveOrOperatorName)
        },
        // Supported boolean logical operations (&&, ||)
        {
          SyntaxKind.LogicalAndExpression,
          new(SyntaxKind.LogicalAndExpression,
            SyntaxKind.AmpersandAmpersandToken,
            WellKnownMemberNames.LogicalAndOperatorName)
        },
        {
          SyntaxKind.LogicalOrExpression,
          new(SyntaxKind.LogicalOrExpression,
            SyntaxKind.BarBarToken,
            WellKnownMemberNames.LogicalOrOperatorName)
        },
        // Supported integral bitwise shift operations (<<, >>, >>>)
        {
          SyntaxKind.LeftShiftExpression,
          new(SyntaxKind.LeftShiftExpression,
            SyntaxKind.LessThanLessThanToken,
            WellKnownMemberNames.LeftShiftOperatorName)
        },
        {
          SyntaxKind.RightShiftExpression,
          new(SyntaxKind.RightShiftExpression,
            SyntaxKind.GreaterThanGreaterThanToken,
            WellKnownMemberNames.RightShiftOperatorName)
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
            WellKnownMemberNames.EqualityOperatorName)
        },
        {
          SyntaxKind.NotEqualsExpression,
          new(SyntaxKind.NotEqualsExpression,
            SyntaxKind.ExclamationEqualsToken,
            WellKnownMemberNames.InequalityOperatorName)
        },
        // Supported inequality comparison operators (<, <=, >, >=)
        {
          SyntaxKind.LessThanExpression,
          new(SyntaxKind.LessThanExpression,
            SyntaxKind.LessThanToken,
            WellKnownMemberNames.LessThanOperatorName)
        },
        {
          SyntaxKind.LessThanOrEqualExpression,
          new(SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.LessThanEqualsToken,
            WellKnownMemberNames.LessThanOrEqualOperatorName)
        },
        {
          SyntaxKind.GreaterThanExpression,
          new(SyntaxKind.GreaterThanExpression,
            SyntaxKind.GreaterThanToken,
            WellKnownMemberNames.GreaterThanOperatorName)
        },
        {
          SyntaxKind.GreaterThanOrEqualExpression,
          new(SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.GreaterThanEqualsToken,
            WellKnownMemberNames.GreaterThanOrEqualOperatorName)
        }
      }.ToFrozenDictionary();
  
  protected override FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedBinaryOperators() => SupportedOperators;

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}