using System.Collections.Frozen;
using System.Diagnostics;

namespace MutateCSharp.Mutation;

using MutationId = long;

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
  
  // Precondition: None of the mutations are registered
  public void RegisterMutationGroup(MutationGroup mutationGroup)
  {
    _mutationGroups.Add(mutationGroup);
    
    // Register mutation(s)
    foreach (var mutation in mutationGroup.Mutations)
    {
      Trace.Assert(!_mutations.ContainsKey(mutation.Id));
      _mutations[mutation.Id] = mutation;
    }
  }

  public Mutation GetMutation(MutationId id)
  {
    return _mutations[id];
  }

  // Converts all underlying collections to frozen collections to optimise
  // for read-only throughput.
  public void MakeImmutable()
  {
    _mutationGroups = _mutationGroups.ToFrozenSet();
    _mutations = _mutations.ToFrozenDictionary();
  }
}