using Microsoft.CodeAnalysis;

namespace MutateCSharp.Mutation;

public interface IMutationOperator
{
  public bool CanBeApplied(SyntaxNode? originalNode);

  public MutationGroup? CreateMutationGroup(SyntaxNode? originalNode);
}