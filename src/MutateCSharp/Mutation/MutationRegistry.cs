using System.Collections.Frozen;

namespace MutateCSharp.Mutation;

using MutationId = int;

// update base counter with mutation registry
public class MutationRegistry
{
  // Each entry records the unique mapping of original construct to all
  // mutant constructs (mutation group). Mutation group is only concerned
  // with the operation types, not the specific value instances.
  // This information is useful to omit generation of redundant schemas.
  private ISet<MutationGroup> _mutationGroups = new HashSet<MutationGroup>();

  // A mutation produces a mutant. The mutation-mutant relation is a one-to-one
  // mapping; mutation ID and mutant ID can be used interchangeably.
  // A mutation stores the original and replacement syntax trees.
  private IDictionary<MutationId, Mutation>
    _mutations = new Dictionary<MutationId, Mutation>();

  // There is a many-to-one mapping between a mutation and a mutation group
  private IDictionary<Mutation, MutationGroup>
    _mutationToMutationGroup = new Dictionary<Mutation, MutationGroup>();

  private MutationId _idCounter;
  
  // Precondition: None of the mutations are registered
  public void RegisterMutationGroup(MutationGroup mutationGroup)
  {
    // Mutation groups may have existed since different nodes under mutation
    // can generate an equivalent set of mutations
    _mutationGroups.Add(mutationGroup);
    
    // Update number of mutants created
    _idCounter += mutationGroup.SchemaMutantExpressionsTemplate.Count;
  }

  public Mutation GetMutation(MutationId id)
  {
    return _mutations[id];
  }

  public MutationId GetMutationIdAssignment()
  {
    return _idCounter;
  }

  // Converts all underlying collections to frozen collections to optimise
  // for read-only throughput.
  public void MakeImmutable()
  {
    _mutationGroups = _mutationGroups.ToFrozenSet();
    _mutations = _mutations.ToFrozenDictionary();
  }
}