using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Mutation;

public abstract class AbstractMutationOperator<T>(SemanticModel semanticModel)
  : IMutationOperator 
  where T : ExpressionSyntax
{
  protected readonly SemanticModel SemanticModel = semanticModel;

  // Check that mutation operator can be applied on currently visited node.
  // Should be run before other methods in this class are called.
  public bool CanBeApplied(ExpressionSyntax? originalNode)
  {
    // The type check guarantees originalNode cannot be null
    return originalNode is T node && CanBeApplied(node);
  }

  public MutationGroup? CreateMutationGroup(ExpressionSyntax? originalNode)
  {
    // Guard against nullable values in the validation check
    if (!CanBeApplied(originalNode)) return null;
    var node = (T)originalNode!;

    var mutationsWithId = ValidMutantExpressionsTemplate(node);
    if (mutationsWithId.Count == 0) return null;

    var mutations = mutationsWithId.Select(entry => entry.Item2);
    var uniqueMutantsId =
      string.Join("", mutationsWithId.Select(entry => entry.Item1));

    return new MutationGroup
    {
      SchemaName = $"{SchemaBaseName(node)}{uniqueMutantsId}",
      SchemaParameterTypes = ParameterTypes(node),
      SchemaReturnType = ReturnType(node),
      SchemaOriginalExpressionTemplate =
        OriginalExpressionTemplate(node),
      SchemaMutantExpressionsTemplate = mutations.ToImmutableArray()
    };
  }

  protected abstract bool CanBeApplied(T originalNode);

  protected abstract string OriginalExpressionTemplate(T originalNode);

  // Generate list of valid mutations for the currently visited node, in the
  // form of string template to be formatted later to insert arguments.
  // Each valid name is given an ID that is unique within the context of the
  // mutation operator class.
  protected abstract IList<(int, string)> ValidMutantExpressionsTemplate(
    T originalNode);

  // Parameter type of the programming construct.
  protected abstract IList<string> ParameterTypes(T originalNode);

  // Return type of the programming construct (expression, statement)
  // represented by type of node under mutation.
  protected abstract string ReturnType(T originalNode);

  // The method name used to identify the replacement operation.
  // Note: this is not unique as there can be multiple expressions of the
  // same form and type replaced; these can call the same method
  // (The deduplication process will be handled in MutantSchemataGenerator)
  protected abstract string SchemaBaseName(T originalNode);
}