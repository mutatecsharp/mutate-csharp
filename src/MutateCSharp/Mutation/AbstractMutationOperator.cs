using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;

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

  public MutationGroup? CreateMutationGroup(SyntaxNode? originalNode)
  {
    // Guard against nullable values in the validation check
    if (!CanBeApplied(originalNode)) return null;
    var node = (T)originalNode!;

    var mutationsWithId = ValidMutantExpressions(node);
    if (mutationsWithId.Length == 0) return null;

    var mutations =
      mutationsWithId.Select(entry => entry.expr).ToImmutableArray();
    var uniqueMutantsId =
      string.Join("", mutationsWithId.Select(entry => entry.exprIdInMutator));

    // Replace (?) characters in schema's base name that contains the return type
    var schemaName = SchemaBaseName(node).Replace("?", "Nullable");
    
    return new MutationGroup
    {
      SchemaName = $"{schemaName}{uniqueMutantsId}",
      SchemaParameterTypes = ParameterTypes(node, mutations),
      SchemaReturnType = ReturnType(node),
      SchemaOriginalExpression = OriginalExpression(node, mutations),
      SchemaMutantExpressions = mutations,
      OriginalLocation = node.GetLocation()
    };
  }

  protected abstract bool CanBeApplied(T originalNode);

  protected abstract ExpressionRecord OriginalExpression(T originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions);

  // Generate list of valid mutations for the currently visited node, in the
  // form of string template to be formatted later to insert arguments.
  // Each valid candidate is statically assigned an ID that is unique within
  // the context of the mutation operator class.
  protected abstract
    ImmutableArray<(int exprIdInMutator, ExpressionRecord expr)>
    ValidMutantExpressions(T originalNode);

  // Parameter type of the programming construct.
  protected abstract ImmutableArray<string> ParameterTypes(T originalNode,
    ImmutableArray<ExpressionRecord> mutantExpressions);

  // Return type of the programming construct (expression, statement)
  // represented by type of node under mutation.
  protected abstract string ReturnType(T originalNode);

  // The method name used to identify the replacement operation.
  // Note: this is not unique as there can be multiple expressions of the
  // same form and type replaced; these can call the same method
  // (The deduplication process will be handled in MutantSchemataGenerator)
  protected abstract string SchemaBaseName(T originalNode);
}