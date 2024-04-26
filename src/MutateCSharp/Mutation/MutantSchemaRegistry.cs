using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace MutateCSharp.Mutation;

// update base counter with mutation registry
public class MutantSchemaRegistry
{
  // There is a many-to-one mapping between a mutation and a mutation group

  [JsonInclude] 
  private long _idGenerator;

  // Each entry records the unique mapping of original construct to all
  // mutant constructs (mutation group). Mutation group is only concerned
  // with the operation types, not the specific value instances.
  // This information is useful to omit generation of redundant schemas.
  // [JsonInclude]
  private ISet<MutationGroup> _mutationGroups = new HashSet<MutationGroup>();

  // A mutation produces a mutant. The mutation-mutant relation is a one-to-one
  // mapping; mutation ID and mutant ID can be used interchangeably.
  // A mutation stores the original and replacement syntax trees.
  // [JsonInclude] 
  private IDictionary<long, Mutation>
    _mutations = new Dictionary<long, Mutation>();

  public long RegisterMutationGroupAndGetIdAssignment(
    MutationGroup mutationGroup)
  {
    // Mutation groups may have existed since different nodes under mutation
    // can generate an equivalent set of mutations
    _mutationGroups.Add(mutationGroup);

    var baseId = _idGenerator;
    var mutants = mutationGroup.SchemaMutantExpressions;

    foreach (var mutant in mutants)
    {
      var mutation = new Mutation
      {
        MutantId = ++_idGenerator,
        OriginalOperation = mutationGroup.SchemaOriginalExpression.Operation,
        MutantOperation = mutant.Operation,
        OriginalNodeLocation = mutationGroup.OriginalLocation
      };

      _mutations[mutation.MutantId] = mutation;
    }

    return baseId;
  }

  public IReadOnlySet<MutationGroup> GetAllMutationGroups()
  {
    return _mutationGroups.ToFrozenSet();
  }

  public Mutation GetMutation(long mutantId)
  {
    return _mutations[mutantId];
  }
}