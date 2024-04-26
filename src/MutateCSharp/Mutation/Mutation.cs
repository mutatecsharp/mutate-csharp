using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MutateCSharp.Mutation;

// Uniquely identifiable replacement:
// the map from original expression/value => list of possible mutations
// 1) Node expression type (input, output)
// 2) Valid mutations
// first call base.Visit() in any method you override and use the
// result so that you have the updated version of any nodes that are
// descendants of the current node.
public record Mutation
{
  [JsonInclude]
  public required long MutantId { get; init; }
  [JsonInclude]
  public required SyntaxKind OriginalOperation { get; init; }
  [JsonInclude]
  public required SyntaxKind MutantOperation { get; init; }
  [JsonInclude]
  public required Location OriginalNodeLocation { get; init; }
}