using System.Collections.Frozen;

namespace MutateCSharp.Mutation.Registry;

using MutantId = long;
using SchemaSuffixId = int;

public class FileLevelMutantSchemaRegistry
{
  // Id generator used to uniquely identify generated class and environment variable
  private static int _fileIdGenerator;
  
  // There is a many-to-one mapping between a mutation and a mutation group
  private MutantId _mutantIdGenerator;
  
  // There is a one-to-one mapping between a mutation group and a suffix ID
  private SchemaSuffixId _suffixIdGenerator;

  // Each entry records the unique mapping of original construct to all
  // mutant constructs (mutation group). Mutation group is only concerned
  // with the operation types, not the specific value instances.
  // This information is useful to omit generation of redundant schemas.
  private readonly Dictionary<MutationGroup, SchemaSuffixId> _mutationGroupsToSuffix;

  private readonly Dictionary<MutantId, MutationGroup> _baseIdToMutationGroup;
  
  public static Type MutantIdType { get; } = typeof(MutantId);
  
  public string ClassName { get; private init; }

  public string EnvironmentVariable { get; private init; }

  public FileLevelMutantSchemaRegistry()
  {
    _mutantIdGenerator = 1;
    // Every file id is guaranteed to be unique
    var fileId = Interlocked.Increment(ref _fileIdGenerator);
    ClassName = $"Schemata{fileId}";
    EnvironmentVariable = $"MUTATE_CSHARP_ACTIVATED_MUTANT{fileId}";
    _mutationGroupsToSuffix = new Dictionary<MutationGroup, SchemaSuffixId>();
    _baseIdToMutationGroup = new Dictionary<MutantId, MutationGroup>();
  }
  
  public MutantId RegisterMutationGroupAndGetIdAssignment(MutationGroup mutationGroup)
  {
    var baseId = _mutantIdGenerator;
    
    // Mutation groups may have existed since different nodes under mutation
    // can generate an equivalent set of mutations
    if (!_mutationGroupsToSuffix.ContainsKey(mutationGroup))
      _mutationGroupsToSuffix[mutationGroup] = _suffixIdGenerator++;
    _baseIdToMutationGroup[baseId] = mutationGroup;
    _mutantIdGenerator += mutationGroup.SchemaMutantExpressions.Length;
    return baseId;
  }

  public string GetUniqueSchemaName(MutationGroup mutationGroup)
  {
    return $"{mutationGroup.SchemaName}_{_mutationGroupsToSuffix[mutationGroup]}";
  }

  public IReadOnlySet<MutationGroup> GetAllMutationGroups()
  {
    return _mutationGroupsToSuffix.Keys.ToFrozenSet();
  }

  public FileLevelMutationRegistry ToMutationRegistry(string fileRelativePath)
  {
    var mutations = new Dictionary<MutantId, Mutation>();
    
    foreach (var (baseId, group) in _baseIdToMutationGroup)
    {
      for (var i = 0; i < group.SchemaMutantExpressions.Length; i++)
      {
        var mutation = new Mutation
        {
          MutantId = baseId + i,
          OriginalOperation = group.SchemaOriginalExpression.Operation,
          OriginalExpressionTemplate = group.SchemaOriginalExpression.ExpressionTemplate,
          MutantOperation = group.SchemaMutantExpressions[i].Operation,
          MutantOperandKind = group.SchemaMutantExpressions[i].Operand,
          MutantExpressionTemplate = group.SchemaMutantExpressions[i].ExpressionTemplate,
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