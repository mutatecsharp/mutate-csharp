using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

/*
 * Note: we handle updatable parameters differently from non-updatable parameters.
 * By default, since value types are passed by value in C# whereas
 * user-defined types are passed by reference, we take care to pass value types
 * by reference instead
 */
public sealed partial class PrefixUnaryExprOpReplacer(SemanticModel semanticModel)
: AbstractMutationOperator<PrefixUnaryExpressionSyntax>(semanticModel)
{
  protected override bool CanBeApplied(PrefixUnaryExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }

  private static string ExpressionTemplate(SyntaxKind kind)
   => $"{SupportedOperators[kind]}{{0}}";

  protected override string OriginalExpressionTemplate(PrefixUnaryExpressionSyntax originalNode)
   => ExpressionTemplate(originalNode.Kind());

  private IEnumerable<SyntaxKind> ValidMutants(
    PrefixUnaryExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type!;
    
    // Case 1: Predefined types
    if (type.SpecialType != SpecialType.None)
    {
      return SupportedOperators
        .Where(replacementOpEntry =>
          CanApplyOperatorForSpecialTypes(originalNode,
            replacementOpEntry.Value))
        .Select(replacementOpEntry => replacementOpEntry.Key);
    }
    
    // Case 2: User-defined types (type.SpecialType == SpecialType.None)
    if (type is INamedTypeSymbol customType)
    {
      var overloadedOperators =
        CodeAnalysisUtil.GetOverloadedOperatorsInUserDefinedType(customType);
      return overloadedOperators
        .Where(replacementOpMethodEntry =>
          CanApplyOperatorForUserDefinedTypes(originalNode,
            replacementOpMethodEntry.Value))
        .Select(replacementOpMethodEntry => replacementOpMethodEntry.Key);
    }

    return Array.Empty<SyntaxKind>();
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(PrefixUnaryExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return attachIdToMutants.Select(entry =>
      (entry.Item1, ExpressionTemplate(entry.Item2))).ToList();
  }

  protected override IList<string> ParameterTypes(PrefixUnaryExpressionSyntax originalNode)
  {
    return [SemanticModel.GetTypeInfo(originalNode.Operand).Type!.ToDisplayString()];
  }

  protected override string ReturnType(PrefixUnaryExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
  }

  protected override string SchemaBaseName(PrefixUnaryExpressionSyntax originalNode)
  {
    return "ReplacePrefixUnaryExpressionOperator";
  }
}

public sealed partial class PrefixUnaryExprOpReplacer
{
  public static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
      {
        {
          SyntaxKind.UnaryPlusExpression, // +x
          new (ExprKind: SyntaxKind.UnaryPlusExpression,
          TokenKind: SyntaxKind.PlusToken,
          TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.UnaryMinusExpression, // -x
          new(ExprKind: SyntaxKind.UnaryMinusExpression,
            TokenKind: SyntaxKind.MinusToken,
            TypeSignatures: CodeAnalysisUtil.ArithmeticTypeSignature)
        },
        {
          SyntaxKind.BitwiseNotExpression, // ~x
          new(ExprKind: SyntaxKind.BitwiseNotExpression,
            TokenKind: SyntaxKind.TildeToken,
            TypeSignatures: CodeAnalysisUtil.BitwiseLogicalTypeSignature)
        },
        {
          SyntaxKind.LogicalNotExpression, // !x
          new(ExprKind: SyntaxKind.LogicalNotExpression,
            TokenKind: SyntaxKind.ExclamationToken,
            TypeSignatures: CodeAnalysisUtil.BooleanLogicalTypeSignature)
        },
        {
          SyntaxKind.PreIncrementExpression, // ++x
          new(ExprKind: SyntaxKind.PreIncrementExpression,
            TokenKind: SyntaxKind.PlusPlusToken,
            TypeSignatures: CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        },
        {
          SyntaxKind.PreDecrementExpression, // --x
          new(ExprKind: SyntaxKind.PreDecrementExpression,
            TokenKind: SyntaxKind.MinusMinusToken,
            TypeSignatures: CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}

public sealed partial class PrefixUnaryExprOpReplacer
{
  /*
   * For an operator to be applicable to a special type:
   * 1) The replacement operator must differ from the original operator;
   * 2) The replacement operator must support the same parameter types as the original operator;
   * 3) The replacement return type must be assignable to the original return type.
   */
  private bool CanApplyOperatorForSpecialTypes(
    PrefixUnaryExpressionSyntax originalNode,
    CodeAnalysisUtil.UnaryOp replacementOp)
  {
    // Operator checks
    // Reject if the replacement candidate is the same as the original operator
    if (originalNode.Kind() == replacementOp.ExprKind) return false;
    // Reject if the original operator is not supported
    if (!SupportedOperators.ContainsKey(originalNode.Kind())) return false;
    
    // Type checks
    var returnType = SemanticModel.GetTypeInfo(originalNode).Type!;
    var operandType = SemanticModel.GetTypeInfo(originalNode.Operand).Type!;

    var returnTypeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(returnType.SpecialType);
    var operandTypeClassification =
      CodeAnalysisUtil.GetSpecialTypeClassification(operandType.SpecialType);
    
    // Reject if the replacement operator type group is not the same as the
    // original operator type group
    return replacementOp.TypeSignatures.Any(
      signature => signature.OperandType.HasFlag(operandTypeClassification)
                   && signature.ReturnType.HasFlag(returnTypeClassification));

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
  private bool CanApplyOperatorForUserDefinedTypes(
    PrefixUnaryExpressionSyntax originalNode, IMethodSymbol replacementOpMethod)
  {
    // 1) Get the parameter types for the replacement operator
    if (replacementOpMethod.Parameters is not [var replacementOperand]) return false;

    // 2) Get the types of variables involved in the original unary operator
    var originalOperandType = SemanticModel.GetTypeInfo(originalNode.Operand).Type;
    var originalReturnType = SemanticModel.GetTypeInfo(originalNode).Type;

    // 3) Check that the types are assignable:
    // Operand type (original should be assignable to replacement)
    var checkOperandAssignable =
      originalOperandType?.GetType().IsAssignableTo(replacementOperand.Type.GetType()) ??
      false;

    if (!checkOperandAssignable) return false;
    
    // Return type (replacement should be assignable to original)
    return originalReturnType != null && replacementOpMethod.ReturnType
      .GetType().IsAssignableTo(originalReturnType.GetType());
  }
}