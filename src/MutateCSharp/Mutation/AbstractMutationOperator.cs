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

    var mutationsWithId = ValidMutantExpressions(node, requiredReturnType);
    if (mutationsWithId.Length == 0) return null;

    var mutations =
      mutationsWithId.Select(entry => entry.expr).ToImmutableArray();
    var uniqueMutantsId =
      string.Join(string.Empty, mutationsWithId.Select(entry => entry.exprIdInMutator));
    
    // Replace (?, .) characters in schema's base name that contains the return type
    var returnTypeDisplay = SchemaReturnTypeDisplay(node, requiredReturnType);
    var schemaName = $"{SchemaBaseName()}Return{returnTypeDisplay}"
      .Replace(".", string.Empty)
      .Replace("?", "Nullable");
    
    return new MutationGroup
    {
      SchemaName = $"{schemaName}{uniqueMutantsId}",
      SchemaParameterTypes = SchemaParameterTypeDisplays(node, mutations, requiredReturnType),
      ParameterTypeSymbols = typeSymbols.OperandTypes,
      SchemaReturnType = returnTypeDisplay,
      ReturnTypeSymbol = typeSymbols.ReturnType,
      SchemaOriginalExpression = OriginalExpression(node, mutations, requiredReturnType),
      SchemaMutantExpressions = mutations,
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
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
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
    ITypeSymbol? requiredReturnType);

  // The method name used to identify the replacement operation.
  // Note: this is not unique as there can be multiple expressions of the
  // same form and type replaced; these can call the same method
  // (The deduplication process will be handled in MutantSchemataGenerator)
  protected abstract string SchemaBaseName();
}