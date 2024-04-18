using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.OperatorImplementation;

public sealed partial class PostfixUnaryExprOpReplacer(
  SemanticModel semanticModel)
  : AbstractMutationOperator<PostfixUnaryExpressionSyntax>(semanticModel)
{
  protected override bool CanBeApplied(
    PostfixUnaryExpressionSyntax originalNode)
  {
    return SupportedOperators.ContainsKey(originalNode.Kind());
  }
  
  private static string ExpressionTemplate(SyntaxKind kind)
    => $"{{0}}{SupportedOperators[kind]}";

  protected override string OriginalExpressionTemplate(
    PostfixUnaryExpressionSyntax originalNode)
    => ExpressionTemplate(originalNode.Kind());
  
  private IEnumerable<SyntaxKind> ValidMutants(
    PostfixUnaryExpressionSyntax originalNode)
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
  
  protected override IList<(int, string)> ValidMutantExpressionsTemplate(PostfixUnaryExpressionSyntax originalNode)
  {
    var validMutants = ValidMutants(originalNode);
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(OperatorIds,
        validMutants);
    return attachIdToMutants.Select(entry =>
      (entry.Item1, ExpressionTemplate(entry.Item2))).ToList();
  }
  protected override IList<string> ParameterTypes(
    PostfixUnaryExpressionSyntax originalNode)
  {
    return [SemanticModel.GetTypeInfo(originalNode.Operand).Type!.ToDisplayString()];
  }

  protected override string ReturnType(
    PostfixUnaryExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type!.ToDisplayString();
  }

  protected override string SchemaBaseName(
    PostfixUnaryExpressionSyntax originalNode)
  {
    return "ReplacePostfixUnaryExpressionOperator";
  }
}

public sealed partial class PostfixUnaryExprOpReplacer
{
  private static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.UnaryOp>
      {
        {
          SyntaxKind.PostIncrementExpression,
          new(ExprKind: SyntaxKind.PostIncrementExpression,
            TokenKind: SyntaxKind.PlusPlusToken,
            TypeSignatures: CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        },
        {
          SyntaxKind.PostDecrementExpression,
          new(ExprKind: SyntaxKind.PostDecrementExpression,
            TokenKind: SyntaxKind.MinusMinusToken,
            TypeSignatures: CodeAnalysisUtil.IncrementOrDecrementTypeSignature)
        }
      }.ToFrozenDictionary();

  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys)
      .ToFrozenDictionary();
}

public sealed partial class PostfixUnaryExprOpReplacer
{
  /*
   * For an operator to be applicable to a special type:
   * 1) The replacement operator must differ from the original operator;
   * 2) The replacement operator must support the same parameter types as the original operator;
   * 3) The replacement return type must be assignable to the original return type.
   */
  private bool CanApplyOperatorForSpecialTypes(
    PostfixUnaryExpressionSyntax originalNode,
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
       public static A operator --(A a1) => a1;
     }
     
   * Given the following statement:
   * var x = op1 a
   * where:
   * a is of type A or A?,
   * op1 is the original binary operator,
   * op2 is the replacement binary operator,
   *
   * the following should hold:
   * 1) op2 should exist in A;
   * 2) op2 should take a parameters of type A/A?;
   * 3) op2 should return type A/A?.
   */
  private bool CanApplyOperatorForUserDefinedTypes(
    PostfixUnaryExpressionSyntax originalNode, IMethodSymbol replacementOpMethod)
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