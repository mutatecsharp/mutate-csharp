using System.Collections.Frozen;

namespace MutateCSharp.Mutation.Registry;

using MutantId = long;

public class FileLevelMutantSchemaRegistry
{
  // Id generator used to uniquely identify generated class and environment variable
  private static int _fileIdGenerator;
  
  // There is a many-to-one mapping between a mutation and a mutation group
  private MutantId _mutantIdGenerator;

  // Each entry records the unique mapping of original construct to all
  // mutant constructs (mutation group). Mutation group is only concerned
  // with the operation types, not the specific value instances.
  // This information is useful to omit generation of redundant schemas.
  private ISet<MutationGroup> _mutationGroups = new HashSet<MutationGroup>();

  private IDictionary<MutantId, MutationGroup> _baseIdToMutationGroup
    = new Dictionary<MutantId, MutationGroup>();
  
  public static Type MutantIdType { get; } = typeof(MutantId);
  
  public string ClassName { get; private init; }

  public string EnvironmentVariable { get; private init; }

  public FileLevelMutantSchemaRegistry()
  {
    _mutantIdGenerator = 1;
    ClassName = $"Schemata{_fileIdGenerator}";
    EnvironmentVariable = $"MUTATE_CSHARP_ACTIVATED_MUTANT{_fileIdGenerator}";
    _fileIdGenerator++;
  }
  
  public long RegisterMutationGroupAndGetIdAssignment(
    MutationGroup mutationGroup)
  {
    var baseId = _mutantIdGenerator;
    
    // Mutation groups may have existed since different nodes under mutation
    // can generate an equivalent set of mutations
    _mutationGroups.Add(mutationGroup);
    _baseIdToMutationGroup[baseId] = mutationGroup;
    _mutantIdGenerator += mutationGroup.SchemaMutantExpressions.Count;
    return baseId;
  }

  public IReadOnlySet<MutationGroup> GetAllMutationGroups()
  {
    return _mutationGroups.ToFrozenSet();
  }

  public FileLevelMutationRegistry ToMutationRegistry(string fileRelativePath)
  {
    var mutations = new Dictionary<MutantId, Mutation>();
    
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
      EnvironmentVariable = EnvironmentVariable,
      Mutations = mutations.ToFrozenDictionary()
    };
  }
}