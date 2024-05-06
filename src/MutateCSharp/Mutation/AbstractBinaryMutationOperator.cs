using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation;

public abstract class AbstractBinaryMutationOperator<T>(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractMutationOperator<T>(sutAssembly, semanticModel)
  where T : ExpressionSyntax // currently support binary expression and assignment expression
{
  public abstract FrozenDictionary<SyntaxKind, CodeAnalysisUtil.BinOp>
    SupportedBinaryOperators();

  public static ExpressionSyntax? GetLeftOperand(T originalNode)
  {
    return originalNode switch
    {
      BinaryExpressionSyntax expr => expr.Left,
      AssignmentExpressionSyntax expr => expr.Left,
      _ => null
    };
  }

  public static ExpressionSyntax? GetRightOperand(T originalNode)
  {
    return originalNode switch
    {
      BinaryExpressionSyntax expr => expr.Right,
      AssignmentExpressionSyntax expr => expr.Right,
      _ => null
    };
  }

  protected IEnumerable<SyntaxKind> ValidMutants(T originalNode)
  {
    var left = GetLeftOperand(originalNode);
    var right = GetRightOperand(originalNode);
    if (left == null || right == null) return Array.Empty<SyntaxKind>();

    var leftType = SemanticModel.GetTypeInfo(left).ResolveType().GetNullableUnderlyingType();
    var rightType = SemanticModel.GetTypeInfo(right).ResolveType().GetNullableUnderlyingType();
    var returnType = SemanticModel.GetTypeInfo(originalNode).ResolveType()
      .GetNullableUnderlyingType();

    if (leftType is null)
    {
      Log.Warning("Type symbol not available for {TypeSymbol} (line {Line})", 
        left.GetText(), left.GetLocation().GetLineSpan().StartLinePosition.Line);
      return Array.Empty<SyntaxKind>();
    }

    if (rightType is null)
    {
      Log.Warning("Type symbol not available for {TypeSymbol} (line {Line})", 
        right.GetText(), right.GetLocation().GetLineSpan().StartLinePosition.Line);
      return Array.Empty<SyntaxKind>();
    }

    if (returnType is null)
    {
      Log.Warning("Type symbol not available for {TypeSymbol} (line {Line})", 
        originalNode.GetText(), originalNode.GetLocation().GetLineSpan().StartLinePosition.Line);
      return Array.Empty<SyntaxKind>();
    }

    // Case 1: Predefined types
    // Binary operators can take operand of separate types, warranting the
    // necessity to check the operand type
    if (leftType.SpecialType != SpecialType.None
        && rightType.SpecialType != SpecialType.None)
      return SupportedBinaryOperators()
        .Where(replacementOpEntry
          => CanApplyOperatorForSpecialTypes(
            originalNode, replacementOpEntry.Value))
        .Select(replacementOpEntry => replacementOpEntry.Key);

    // Case 2: User-defined types
    // At this point, either the left or right operand is user-defined
    // Since overloaded operators can return types that do not relate to the
    // current class, we should check the overloaded operators from the
    // operand types
    //
    // We attempt overload resolution as specified in
    // https://learn.microsoft.com/en-us/dotnet/visual-basic/reference/language-specification/overload-resolution
    return SupportedBinaryOperators()
      .Where(replacementOpEntry
        => CanApplyOperatorForUserDefinedTypes(originalNode,
          replacementOpEntry.Value))
      .Select(replacementOpMethodEntry => replacementOpMethodEntry.Key);
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

    // Type checks
    // TODO: checking only one of both variables is a heuristic, verify later
    // TODO: may be acceptable to only check return type
    var leftOperand = GetLeftOperand(originalNode);
    if (leftOperand == null) return false;
    var returnType = SemanticModel.GetTypeInfo(originalNode).ResolveType().GetNullableUnderlyingType();
    var operandType = SemanticModel.GetTypeInfo(leftOperand).ResolveType().GetNullableUnderlyingType();
    if (returnType is null || operandType is null) return false;

    var returnTypeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(returnType.SpecialType);
    var variableTypeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(operandType.SpecialType);
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
   * 3) op2 should return a type assignable to C.
   *
   * Read more about overload resolution here:
   * https://stackoverflow.com/questions/5173339/how-does-the-method-overload-resolution-system-decide-which-method-to-call-when
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/expressions#1264-overload-resolution
   */
  public bool CanApplyOperatorForUserDefinedTypes(
    T originalNode, CodeAnalysisUtil.BinOp replacementOp)
  {
    // Reject if the original operator is the same as the replacement operator
    if (originalNode.Kind() == replacementOp.ExprKind) return false;

    // 1) Get the types of variables involved in the original binary operator
    var left = GetLeftOperand(originalNode);
    var right = GetRightOperand(originalNode);
    if (left == null || right == null) return false;

    var leftAbsoluteType = 
      SemanticModel.GetTypeInfo(left).ResolveType()!.GetNullableUnderlyingType();
    var rightAbsoluteType =
      SemanticModel.GetTypeInfo(right).ResolveType()!.GetNullableUnderlyingType();
    var returnMaybeNullableType = SemanticModel.GetTypeInfo(originalNode).Type;

    if (leftAbsoluteType is null || rightAbsoluteType is null ||
        returnMaybeNullableType is null) return false;

    // 2) Check if user-defined operator exists
    if (HasUnambiguousUserDefinedOperator(replacementOp, leftAbsoluteType,
          rightAbsoluteType, returnMaybeNullableType.GetNullableUnderlyingType()!))
    {
      return true;
    }
    
    // 3) Fallback: check if equality operator applies
    if (replacementOp.ExprKind 
          is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
    {
      return CanApplyEqualityOperator(
        leftAbsoluteType, rightAbsoluteType, returnMaybeNullableType);
    }

    return false;
  }

  /*
   * Equality operator is baked into the C# programming language and is defined
   * for all types regardless of whether is overloaded. We handle this case
   * specially.
   *
   * A binary (in)equality expression should have either left operand or
   * right operand implicitly convertable to the other.
   */
  private bool CanApplyEqualityOperator(
    ITypeSymbol leftType, ITypeSymbol rightType, ITypeSymbol returnType)
  {
    var operandTypeChecks =
      SemanticModel.Compilation.HasImplicitConversion(leftType, rightType)
           || SemanticModel.Compilation.HasImplicitConversion(
             rightType, leftType);

    var boolType =
      SemanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);

    return operandTypeChecks &&
           SemanticModel.Compilation.HasImplicitConversion(boolType,
             returnType);
  }

  private bool HasUnambiguousUserDefinedOperator(CodeAnalysisUtil.BinOp replacementOp,
    ITypeSymbol leftAbsoluteType, ITypeSymbol rightAbsoluteType, ITypeSymbol returnAbsoluteType)
  {
    var leftRuntimeType = leftAbsoluteType.GetRuntimeType(SutAssembly);
    var rightRuntimeType = rightAbsoluteType.GetRuntimeType(SutAssembly);
    var returnRuntimeType = returnAbsoluteType.GetRuntimeType(SutAssembly);
    
    // Type information not available in SUT assembly and mscorlib assembly
    if (leftRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {LeftType}",
        leftAbsoluteType.ToClrTypeName());
      return false;
    }

    if (rightRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {RightType}",
        rightAbsoluteType.ToClrTypeName());
      return false;
    }

    if (returnRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {ReturnType}",
        returnAbsoluteType.ToClrTypeName());
      return false;
    }
    
    // Get overloaded operator methods from operand user-defined types
    //
    // We leverage reflection to handle overload resolution within a single type
    //
    // Note that either left or right operand may be of predefined type,
    // but not both at the same time
    var (leftReplacementOpMethod, rightReplacementOpMethod) =
      GetResolvedBinaryOverloadedOperator(
        replacementOp, leftRuntimeType, rightRuntimeType);

    // No match for the overloaded operator from both operand types is found
    if (leftReplacementOpMethod == null && rightReplacementOpMethod == null)
      return false;

    // If one of the methods are null, we trivially select the non-null method
    // as the replacement operator method
    var replacementOpMethod =
      leftReplacementOpMethod == null ? rightReplacementOpMethod
      : rightReplacementOpMethod == null ? leftReplacementOpMethod : null;

    // 4) Apply tiebreaks by selecting operator with more specific parameter types
    if (replacementOpMethod == null)
      replacementOpMethod = DetermineBetterFunctionMember(
        leftReplacementOpMethod!, rightReplacementOpMethod!);
    
    // 5) Check if replacement operator method return type is assignable to
    // original operator method return type
    return replacementOpMethod is not null && 
           replacementOpMethod.ReturnType.IsAssignableTo(returnRuntimeType);
  }
  
  public (MethodInfo?, MethodInfo?) GetResolvedBinaryOverloadedOperator(
    CodeAnalysisUtil.BinOp binOp, Type leftAbsoluteType, Type rightAbsoluteType)
  {
    // Resolving the type in the assembly requires querying with the absolute type
    // Note: Assigning A? to A does not cause an error, only a warning in C#
    // Hence it would be fine to query with the absolute type
    try
    {
      var leftMethod =
        leftAbsoluteType.GetMethod(binOp.MemberName, [leftAbsoluteType, rightAbsoluteType]);
      var rightMethod =
        rightAbsoluteType.GetMethod(binOp.MemberName, [leftAbsoluteType, rightAbsoluteType]);

      return (leftMethod, rightMethod);
    }
    catch (AmbiguousMatchException)
    {
      return (null, null);
    }
  }

  private static MethodInfo? DetermineBetterFunctionMember(
    MethodInfo firstMethod,
    MethodInfo secondMethod)
  {
    var firstTypes = firstMethod.GetParameters();
    var secondTypes = secondMethod.GetParameters();

    var firstLeftType = firstTypes[0];
    var firstRightType = firstTypes[1];
    var secondLeftType = secondTypes[0];
    var secondRightType = secondTypes[1];

    var firstMethodMoreSpecific =
      firstLeftType.ParameterType.IsAssignableTo(secondLeftType.ParameterType)
      && firstRightType.ParameterType.IsAssignableTo(secondRightType
        .ParameterType);
    var secondMethodMoreSpecific =
      secondLeftType.ParameterType.IsAssignableTo(firstLeftType.ParameterType)
      && secondRightType.ParameterType.IsAssignableTo(firstRightType
        .ParameterType);

    return firstMethodMoreSpecific ? firstMethod
      : secondMethodMoreSpecific ? secondMethod : null;
  }
}