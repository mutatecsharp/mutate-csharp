using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;
using Serilog;

namespace MutateCSharp.Mutation.Mutator;

/*
 * Supports insertion of unary not operator, (!).
 */
public sealed partial class UnaryOpInserter(
  Assembly sutAssembly, 
  SemanticModel semanticModel,
  FrozenDictionary<SyntaxKind,
    ImmutableArray<CodeAnalysisUtil.MethodSignature>> builtInOperatorSignatures):
  AbstractMutationOperator<ExpressionSyntax>(sutAssembly, semanticModel)
{
  protected override bool CanBeApplied(ExpressionSyntax originalNode)
  {
    Log.Debug("Processing expression: {SyntaxNode}",
          originalNode.GetText().ToString());

    var node = originalNode as SyntaxNode;
    
      // Ignore: Cannot obtain type information
      if (!SyntaxRewriterUtil.IsTypeResolvableLogged(in SemanticModel, in node))
        return false;

      var type =
        SemanticModel.ResolveTypeSymbol(node)!.GetNullableUnderlyingType();
      // Exclude node from mutation:
      // 1) If type contains generic type parameter;
      // 2) If type is private (and thus inaccessible);
      // 3) If node is supported by another replacer.
      // Include node from mutation:
      // 1) If node is a while or if predicate.
      return
        !SyntaxRewriterUtil.ContainsGenericTypeParameterLogged(in type) &&
        type.GetVisibility() is not CodeAnalysisUtil.SymbolVisibility.Private
        && originalNode is not BinaryExpressionSyntax
          or PrefixUnaryExpressionSyntax
          or PostfixUnaryExpressionSyntax
          or LiteralExpressionSyntax
          or AssignmentExpressionSyntax;
  }

  protected override ExpressionRecord OriginalExpression(ExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType)
  {
    return new ExpressionRecord(originalNode.Kind(),
      CodeAnalysisUtil.OperandKind.None, "{0}");
  }

  protected override ImmutableArray<ExpressionRecord> ValidMutantExpressions(
    ExpressionSyntax originalNode,
    ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not 
        { } methodSignature) return [];
    
    var expressionAbsoluteType =
      methodSignature.OperandTypes[0].GetNullableUnderlyingType();

    IEnumerable<KeyValuePair<SyntaxKind,CodeAnalysisUtil.Op>> validMutators 
      = SupportedOperators;
    
    if (!IsOperandAssignable(originalNode))
    {
      // Remove candidate operators that modify variable if the operand is not
      // assignable (example: !x where x is const and cannot be updated)
      validMutators = validMutators.Where(replacementOpEntry =>
        !CodeAnalysisUtil.VariableModifyingOperators.Contains(
          replacementOpEntry.Key));
    }
    
    // Case 1: Special types(simple types)
    if (expressionAbsoluteType.SpecialType is not SpecialType.None)
    {
      validMutators = validMutators
        .Where(replacementOpEntry =>
          CanApplyOperatorForSpecialTypes(
            replacementOpEntry.Value, methodSignature));
    }
    else
    {
      // Case 2: User-defined types (type.SpecialType == SpecialType.None)
      validMutators = validMutators.Where(replacementOpEntry =>
        CanApplyOperatorForUserDefinedTypes(
          methodSignature.ReturnType,
          methodSignature.OperandTypes[0],
          replacementOpEntry.Value));
    }

    var validOperators = validMutators.Select(op => op.Key);
    
    var attachIdToMutants =
      SyntaxKindUniqueIdGenerator.ReturnSortedIdsToKind(
        OperatorIds, validOperators);
    
    var sortedMutants = attachIdToMutants.Select(entry =>
      new ExpressionRecord(entry.op, CodeAnalysisUtil.OperandKind.None, ExpressionTemplate(entry.op))
    ).ToList();

    return [..sortedMutants];
  }

  protected override CodeAnalysisUtil.MethodSignature NonMutatedTypeSymbols(
    ExpressionSyntax originalNode,
    ITypeSymbol? requiredReturnType)
  {
    var expressionType = SemanticModel.ResolveTypeSymbol(originalNode)!;
    
    // Remove nullable if operand is of reference type, since reference type
    // T? can be cast to T
    if (!expressionType.IsValueType)
      expressionType = expressionType.GetNullableUnderlyingType();

    return new CodeAnalysisUtil.MethodSignature(expressionType, [expressionType]);
  }

  protected override ImmutableArray<string> SchemaParameterTypeDisplays(ExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType)
  {
    if (NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
        { } methodSignature) return [];

    return [methodSignature.OperandTypes[0].ToDisplayString()];
  }

  protected override string SchemaReturnTypeDisplay(ExpressionSyntax originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType)
  {
    return NonMutatedTypeSymbols(originalNode, requiredReturnType) is not
      { } typeSignature
      ? string.Empty
      : typeSignature.ReturnType.ToDisplayString();
  }

  protected override string SchemaBaseName()
  {
    return "UnaryOpInserter";
  }
  
  private static string ExpressionTemplate(SyntaxKind kind)
  {
    return kind.IsSyntaxKindLiteral() 
      ? SupportedOperators[kind].ToString() 
      : $"{SupportedOperators[kind]}{{0}}";
  }
}

public sealed partial class UnaryOpInserter
{
  private bool CanApplyOperatorForSpecialTypes(
    CodeAnalysisUtil.Op replacementOp, 
    CodeAnalysisUtil.MethodSignature originalSignature)
  {
    var mutantExpressionType =
      SemanticModel.ResolveOverloadedPredefinedUnaryOperator(
        builtInOperatorSignatures, replacementOp.ExprKind, originalSignature);
    if (!mutantExpressionType.HasValue) return false;

    var resolvedSignature = mutantExpressionType.Value;
    
    // Check if expression type is assignable to original return type
    return SemanticModel.Compilation.HasImplicitConversion(
             originalSignature.OperandTypes[0], resolvedSignature.operandSymbol)
           && SemanticModel.Compilation.HasImplicitConversion(
             resolvedSignature.returnSymbol, originalSignature.ReturnType);
  }
  
  private bool CanApplyOperatorForUserDefinedTypes(
    ITypeSymbol returnType, 
    ITypeSymbol operandType, 
    CodeAnalysisUtil.Op replacementOp)
  {
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
  
  private bool IsOperandAssignable(ExpressionSyntax originalNode)
  {
    return CodeAnalysisUtil.VariableModifyingOperators.Contains(
             originalNode.Kind())
           || (SemanticModel.GetSymbolInfo(originalNode).Symbol?.IsSymbolVariable() 
               ?? false);
  }
}

public sealed partial class UnaryOpInserter
{
  private static readonly FrozenDictionary<SyntaxKind, CodeAnalysisUtil.Op>
    SupportedOperators
      = new Dictionary<SyntaxKind, CodeAnalysisUtil.Op>
      {
        {
          SyntaxKind.LogicalNotExpression, // !x
          new(SyntaxKind.LogicalNotExpression,
            SyntaxKind.ExclamationToken,
            WellKnownMemberNames.LogicalNotOperatorName)
        }
      }.ToFrozenDictionary();
  
  private static readonly FrozenDictionary<SyntaxKind, int> OperatorIds
    = SyntaxKindUniqueIdGenerator.GenerateIds(SupportedOperators.Keys.Order())
      .ToFrozenDictionary();
}