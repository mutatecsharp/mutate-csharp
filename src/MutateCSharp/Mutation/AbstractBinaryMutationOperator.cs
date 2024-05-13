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
  protected abstract FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedBinaryOperators();

  protected IEnumerable<SyntaxKind> ValidMutants(T originalNode, ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } methodSignature) return [];

    var leftType = methodSignature.OperandTypes[0];
    var rightType = methodSignature.OperandTypes[1];
    
    var leftAbsoluteType = leftType.GetNullableUnderlyingType();
    var rightAbsoluteType = rightType.GetNullableUnderlyingType();

    var validMutants = SupportedBinaryOperators()
      .Where(replacementOpEntry =>
        originalNode.Kind() != replacementOpEntry.Key);
    
    // Simple types: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/types#835-simple-types
    // Case 1: Simple types (value types) and string type (reference type)
    if (leftAbsoluteType.SpecialType is not SpecialType.None &&
        rightAbsoluteType.SpecialType is not SpecialType.None)
    {
      // https://github.com/dotnet/csharplang/issues/871
      // C# does not allow either operand of short-circuit operators to contain nullable
      if (leftType.IsTypeSymbolNullable() || rightType.IsTypeSymbolNullable())
      {
        validMutants = validMutants.Where(replacementOpEntry =>
          !CodeAnalysisUtil.ShortCircuitOperators.Contains(replacementOpEntry.Key));
      }
      
      validMutants = validMutants
        .Where(replacementOpEntry =>
          CanApplyOperatorForSpecialTypes(
            leftAbsoluteType,
            rightAbsoluteType,
            methodSignature.ReturnType.GetNullableUnderlyingType(),
            replacementOpEntry.Value));
    }
    else
    {
      // Case 2: User-defined types
      // At this point, either the left and/or right operand is user-defined
      // Since overloaded operators can return types that do not relate to the
      // current class, we should check the overloaded operators from the
      // operand types
      // We attempt overload resolution as specified in
      // https://learn.microsoft.com/en-us/dotnet/visual-basic/reference/language-specification/overload-resolution
      validMutants = validMutants
        .Where(replacementOpEntry =>
          CanApplyOperatorForUserDefinedTypes(
            methodSignature.ReturnType,
            methodSignature.OperandTypes[0],
            methodSignature.OperandTypes[1],
            replacementOpEntry.Value));
    }

    return validMutants.Select(op => op.Key);
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
    ITypeSymbol leftAbsoluteType,
    ITypeSymbol rightAbsoluteType,
    ITypeSymbol returnType,
    CodeAnalysisUtil.Op replacementOp)
  {
    var returnAbsoluteType = returnType.GetNullableUnderlyingType();
    
    // Obtain mutant expression return type for the replacement operator
    var mutantExpressionType = 
      SemanticModel.ResolveOverloadedPredefinedBinaryOperator(
      replacementOp.ExprKind, returnAbsoluteType.SpecialType,
      leftAbsoluteType.SpecialType, rightAbsoluteType.SpecialType);
    if (!mutantExpressionType.HasValue) return false;

    var (resolvedReturnType, resolvedLeftType, resolvedRightType) =
      mutantExpressionType.Value;
    
    // Check if mutant expression return type has an implicit conversion to the
    // original return type, and if each of the original expression operand type
    // has an implicit conversion to the corresponding mutant expression operand
    // type
    return SemanticModel.Compilation.HasImplicitConversion(
             resolvedReturnType, returnType)
           && SemanticModel.Compilation.HasImplicitConversion(
             leftAbsoluteType, resolvedLeftType)
           && SemanticModel.Compilation.HasImplicitConversion(
             rightAbsoluteType, resolvedRightType);
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
    ITypeSymbol returnType, ITypeSymbol leftType, ITypeSymbol rightType,
    CodeAnalysisUtil.Op replacementOp)
  {
    // 1) Get the types of variables involved in the original binary operator
    // Note that while reference parameter types are guaranteed to be non-nullable,
    // any of these operators could be overloaded to accept primitive-typed
    // parameters, or return a value of primitive type
    var leftAbsoluteType = leftType.GetNullableUnderlyingType();
    var rightAbsoluteType = rightType.GetNullableUnderlyingType();

    // 2) Check if user-defined operator exists
    if (HasUnambiguousUserDefinedOperator(replacementOp, 
          leftAbsoluteType, rightAbsoluteType, returnType))
    {
      return true;
    }
    
    // 3) Fallback: check if equality operator applies
    if (replacementOp.ExprKind 
          is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression)
    {
      return CanApplyEqualityOperator(
        leftAbsoluteType, rightAbsoluteType, returnType);
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
   *
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
  private bool CanApplyEqualityOperator(
    ITypeSymbol leftAbsoluteType, ITypeSymbol rightAbsoluteType, ITypeSymbol returnType)
  {
    var operandTypeChecks =
      SemanticModel.Compilation.HasImplicitConversion(leftAbsoluteType, rightAbsoluteType)
      || SemanticModel.Compilation.HasImplicitConversion(rightAbsoluteType, leftAbsoluteType);

    var boolType =
      SemanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);

    return operandTypeChecks &&
           SemanticModel.Compilation.HasImplicitConversion(boolType, returnType);
  }

  private bool HasUnambiguousUserDefinedOperator(CodeAnalysisUtil.Op replacementOp,
    ITypeSymbol leftAbsoluteType, ITypeSymbol rightAbsoluteType, ITypeSymbol returnType)
  {
    var returnAbsoluteType = returnType.GetNullableUnderlyingType();
    
    var leftAbsoluteRuntimeType = leftAbsoluteType.GetRuntimeType(SutAssembly);
    var rightAbsoluteRuntimeType = rightAbsoluteType.GetRuntimeType(SutAssembly);
    var returnAbsoluteRuntimeType = returnAbsoluteType.GetRuntimeType(SutAssembly);
    
    // Type information not available in SUT assembly and mscorlib assembly
    if (leftAbsoluteRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {LeftType}",
        leftAbsoluteType.ToClrTypeName());
      return false;
    }

    if (rightAbsoluteRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {RightType}",
        rightAbsoluteType.ToClrTypeName());
      return false;
    }

    if (returnAbsoluteRuntimeType is null)
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
        replacementOp, leftAbsoluteRuntimeType, rightAbsoluteRuntimeType);

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
    
    // Return type could be of value type, in which case a nullable type
    // should be constructed if the original return type is nullable
    var returnRuntimeType =
      returnAbsoluteType.IsValueType && returnType.IsTypeSymbolNullable()
      ? returnAbsoluteRuntimeType.ConstructNullableValueType(sutAssembly)
      : returnAbsoluteRuntimeType;
    
    // 5) Check if replacement operator method return type is assignable to
    // original operator method return type
    return replacementOpMethod is not null && 
           replacementOpMethod.ReturnType.IsAssignableTo(returnRuntimeType);
  }
  
  public (MethodInfo?, MethodInfo?) GetResolvedBinaryOverloadedOperator(
    CodeAnalysisUtil.Op op, Type leftAbsoluteType, Type rightAbsoluteType)
  {
    // Resolving the type in the assembly requires querying with the absolute type
    // Note: Assigning A? to A does not cause an error, only a warning in C#
    // Hence it would be fine to query with the absolute type
    try
    {
      var leftMethod =
        leftAbsoluteType.GetMethod(op.MemberName, [leftAbsoluteType, rightAbsoluteType]);
      var rightMethod =
        rightAbsoluteType.GetMethod(op.MemberName, [leftAbsoluteType, rightAbsoluteType]);

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