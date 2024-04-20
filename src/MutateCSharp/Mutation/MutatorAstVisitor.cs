using System.Collections.Frozen;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.OperatorImplementation;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation;

/*
 * Discover target nodes that are eligible to undergo mutation, and apply mutation.
 */
public class MutatorAstRewriter(Assembly sutAssembly, SemanticModel semanticModel)
  : CSharpSyntaxRewriter
{
  private readonly ISet<IMutationOperator> _mutationOperators =
    new HashSet<IMutationOperator>
    {
      new BooleanConstantReplacer(sutAssembly, semanticModel),
      new NumericConstantReplacer(sutAssembly, semanticModel),
      new StringConstantReplacer(sutAssembly, semanticModel)
    }.ToFrozenSet();

  private readonly MutationRegistry _registry = new();

  // There should be at most one mutation operator that can be applied to the
  // current node, since each mutation operator apply to a disjoint set of nodes
  private IMutationOperator? LocateMutationOperator(SyntaxNode currentNode)
  {
    var mutationOperator =
      _mutationOperators.Where(m => m.CanBeApplied(currentNode)).ToList();
    Trace.Assert(mutationOperator.Count <= 1,
      "There should be at most one qualifying mutation operator.");
    return mutationOperator.FirstOrDefault();
  }

  public override SyntaxNode VisitLiteralExpression(
    LiteralExpressionSyntax node)
  {
    // 1: Mutate all children nodes
    var nodeWithMutatedChildren = (LiteralExpressionSyntax) base.VisitLiteralExpression(node)!;
    
    // 2. Identify possible mutation operator
    var mutationOperator = LocateMutationOperator(node);
    if (mutationOperator is null) return nodeWithMutatedChildren;
      
    // 3. Apply mutation operator to obtain possible mutations
    var mutationGroup = mutationOperator.CreateMutationGroup(node);
    if (mutationGroup is null) return nodeWithMutatedChildren;
    
    // 4. Get assignment of mutant IDs for the mutation group
    var baseMutantId = _registry.GetMutationIdAssignment();
    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));
    
    // 5. Mutate node
    var mutatedNode = SyntaxFactoryUtil.CreateMethodCall(
      MutantSchemataGenerator.Namespace,
      MutantSchemataGenerator.Class,
      mutationGroup.SchemaName,
      baseMutantIdLiteral,
      nodeWithMutatedChildren
    );

    // 6. Register mutation group
    _registry.RegisterMutationGroup(mutationGroup);

    // 7. Finish processing
    return mutatedNode;
  }
}