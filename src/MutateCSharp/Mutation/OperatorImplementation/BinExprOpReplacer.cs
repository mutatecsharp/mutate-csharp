using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class BinExprOpReplacer(SemanticModel semanticModel)
  : AbstractMutationOperator<BinaryExpressionSyntax>(semanticModel)
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

  private IEnumerable<SyntaxKind> ValidMutants(
    BinaryExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type!;
    
    // Case 1: Predefined types
    if (type.SpecialType != SpecialType.None)
    {
      return SupportedOperators
        .Where(replacementOpEntry
          => CanApplyOperatorForSpecialTypes(
            originalNode, replacementOpEntry.Value))
        .Select(replacementOpEntry => replacementOpEntry.Key);
    }

    // Case 2: User-defined types (type.SpecialType == SpecialType.None)
    if (type is INamedTypeSymbol customType)
    {
      var overloadedOperators =
        CodeAnalysisUtil.GetOverloadedOperatorsInUserDefinedType(customType);
      return overloadedOperators
        .Where(replacementOpMethodEntry
          => CanApplyOperatorForUserDefinedTypes(
            originalNode, replacementOpMethodEntry.Value))
        .Select(replacementOpMethodEntry => replacementOpMethodEntry.Key);
    }

    return Array.Empty<SyntaxKind>();
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
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      {
        SyntaxKind.SubtractExpression,
        new(ExprKind: SyntaxKind.SubtractExpression, 
          TokenKind: SyntaxKind.MinusToken,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      {
        SyntaxKind.MultiplyExpression,
        new(ExprKind: SyntaxKind.MultiplyExpression, 
          TokenKind: SyntaxKind.AsteriskToken,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      {
        SyntaxKind.DivideExpression,
        new(ExprKind: SyntaxKind.DivideExpression, 
          TokenKind: SyntaxKind.SlashToken,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      {
        SyntaxKind.ModuloExpression,
        new(ExprKind: SyntaxKind.ModuloExpression, 
          TokenKind: SyntaxKind.PercentToken,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
      },
      // Supported boolean/integral bitwise logical operations (&, |, ^)
      {
        SyntaxKind.BitwiseAndExpression,
        new(ExprKind: SyntaxKind.BitwiseAndExpression, 
          TokenKind: SyntaxKind.AmpersandToken,
          TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
      },
      {
        SyntaxKind.BitwiseOrExpression,
        new(ExprKind: SyntaxKind.BitwiseOrExpression, 
          TokenKind: SyntaxKind.BarToken,
          TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
      },
      {
        SyntaxKind.ExclusiveOrExpression,
        new(ExprKind: SyntaxKind.ExclusiveOrExpression, 
          TokenKind: SyntaxKind.CaretToken,
          TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
      },
      // Supported boolean logical operations (&&, ||)
      {
        SyntaxKind.LogicalAndExpression,
        new(ExprKind: SyntaxKind.LogicalAndExpression, 
          TokenKind: SyntaxKind.AmpersandAmpersandToken,
          TypeSignatures: CodeAnalysisUtil.BooleanLogicalTypeSignature)
      },
      {
        SyntaxKind.LogicalOrExpression,
        new(ExprKind: SyntaxKind.LogicalOrExpression, 
          TokenKind: SyntaxKind.BarBarToken,
          TypeSignatures: CodeAnalysisUtil.BooleanLogicalTypeSignature)
      },
      // Supported integral bitwise shift operations (<<, >>, >>>)
      {
        SyntaxKind.LeftShiftExpression,
        new(ExprKind: SyntaxKind.LeftShiftExpression,
          TokenKind: SyntaxKind.LessThanLessThanToken,
          TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
      },
      {
        SyntaxKind.RightShiftExpression,
        new(ExprKind: SyntaxKind.RightShiftExpression,
          TokenKind: SyntaxKind.GreaterThanGreaterThanToken,
          TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
      },
      {
        SyntaxKind.UnsignedRightShiftExpression,
        new(ExprKind: SyntaxKind.UnsignedRightShiftExpression,
          TokenKind: SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
          TypeSignatures: CodeAnalysisUtil.BitwiseShiftTypeSignature)
      },
      // Supported equality comparison operators (==, !=)
      {
        SyntaxKind.EqualsExpression,
        new(ExprKind: SyntaxKind.EqualsExpression, 
          TokenKind: SyntaxKind.EqualsEqualsToken,
          TypeSignatures: CodeAnalysisUtil.EqualityTypeSignature)
      },
      {
        SyntaxKind.NotEqualsExpression,
        new(ExprKind: SyntaxKind.NotEqualsExpression, 
          TokenKind: SyntaxKind.ExclamationEqualsToken,
          TypeSignatures: CodeAnalysisUtil.EqualityTypeSignature)
      },
      // Supported inequality comparison operators (<, <=, >, >=)
      {
        SyntaxKind.LessThanExpression,
        new(ExprKind: SyntaxKind.LessThanExpression, 
          TokenKind: SyntaxKind.LessThanToken,
          TypeSignatures: CodeAnalysisUtil.InequalityTypeSignature)
      },
      {
        SyntaxKind.LessThanOrEqualExpression,
        new(ExprKind: SyntaxKind.LessThanOrEqualExpression,
          TokenKind: SyntaxKind.LessThanEqualsToken,
          TypeSignatures: CodeAnalysisUtil.InequalityTypeSignature)
      },
      {
        SyntaxKind.GreaterThanExpression,
        new(ExprKind: SyntaxKind.GreaterThanExpression, 
          TokenKind: SyntaxKind.GreaterThanToken,
          TypeSignatures: CodeAnalysisUtil.InequalityTypeSignature)
      },
      {
        SyntaxKind.GreaterThanOrEqualExpression,
        new(ExprKind: SyntaxKind.GreaterThanOrEqualExpression,
          TokenKind: SyntaxKind.GreaterThanOrEqualExpression,
          TypeSignatures: CodeAnalysisUtil.InequalityTypeSignature)
      }
    }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}

/*
 * Check if operator is applicable for a specific type.
 *
 *  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/operator-overloading#overloadable-operators
    An operator declaration must satisfy the following rules:

    A binary operator has two input parameters.
    In each case, at least one parameter must have type T or T?,
    where T is the type that contains the operator declaration.

    ---
    Note that user-defined operator overloading can return any type, including
    types not related to the class overloading the operator.
 */
public sealed partial class BinExprOpReplacer
{
  /*
   * For an operator to be applicable to a special type:
   * 1) The replacement operator must differ from the original operator;
   * 2) The replacement operator must support the same parameter types as the original operator;
   * 3) The replacement return type must be assignable to the original return type.
   */
  private bool CanApplyOperatorForSpecialTypes(
    BinaryExpressionSyntax originalNode, CodeAnalysisUtil.BinOp replacementOp)
  {
    // Operator checks
    // Reject if the replacement candidate is the same as the original operator
    if (originalNode.Kind() == replacementOp.ExprKind) return false;
    // Reject if original operator is not supported
    if (!SupportedOperators.ContainsKey(originalNode.Kind()))
      return false;

    // Type checks
    var returnType = SemanticModel.GetTypeInfo(originalNode).Type!;
    var variableType = SemanticModel.GetTypeInfo(originalNode.Left).Type!;

    var returnTypeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(returnType.SpecialType);
    var variableTypeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(variableType.SpecialType);
    // Reject if the replacement operator type group is not the same as the
    // original operator type group
    return replacementOp.TypeSignatures
      .Any(signature =>
        signature.OperandType.HasFlag(variableTypeClassification)
        && signature.ReturnType.HasFlag(returnTypeClassification));

  }

  /*
   * In C# it is possible to define operator overloads for user-defined types
   * that return a different type from the user-defined type.
   *
   * Example:
   * public class A
     {
       public static int operator +(A a1, B b1) => 0;
     }

     public class B
     {
       public static int operator +(B b1, A a1) => 0;
     }

     public class C
     {
       public static void Main()
       {
         var a = new A();
         var b = new B();
         var c = a + b; // compiles with operator+ definition from A
         var d = b + a; // compiles with operator+ definition from B
       }
     }
   *
   * Given the following statement:
   * var c = a op1 b
   * where:
   * a is of type A or A?,
   * b is of type B of B?,
   * op1 is the original binary operator,
   * op2 is the replacement binary operator,
   * C is of return type of op1 that is overloaded in type A or type B.
   *
   * the following should hold:
   * 1) op2 should exist in A or B;
   * 2) op2 should take two parameters of type A/A? and type B/B? respectively;
   * 3) op2 should return type C.
   */
  private bool CanApplyOperatorForUserDefinedTypes(
    BinaryExpressionSyntax originalNode, IMethodSymbol replacementOpMethod)
  {
    // 1) Get the parameter types for the replacement operator
    if (replacementOpMethod.Parameters is not
        [var firstParam, var secondParam]) return false;

    // 2) Get the types of variables involved in the original binary operator
    var firstVariableType = SemanticModel.GetTypeInfo(originalNode.Left).Type;
    var secondVariableType = SemanticModel.GetTypeInfo(originalNode.Right).Type;
    var originalReturnType = SemanticModel.GetTypeInfo(originalNode).Type;

    // 3) Check that the types are assignable:
    // First parameter type (original should be assignable to replacement)
    var checkFirstTypeAssignable =
      firstVariableType?.GetType().IsAssignableTo(firstParam.Type.GetType()) ??
      false;

    if (!checkFirstTypeAssignable) return false;

    // Second parameter type (original should be assignable to replacement)
    var checkSecondTypeAssignable = secondVariableType?.GetType()
      .IsAssignableTo(secondParam.Type.GetType()) ?? false;

    if (!checkSecondTypeAssignable) return false;

    // Return type (replacement should be assignable to original)
    return originalReturnType != null && replacementOpMethod.ReturnType
      .GetType().IsAssignableTo(originalReturnType.GetType());
  }
}