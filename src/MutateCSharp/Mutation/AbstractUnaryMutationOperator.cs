using System.Collections.Frozen;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation;

public abstract class AbstractUnaryMutationOperator<T>(
  Assembly sutAssembly,
  SemanticModel semanticModel)
  : AbstractMutationOperator<T>(sutAssembly, semanticModel)
  where T : ExpressionSyntax // currently support prefix or postfix unary expression
{
  public abstract FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
    SupportedUnaryOperators();

  public static ExpressionSyntax? GetOperand(T originalNode)
  {
    return originalNode switch
    {
      PrefixUnaryExpressionSyntax expr => expr.Operand,
      PostfixUnaryExpressionSyntax expr => expr.Operand,
      _ => null
    };
  }

  protected IEnumerable<SyntaxKind> ValidMutants(T originalNode)
  {
    var operandNode = GetOperand(originalNode);
    if (operandNode == null) return Array.Empty<SyntaxKind>();

    var operandType = 
      SemanticModel.GetTypeInfo(operandNode).ResolveType()!.GetNullableUnderlyingType();
    var returnType = SemanticModel.GetTypeInfo(originalNode).ResolveType()!
      .GetNullableUnderlyingType();
    
    if (operandType is null)
    {
      Log.Warning("Type symbol not available for {TypeSymbol} (line {Line})", 
        operandNode.GetText(), operandNode.GetLocation().GetLineSpan().StartLinePosition.Line);
      return Array.Empty<SyntaxKind>();
    }
    
    if (returnType is null)
    {
      Log.Warning("Type symbol not available for {TypeSymbol} (line {Line})", 
        originalNode.GetText(), originalNode.GetLocation().GetLineSpan().StartLinePosition.Line);
      return Array.Empty<SyntaxKind>();
    }

    // Case 1: Predefined types
    if (operandType.SpecialType != SpecialType.None)
      return SupportedUnaryOperators()
        .Where(replacementOpEntry =>
          CanApplyOperatorForSpecialTypes(originalNode,
            replacementOpEntry.Value))
        .Select(replacementOpEntry => replacementOpEntry.Key);

    // Case 2: User-defined types (type.SpecialType == SpecialType.None)
    return SupportedUnaryOperators()
      .Where(replacementOpEntry =>
        CanApplyOperatorForUserDefinedTypes(originalNode,
          replacementOpEntry.Value))
      .Select(replacementOpMethodEntry => replacementOpMethodEntry.Key);
  }

  /*
   * For an operator to be applicable to a special type:
   * 1) The replacement operator must differ from the original operator;
   * 2) The replacement operator must support the same parameter types as the original operator;
   * 3) The replacement return type must be assignable to the original return type.
   */
  protected bool CanApplyOperatorForSpecialTypes(
    T originalNode, CodeAnalysisUtil.UnaryOp replacementOp)
  {
    // Reject if the replacement candidate is the same as the original operator
    if (originalNode.Kind() == replacementOp.ExprKind) return false;

    // 1) Get the operand type name and return type name
    var operand = GetOperand(originalNode);
    if (operand == null) return false;
    var operandType = SemanticModel.GetTypeInfo(operand).ResolveType()!.GetNullableUnderlyingType();
    var returnType = SemanticModel.GetTypeInfo(originalNode).ResolveType()!.GetNullableUnderlyingType();
    if (operandType is null || returnType is null) return false;

    var operandTypeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(operandType.SpecialType);
    var returnTypeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(returnType.SpecialType);

    // 2) Replacement operator is valid if its return type is in the same
    // type group as the original operator type group
    return replacementOp.TypeSignatures
      .Any(signature => signature.OperandType.HasFlag(operandTypeClassification)
                        && signature.ReturnType.HasFlag(
                          returnTypeClassification));
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
  protected bool CanApplyOperatorForUserDefinedTypes(T originalNode,
    CodeAnalysisUtil.UnaryOp replacementOp)
  {
    // Reject if the replacement candidate is the same as the original operator
    if (originalNode.Kind() == replacementOp.ExprKind) return false;

    // 1) Get the operand type name and return type name
    var operand = GetOperand(originalNode);
    if (operand == null) return false;
    
    var operandTypeSymbol = 
      SemanticModel.GetTypeInfo(operand).ResolveType()!.GetNullableUnderlyingType();
    var returnTypeSymbol =
      SemanticModel.GetTypeInfo(originalNode).ResolveType()!.GetNullableUnderlyingType();
    if (operandTypeSymbol is null || returnTypeSymbol is null) return false;
    
    // 2) Get operand type and return type in runtime
    // If we cannot locate the type from the assembly of SUT, this means we
    // are looking for types defined in the core library: we defer to the
    // current assembly to get the type's runtime type
    var operandAbsoluteRuntimeType =
      operandTypeSymbol.GetRuntimeType(SutAssembly);
    var returnAbsoluteRuntimeType =
      returnTypeSymbol.GetRuntimeType(SutAssembly);

    if (operandAbsoluteRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {OperandType}",
        operandTypeSymbol.ToClrTypeName());
      return false;
    }
      
    if (returnAbsoluteRuntimeType is null)
    {
      Log.Debug("Assembly type information not available for {OperandType}",
        returnTypeSymbol.ToClrTypeName());
      return false;
    }

    try
    {
      // 3) Get replacement operator method
      var replacementOpMethod =
        operandAbsoluteRuntimeType.GetMethod(replacementOp.MemberName,
          [operandAbsoluteRuntimeType]);

      // 4) Replacement operator is valid if its return type is assignable to
      // the original operator return type
      return replacementOpMethod is not null &&
             replacementOpMethod.ReturnType.IsAssignableTo(
               returnAbsoluteRuntimeType);
    }
    catch (AmbiguousMatchException)
    {
      return false;
    }
  }
}