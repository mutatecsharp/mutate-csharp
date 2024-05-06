using System.Collections.Frozen;
using Microsoft.CodeAnalysis.CSharp;

namespace MutateCSharp.Util;

public static class SyntaxKindUniqueIdGenerator
{
  public static FrozenDictionary<SyntaxKind, int> GenerateIds(
    IOrderedEnumerable<SyntaxKind> sortedOperators)
  {
    var itemIds = new Dictionary<SyntaxKind, int>();
    var currentId = 0;

    foreach (var item in sortedOperators)
    {
      currentId++;
      itemIds[item] = currentId;
    }

    return itemIds.ToFrozenDictionary();
  }

  // Precondition: predefinedIds contain entries corresponding to each element in syntaxKinds
  public static IEnumerable<(int id, SyntaxKind op)> ReturnSortedIdsToKind(
    FrozenDictionary<SyntaxKind, int> predefinedIds,
    IEnumerable<SyntaxKind> syntaxKinds)
  {
    return syntaxKinds
        .Select(kind => (id: predefinedIds[kind], op: kind))
        .Order();
  }
}