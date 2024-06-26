using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation;

public abstract class AbstractUnaryMutationOperator<T>(
  Assembly sutAssembly,
  SemanticModel semanticModel,
  FrozenDictionary<SyntaxKind,
    ImmutableArray<CodeAnalysisUtil.MethodSignature>> builtInOperatorSignatures)
  : AbstractMutationOperator<T>(sutAssembly, semanticModel)
  where T : ExpressionSyntax // currently support prefix or postfix unary expression
{
  protected readonly FrozenDictionary<SyntaxKind,
      ImmutableArray<CodeAnalysisUtil.MethodSignature>>
    BuiltInOperatorSignatures = builtInOperatorSignatures;

  protected abstract FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedUnaryOperators();
  
  private static ExpressionSyntax? GetOperand(T originalNode)
  {
    return originalNode switch
    {
      PrefixUnaryExpressionSyntax expr => expr.Operand,
      PostfixUnaryExpressionSyntax expr => expr.Operand,
      _ => null
    };
  }
  
  /*
   * Perform local value analysis on the operand if it is of literal type.
   * This works on all compile-time constants as long as it is stored in const variable.
   */
  private bool IsNonRedundantArithmeticReplacement(T originalNode,
    SyntaxKind replacementOpKind)
  {
    var operandConstant =
      SemanticModel.GetConstantValue(GetOperand(originalNode)!);
    
    if (operandConstant is {HasValue: true, Value: 0 or 0U or 0L or 0UL or 0.0f or 0.0 or 0.0m})
    {
      // expression is of type (op) 0
      if (replacementOpKind is SyntaxKind.UnaryPlusExpression
          or SyntaxKind.UnaryMinusExpression)
        return false;
    }

    if (operandConstant is {HasValue: true, 
          Value: 1 or 1U or 1UL or 1L or 1.0f or 1.0 or 1.0m
        or -1 or -1L or -1.0f or -1.0 or -1.0m})
    {
      // expression is of type (op) 1 / (op) -1
      if (replacementOpKind is SyntaxKind.UnaryMinusExpression)
        return false;
    }
    
    // Only replace inc/dec with each other
    return originalNode.Kind() switch
    {
      SyntaxKind.PreIncrementExpression => replacementOpKind is 
        SyntaxKind.PreDecrementExpression,
      SyntaxKind.PreDecrementExpression => replacementOpKind is 
        SyntaxKind.PreIncrementExpression,
      SyntaxKind.PostIncrementExpression => replacementOpKind is 
        SyntaxKind.PostDecrementExpression,
      SyntaxKind.PostDecrementExpression => replacementOpKind is
        SyntaxKind.PostIncrementExpression,
      // default: treat all operator except unary plus as non-redundant
      _ => replacementOpKind is not SyntaxKind.UnaryPlusExpression
    };
  }

  protected ISet<CodeAnalysisUtil.OperandKind> ValidOperandReplacement(
    T originalNode, CodeAnalysisUtil.MethodSignature originalSignature,
    bool optimise)
  {
    var assignable =
      SemanticModel.Compilation.HasImplicitConversion(
        originalSignature.OperandTypes[0], originalSignature.ReturnType);
    
    var results = new HashSet<CodeAnalysisUtil.OperandKind>();
    if (assignable)
      results.Add(CodeAnalysisUtil.OperandKind.UnaryOperand);

    return results;
  }

  protected IEnumerable<SyntaxKind> ValidOperatorReplacements(
    T originalNode, CodeAnalysisUtil.MethodSignature methodSignature, bool optimise)
  {
    var operandAbsoluteType =
      methodSignature.OperandTypes[0].GetNullableUnderlyingType();

    var validMutants = SupportedUnaryOperators()
      .Where(replacementOpEntry =>
        originalNode.Kind() != replacementOpEntry.Key);

    if (!IsOperandAssignable(originalNode))
    {
      // Remove candidate operators that modify variable if the operand is not
      // assignable (example: !x where x is const and cannot be updated)
      validMutants = validMutants.Where(replacementOpEntry =>
        !CodeAnalysisUtil.VariableModifyingOperators.Contains(
          replacementOpEntry.Key));
    }
    
    // Remove redundant and equivalent mutants
    if (optimise)
    {
      validMutants = validMutants.Where(replacementOpEntry =>
        IsNonRedundantArithmeticReplacement(originalNode, replacementOpEntry.Key));
    }

    // Case 1: Special types (simple types, string)
    if (operandAbsoluteType.SpecialType is not SpecialType.None)
    {
      validMutants = validMutants
        .Where(replacementOpEntry =>
          CanApplyOperatorForSpecialTypes(
            replacementOpEntry.Value,
            methodSignature));
    }
    else
    {
      // Case 2: User-defined types (type.SpecialType == SpecialType.None)
      validMutants = validMutants
        .Where(replacementOpEntry =>
          CanApplyOperatorForUserDefinedTypes(
            methodSignature.ReturnType,
            methodSignature.OperandTypes[0],
            replacementOpEntry.Value));
    }
    
    return validMutants.Select(op => op.Key);
  }

  /*
   * For an operator to be applicable to a special type:
   * 1) The replacement operator must differ from the original operator;
   * 2) The replacement operator must support the same parameter types as the original operator;
   * 3) The replacement return type must be assignable to the original return type.
   */
  protected bool CanApplyOperatorForSpecialTypes(
    CodeAnalysisUtil.Op replacementOp,
    CodeAnalysisUtil.MethodSignature originalSignature)
  {
    // Special case for true/false literals
    if (replacementOp.ExprKind.IsSyntaxKindLiteral())
    {
      return CanReplaceWithLiteral(originalSignature.ReturnType, replacementOp);
    }
    
    var mutantExpressionType = 
      SemanticModel.ResolveOverloadedPredefinedUnaryOperator(
        BuiltInOperatorSignatures, replacementOp.ExprKind, originalSignature);
    if (!mutantExpressionType.HasValue) return false;

    var resolvedSignature = mutantExpressionType.Value;

    // Check if expression type is assignable to original return type
    return SemanticModel.Compilation.HasImplicitConversion(
             originalSignature.OperandTypes[0], resolvedSignature.operandSymbol)
           && SemanticModel.Compilation.HasImplicitConversion(
             resolvedSignature.returnSymbol, originalSignature.ReturnType);
  }

  /*
   * In C# the overloaded increment/decrement operator must have the
   * parameter type and the return type the same as the class type.
   *
   * Example:
     public class A
     {
       public static A operator ++(A a1) => a1;
     }

     However this does not hold for the other unary operators:

     Example:
     public class C;

     public class A
     {
       public static C operator ~(A a) => new C();
       public static C operator !(A a) => new C();
     }

     In either case the following general rules hold.

   * Given the following statement:
   * var x = op1 a
   * where:
   * a is of type A or A?,
   * op1 is the original binary operator,
   * op2 is the replacement binary operator,
   * B is of return type of op1 that is overloaded in class A.
   *
   * the following should hold:
   * 1) op2 should exist in A;
   * 2) op2 should take a parameters of type A/A?;
   * 3) op2 should return type B.
   */
  protected bool CanApplyOperatorForUserDefinedTypes(ITypeSymbol returnType,
    ITypeSymbol operandType, CodeAnalysisUtil.Op replacementOp)
  {
    // 0) Special case for true/false literals
    if (replacementOp.ExprKind.IsSyntaxKindLiteral())
    {
      return CanReplaceWithLiteral(returnType, replacementOp);
    }
    
    // 1) Get nullable underlying type
    var operandAbsoluteType = operandType.GetNullableUnderlyingType();
    var returnAbsoluteType = returnType.GetNullableUnderlyingType();
    
    // 2) Get operand type and return type in runtime
    var operandAbsoluteRuntimeType =
      operandAbsoluteType.GetRuntimeType(SutAssembly);
    var returnAbsoluteRuntimeType =
      returnAbsoluteType.GetRuntimeType(SutAssembly);

    if (operandAbsoluteRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {OperandType}",
        operandType.ToClrTypeName());
      return false;
    }
      
    if (returnAbsoluteRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {OperandType}",
        returnType.ToClrTypeName());
      return false;
    }

    try
    {
      // 3) Get replacement operator method
      var replacementOpMethod =
        operandAbsoluteRuntimeType.GetMethod(replacementOp.MemberName,
          [operandAbsoluteRuntimeType]);
      
      // Return type could be of value type, in which case a nullable type
      // should be constructed
      var returnRuntimeType = 
        returnAbsoluteType.IsValueType && returnType.IsTypeSymbolNullable()
        ? returnAbsoluteRuntimeType.ConstructNullableValueType(sutAssembly)
        : returnAbsoluteRuntimeType;

      // 4) Replacement operator is valid if its return type is assignable to
      // the original operator return type
      return replacementOpMethod is not null &&
             replacementOpMethod.ReturnType.IsAssignableTo(returnRuntimeType);
    }
    catch (AmbiguousMatchException)
    {
      return false;
    }
  }

  private bool CanReplaceWithLiteral(ITypeSymbol returnType, CodeAnalysisUtil.Op replacementLiteral)
  {
    if (replacementLiteral.ExprKind is 
        SyntaxKind.TrueLiteralExpression or
        SyntaxKind.FalseLiteralExpression)
    {
      var replacementType =
        SemanticModel.Compilation.GetSpecialType(SpecialType.System_Boolean);
      return SemanticModel.Compilation.HasImplicitConversion(replacementType,
        returnType);
    }

    // Other literal types currently not supported
    return false;
  }

  private bool IsOperandAssignable(T originalNode)
  {
    var operand = GetOperand(originalNode)!;
    return CodeAnalysisUtil.VariableModifyingOperators.Contains(
             originalNode.Kind())
           || (SemanticModel.GetSymbolInfo(operand).Symbol?.IsSymbolVariable() 
               ?? false);
  }
}