using Microsoft.CodeAnalysis;

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
  private static long _idGenerator;
  // For Roslyn use (original node may contain replacement children nodes)
  public SyntaxNode RoslynOriginalNode { get; }
  public SyntaxNode RoslynReplacementNode { get; }
  
  // For internal use
  public long Id { get; }

  public Mutation(SyntaxNode originalNode, SyntaxNode replacementNode)
  {
    _idGenerator++;
    RoslynOriginalNode = originalNode;
    RoslynReplacementNode = replacementNode;
    Id = _idGenerator;
  }
}