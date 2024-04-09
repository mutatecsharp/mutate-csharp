using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
  public ExpressionSyntax RoslynOriginalNode { get; }
  public ExpressionSyntax RoslynReplacementNode { get; }
  
  // For internal use
  public long Id { get; }
  public SyntaxKind OriginalOperation { get; }
  public SyntaxKind MutantOperation { get; }
  public string OperandType { get; }

  public Mutation(ExpressionSyntax originalNode, ExpressionSyntax replacementNode, TypeInfo operandType)
  {
    _idGenerator++;
    RoslynOriginalNode = originalNode;
    RoslynReplacementNode = replacementNode;
    OriginalOperation = originalNode.Kind();
    MutantOperation = replacementNode.Kind();
    OperandType = operandType.ToString()!;
    Id = _idGenerator;
  }
}