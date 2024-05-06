using System.Collections.Frozen;
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
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind, bool isLeftDelegate, bool isRightDelegate)
  {
    var insertLeftInvocation = isLeftDelegate ? "()" : string.Empty;
    var insertRightInvocation = isRightDelegate ? "()" : string.Empty;
    return $"{{0}}{insertLeftInvocation} {SupportedOperators[kind]} {{1}}{insertRightInvocation}";
  }

  protected override ExpressionRecord OriginalExpression(
    BinaryExpressionSyntax originalNode,
    IList<ExpressionRecord> mutantExpressions)
  {
    var containsShortCircuitOperators =
      HasShortCircuitOperators(InvolvedOperators(originalNode, mutantExpressions));
    
    var leftSymbol = SemanticModel.GetSymbolInfo(originalNode.Left).Symbol!;
    var rightSymbol = SemanticModel.GetSymbolInfo(originalNode.Right).Symbol!;

    var isLeftDelegate = containsShortCircuitOperators
                         && OperandCanBeDelegate(leftSymbol);
    var isRightDelegate = containsShortCircuitOperators
                          && OperandCanBeDelegate(rightSymbol);
    
    return new ExpressionRecord(originalNode.Kind(),
      ExpressionTemplate(originalNode.Kind(), isLeftDelegate, isRightDelegate));
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
    var involvedOperators = validMutants.Append(originalNode.Kind());
      
    var containsShortCircuitOperators = HasShortCircuitOperators(involvedOperators);
    var leftSymbol = SemanticModel.GetSymbolInfo(originalNode.Left).Symbol!;
    var rightSymbol = SemanticModel.GetSymbolInfo(originalNode.Right).Symbol!;

    var isLeftDelegate = containsShortCircuitOperators
                         && OperandCanBeDelegate(leftSymbol);
    var isRightDelegate = containsShortCircuitOperators
                          && OperandCanBeDelegate(rightSymbol);
    
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    
    return attachIdToMutants.Select(entry =>
        (entry.id, new ExpressionRecord(entry.op, 
          ExpressionTemplate(entry.op, isLeftDelegate, isRightDelegate))
        )
      ).ToList();
  }

  // C# allows short-circuit evaluation for boolean && and || operators.
  // To preserve the semantics in mutant schemata, we defer the expression
  // evaluation by wrapping the expression in lambda iff any of the original
  // expression or mutant expressions involve the use of && or ||
  protected override IList<string> ParameterTypes(
    BinaryExpressionSyntax originalNode, IList<ExpressionRecord> mutantExpressions)
  {
    var firstVariableType =
      SemanticModel.GetTypeInfo(originalNode.Left).ResolveType()!.ToDisplayString();
    var secondVariableType =
      SemanticModel.GetTypeInfo(originalNode.Right).ResolveType()!.ToDisplayString();
    
    if (!HasShortCircuitOperators(
          InvolvedOperators(originalNode, mutantExpressions))) 
      return [firstVariableType, secondVariableType];
    
    // Short-circuit evaluation operators are present
    var leftOperandSymbol =
      SemanticModel.GetSymbolInfo(originalNode.Left).Symbol!;
    var rightOperandSymbol =
      SemanticModel.GetSymbolInfo(originalNode.Right).Symbol!;
      
    return [OperandCanBeDelegate(leftOperandSymbol)
        ? $"System.Func<{firstVariableType}>"
        : firstVariableType, 
      OperandCanBeDelegate(rightOperandSymbol)
        ? $"System.Func<{secondVariableType}>"
        : secondVariableType];
  }

  protected override string ReturnType(BinaryExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
  }

  protected override string SchemaBaseName(BinaryExpressionSyntax originalNode)
  {
    return $"ReplaceBinExprOpReturn{ReturnType(originalNode)}";
  }

  private IEnumerable<SyntaxKind> InvolvedOperators(
    BinaryExpressionSyntax originalNode,
    IEnumerable<ExpressionRecord> mutantExpressions)
  {
    return mutantExpressions
      .Select(expr => expr.Operation)
      .Append(originalNode.Kind());
  }
  
  private static bool HasShortCircuitOperators(
    IEnumerable<SyntaxKind> involvedOperators)
  {
    return involvedOperators.Any(op =>
             CodeAnalysisUtil.ShortCircuitOperators.Contains(op));
  }

  /*
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/lambda-expressions
   * A lambda expression can't directly capture an in, ref, or out parameter
   * from the enclosing method.
   */
  private static bool OperandCanBeDelegate(ISymbol symbol)
  {
    return symbol switch
    {
      IParameterSymbol paramSymbol => paramSymbol is { RefKind: RefKind.None },
      ILocalSymbol localSymbol => localSymbol is { RefKind: RefKind.None },
      _ => true
    };
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