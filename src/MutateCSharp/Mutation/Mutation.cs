using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using MutateCSharp.Util;
using MutateCSharp.Util.Converters;

namespace MutateCSharp.Mutation;

// Uniquely identifiable replacement:
// the map from original expression/value => list of possible mutations
// 1) Node expression type (input, output)
// 2) Valid mutations
public record Mutation
{
  [JsonInclude]
  public required int MutantId { get; init; }
  
  [JsonInclude]
  public required SyntaxKind OriginalOperation { get; init; }
  
  [JsonInclude]
  public required string OriginalExpressionTemplate { get; init; }
  
  [JsonInclude]
  public required SyntaxKind MutantOperation { get; init; }
  
  [JsonInclude]
  public required CodeAnalysisUtil.OperandKind MutantOperandKind { get; init; }
  
  [JsonInclude]
  public required string MutantExpressionTemplate { get; init; }
  
  // Records the span of the location of the original expression that does
  // not take into account line breaks and line numbers.
  // Used to convert location back to syntax node.
  [JsonInclude]
  [JsonConverter(typeof(SourceSpanConverter))]
  public required TextSpan SourceSpan { get; init; }
  
  // Records the span of the location of the original expression in the
  // source file, taking into account line breaks and line numbers.
  // Used to display location in a human-friendly representation for
  // information reporting.
  [JsonInclude]
  [JsonConverter(typeof(FileLinePositionSpanConverter))]
  public required FileLinePositionSpan LineSpan { get; init; }
}