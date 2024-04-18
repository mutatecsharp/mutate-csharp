using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation;

public abstract class AbstractBinaryMutationOperator<T>(SemanticModel semanticModel)
: AbstractMutationOperator<T>(semanticModel)
where T: ExpressionSyntax // currently support binary expression and assignment expression
{
  public abstract FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
    SupportedBinaryOperators();

  protected IEnumerable<SyntaxKind> ValidMutants(T originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type!;
    
    // Case 1: Predefined types
    if (type.SpecialType != SpecialType.None)
    {
      return SupportedBinaryOperators()
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
    
    * For an operator to be applicable to a special type:
     * 1) The replacement operator must differ from the original operator;
     * 2) The replacement operator must support the same parameter types as the original operator;
     * 3) The replacement return type must be assignable to the original return type.
 */
  private bool CanApplyOperatorForSpecialTypes(
    T originalNode, CodeAnalysisUtil.BinOp replacementOp)
  {
    // Operator checks
    // Reject if the replacement candidate is the same as the original operator
    if (originalNode.Kind() == replacementOp.ExprKind) return false;
    // Reject if original operator is not supported
    if (!SupportedBinaryOperators().ContainsKey(originalNode.Kind()))
      return false;

    // Type checks
    // TODO: checking only one of both variables is a heuristic, verify later
    // TODO: may be acceptable to only check return type
    var returnType = SemanticModel.GetTypeInfo(originalNode).Type!;

    var variableType =
      originalNode switch
      {
        BinaryExpressionSyntax expr =>
          SemanticModel.GetTypeInfo(expr.Left).Type,
        AssignmentExpressionSyntax expr => SemanticModel.GetTypeInfo(expr.Right)
          .Type,
        _ => null
      };

    if (variableType == null) return false;

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
    T originalNode, IMethodSymbol replacementOpMethod)
  {
    // 1) Get the parameter types for the replacement operator
    if (replacementOpMethod.Parameters is not
        [var firstParam, var secondParam]) return false;

    // 2) Get the types of variables involved in the original binary operator
    var firstVariableType
      =
      originalNode switch
      {
        BinaryExpressionSyntax expr =>
          SemanticModel.GetTypeInfo(expr.Left).Type,
        AssignmentExpressionSyntax expr => SemanticModel.GetTypeInfo(expr.Left)
          .Type,
        _ => null
      };
    var secondVariableType = 
      originalNode switch
      {
        BinaryExpressionSyntax expr =>
          SemanticModel.GetTypeInfo(expr.Right).Type,
        AssignmentExpressionSyntax expr => SemanticModel.GetTypeInfo(expr.Right)
          .Type,
        _ => null
      };
    var originalReturnType = SemanticModel.GetTypeInfo(originalNode).Type;

    if (firstVariableType == null || secondVariableType == null) return false;

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