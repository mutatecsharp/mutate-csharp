using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Mutation;

public interface IMutationOperator
{
  public bool CanBeApplied(ExpressionSyntax? originalNode);

  public MutationGroup? CreateMutationGroup(ExpressionSyntax? originalNode);
}