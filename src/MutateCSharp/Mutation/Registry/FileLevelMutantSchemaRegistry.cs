using System.Collections.Frozen;

namespace MutateCSharp.Mutation.Registry;

// update base counter with mutation registry
public class FileLevelMutantSchemaRegistry
{
  // There is a many-to-one mapping between a mutation and a mutation group
  private long _idGenerator = 1;

  // Each entry records the unique mapping of original construct to all
  // mutant constructs (mutation group). Mutation group is only concerned
  // with the operation types, not the specific value instances.
  // This information is useful to omit generation of redundant schemas.
  private ISet<MutationGroup> _mutationGroups = new HashSet<MutationGroup>();

  private IDictionary<long, MutationGroup> _baseIdToMutationGroup
    = new Dictionary<long, MutationGroup>();

  public long RegisterMutationGroupAndGetIdAssignment(
    MutationGroup mutationGroup)
  {
    var baseId = _idGenerator;
    
    // Mutation groups may have existed since different nodes under mutation
    // can generate an equivalent set of mutations
    _mutationGroups.Add(mutationGroup);
    _baseIdToMutationGroup[baseId] = mutationGroup;
    _idGenerator += mutationGroup.SchemaMutantExpressions.Count;
    return baseId;
  }

  public IReadOnlySet<MutationGroup> GetAllMutationGroups()
  {
    return _mutationGroups.ToFrozenSet();
  }

  public FileLevelMutationRegistry ToMutationRegistry(string fileRelativePath)
  {
    var mutations = new Dictionary<long, Mutation>();
    
    foreach (var (baseId, group) in _baseIdToMutationGroup)
    {
      for (var i = 0; i < group.SchemaMutantExpressions.Count; i++)
      {
        var mutation = new Mutation
        {
          MutantId = baseId + i,
          OriginalOperation = group.SchemaOriginalExpression.Operation,
          MutantOperation = group.SchemaMutantExpressions[i].Operation,
          SourceSpan = group.OriginalLocation.SourceSpan,
          LineSpan = group.OriginalLocation.GetLineSpan()
        };

        mutations[mutation.MutantId] = mutation;
      }
    }

    return new FileLevelMutationRegistry
    {
      FileRelativePath = fileRelativePath,
      EnvironmentVariable = MutantSchemataGenerator.EnvVar,
      Mutations = mutations.ToFrozenDictionary()
    };
  }
}