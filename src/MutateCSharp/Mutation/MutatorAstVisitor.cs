using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MutateCSharp.Mutation;

/*
 * Discover target nodes that are eligible to undergo mutation.
 */
public class MutatorAstRewriter(SemanticModel semanticModel) : CSharpSyntaxRewriter
{
  private readonly SemanticModel _semanticModel = semanticModel;
}