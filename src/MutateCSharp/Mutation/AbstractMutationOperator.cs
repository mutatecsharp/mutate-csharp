using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation;

public abstract class AbstractMutationOperator<T>(
  Assembly assembly,
  SemanticModel semanticModel)
  : IMutationOperator
  where T : SyntaxNode
{
  protected readonly SemanticModel SemanticModel = semanticModel;
  protected readonly Assembly SutAssembly = assembly;

  // Check that mutation operator can be applied on currently visited node.
  // Should be run before other methods in this class are called.
  public bool CanBeApplied(SyntaxNode? originalNode)
  {
    // The type check guarantees originalNode cannot be null
    return originalNode is T node && CanBeApplied(node);
  }
  
  public MutationGroup? CreateMutationGroup(SyntaxNode? originalNode, 
    ITypeSymbol? requiredReturnType)
  {
    // Guard against nullable values in the validation check
    if (!CanBeApplied(originalNode)) return null;
    var node = (T)originalNode!;
    var typeSymbols = NonMutatedTypeSymbols(node, requiredReturnType);
    if (typeSymbols is null) return null;
    
    var sortedMutations = ValidMutantExpressions(node, requiredReturnType);
    if (sortedMutations.Length == 0) return null;
    
    // Remove all non-alphanumeric characters in schema's base name that contains the return type
    var schemaName = string.Concat(SchemaBaseName().Where(char.IsLetterOrDigit));
    
    return new MutationGroup
    {
      SchemaName = schemaName,
      SchemaParameterTypes = SchemaParameterTypeDisplays(node, sortedMutations, requiredReturnType),
      ParameterTypeSymbols = typeSymbols.OperandTypes,
      SchemaReturnType = SchemaReturnTypeDisplay(node, sortedMutations, requiredReturnType),
      ReturnTypeSymbol = typeSymbols.ReturnType,
      SchemaOriginalExpression = OriginalExpression(node, sortedMutations, requiredReturnType),
      SchemaMutantExpressions = sortedMutations,
      OriginalLocation = node.GetLocation()
    };
  }

  protected abstract bool CanBeApplied(T originalNode);

  protected abstract ExpressionRecord OriginalExpression(T originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType);

  // Generate list of valid mutations for the currently visited node, in the
  // form of string template to be formatted later to insert arguments.
  // Each valid candidate is statically assigned an ID that is unique within
  // the context of the mutation operator class.
  protected abstract
    ImmutableArray<ExpressionRecord>
    ValidMutantExpressions(T originalNode, ITypeSymbol? requiredReturnType);
  
  /*
   * Type symbols of the syntax node under mutation before it is mutated.
   * Value types may be nullable; reference types are guaranteed to be non-nullable.
   * Return type may be nullable regardless of value type or reference type.
   */
  protected abstract CodeAnalysisUtil.MethodSignature?
    NonMutatedTypeSymbols(T originalNode, ITypeSymbol? requiredReturnType);
  
  protected abstract ImmutableArray<string> 
    SchemaParameterTypeDisplays(T originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions, ITypeSymbol? requiredReturnType);

  protected abstract string SchemaReturnTypeDisplay(T originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions,
    ITypeSymbol? requiredReturnType);

  // The method name used to identify the replacement operation.
  // Note: this is not unique as there can be multiple expressions of the
  // same form and type replaced; these can call the same method
  // (The deduplication process will be handled in MutantSchemataGenerator)
  protected abstract string SchemaBaseName();
}