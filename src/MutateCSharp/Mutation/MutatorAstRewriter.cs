using System.Collections.Frozen;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.OperatorImplementation;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation;

/*
 * Discover target nodes that are eligible to undergo mutation, and apply mutation.
 */
public sealed partial class MutatorAstRewriter(
  Assembly sutAssembly,
  SemanticModel semanticModel,
  FileLevelMutantSchemaRegistry schemaRegistry)
  : CSharpSyntaxRewriter
{
  private readonly FrozenDictionary<Type, IMutationOperator[]>
    _mutationOperators
      = new Dictionary<Type, IMutationOperator[]>
      {
        [typeof(LiteralExpressionSyntax)] =
        [
          new BooleanConstantReplacer(sutAssembly, semanticModel),
          new StringConstantReplacer(sutAssembly, semanticModel),
          new NumericConstantReplacer(sutAssembly, semanticModel)
        ],
        [typeof(PrefixUnaryExpressionSyntax)] =
          [new PrefixUnaryExprOpReplacer(sutAssembly, semanticModel)],
        [typeof(PostfixUnaryExpressionSyntax)] =
          [new PostfixUnaryExprOpReplacer(sutAssembly, semanticModel)],
        [typeof(BinaryExpressionSyntax)] =
          [new BinExprOpReplacer(sutAssembly, semanticModel)]
      }.ToFrozenDictionary();

  // There should be at most one mutation operator that can be applied to the
  // current node, since each mutation operator apply to a disjoint set of nodes
  private IMutationOperator? LocateMutationOperator(SyntaxNode currentNode)
  {
    var baseType = currentNode.GetType();
    if (!_mutationOperators.TryGetValue(baseType, out var mutationOperators))
      return null;

    var mutationOperator =
      mutationOperators.Where(m => m.CanBeApplied(currentNode)).ToList();
    Trace.Assert(mutationOperator.Count <= 1,
      "There should be at most one qualifying mutation operator.");
    return mutationOperator.FirstOrDefault();
  }

  // A file cannot contain both file scoped namespace declaration and
  // ordinary namespace declaration; we convert the file scoped namespace
  // declaration to allow injection of mutant schemata
  public override SyntaxNode VisitFileScopedNamespaceDeclaration(
    FileScopedNamespaceDeclarationSyntax node)
  {
    // 1: Mutate all children nodes
    var nodeWithMutatedChildren =
      (FileScopedNamespaceDeclarationSyntax)base
        .VisitFileScopedNamespaceDeclaration(node)!;

    // 2: Replace file scoped namespace with ordinary namespace
    return SyntaxFactory.NamespaceDeclaration(nodeWithMutatedChildren.Name)
      .WithMembers(nodeWithMutatedChildren.Members)
      .WithLeadingTrivia(nodeWithMutatedChildren.GetLeadingTrivia())
      .WithTrailingTrivia(nodeWithMutatedChildren.GetTrailingTrivia());
  }

  public override SyntaxNode VisitLiteralExpression(
    LiteralExpressionSyntax node)
  {
    // 1: Mutate all children nodes
    var nodeWithMutatedChildren =
      (LiteralExpressionSyntax)base.VisitLiteralExpression(node)!;

    // 2: Apply mutation operator to obtain possible mutations
    var mutationGroup = LocateMutationOperator(node)?.CreateMutationGroup(node);
    if (mutationGroup is null) return nodeWithMutatedChildren;

    // 3: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));

    // 4: Mutate node
    return SyntaxFactoryUtil.CreateMethodCall(
      MutantSchemataGenerator.Namespace,
      MutantSchemataGenerator.Class,
      mutationGroup.SchemaName,
      baseMutantIdLiteral,
      nodeWithMutatedChildren
    );
  }

  public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
  {
    // 1: Mutate all children nodes
    var nodeWithMutatedChildren =
      (BinaryExpressionSyntax)base.VisitBinaryExpression(node)!;

    // 2: Apply mutation operator to obtain possible mutations
    var mutationGroup = LocateMutationOperator(node)?.CreateMutationGroup(node);
    if (mutationGroup is null) return nodeWithMutatedChildren;

    // 3: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));

    // 4: Mutate node
    return SyntaxFactoryUtil.CreateMethodCall(
      MutantSchemataGenerator.Namespace,
      MutantSchemataGenerator.Class,
      mutationGroup.SchemaName,
      baseMutantIdLiteral,
      nodeWithMutatedChildren.Left,
      nodeWithMutatedChildren.Right
    );
  }


  public override SyntaxNode VisitPrefixUnaryExpression(
    PrefixUnaryExpressionSyntax node)
  {
    // 1: Mutate all children nodes
    var nodeWithMutatedChildren =
      (PrefixUnaryExpressionSyntax)base.VisitPrefixUnaryExpression(node)!;

    // 2: Apply mutation operator to obtain possible mutations
    var mutationGroup = LocateMutationOperator(node)?.CreateMutationGroup(node);
    if (mutationGroup is null) return nodeWithMutatedChildren;

    // 3: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));

    // 4: Add ref keyword to parameter if updatable
    var operand = SyntaxFactory.Argument(nodeWithMutatedChildren.Operand);
    if (mutationGroup.SchemaParameterTypes.FirstOrDefault()
          ?.StartsWith("ref") ?? false)
      operand =
        operand.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));

    // 5: Mutate node
    return SyntaxFactoryUtil.CreateMethodCallWithFormedArguments(
      MutantSchemataGenerator.Namespace,
      MutantSchemataGenerator.Class,
      mutationGroup.SchemaName,
      SyntaxFactory.Argument(baseMutantIdLiteral),
      operand
    );
  }

  public override SyntaxNode VisitPostfixUnaryExpression(
    PostfixUnaryExpressionSyntax node)
  {
    // 1: Mutate all children nodes
    var nodeWithMutatedChildren =
      (PostfixUnaryExpressionSyntax)base.VisitPostfixUnaryExpression(node)!;

    // 2: Apply mutation operator to obtain possible mutations
    var mutationGroup = LocateMutationOperator(node)?.CreateMutationGroup(node);
    if (mutationGroup is null) return nodeWithMutatedChildren;

    // 3: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));

    // 4: Add ref keyword to parameter if updatable
    var operand = SyntaxFactory.Argument(nodeWithMutatedChildren.Operand);
    if (mutationGroup.SchemaParameterTypes.FirstOrDefault()
          ?.StartsWith("ref") ?? false)
      operand =
        operand.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));

    // 5: Mutate node
    return SyntaxFactoryUtil.CreateMethodCallWithFormedArguments(
      MutantSchemataGenerator.Namespace,
      MutantSchemataGenerator.Class,
      mutationGroup.SchemaName,
      SyntaxFactory.Argument(baseMutantIdLiteral),
      operand
    );
  }
}

/*
 * Special cases to handle programming constructs that should not be mutated,
 * as the values need to be known at compile-time.
 */
public sealed partial class MutatorAstRewriter
{
  public override SyntaxNode VisitEnumMemberDeclaration(
    EnumMemberDeclarationSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitCaseSwitchLabel(CaseSwitchLabelSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitAttributeList(AttributeListSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitParameterList(ParameterListSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitBracketedParameterList(
    BracketedParameterListSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitTypeParameterList(
    TypeParameterListSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitCrefParameterList(
    CrefParameterListSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitCrefBracketedParameterList(
    CrefBracketedParameterListSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitFunctionPointerParameterList(
    FunctionPointerParameterListSyntax node)
  {
    return node;
  }

  public override SyntaxNode
    VisitRecursivePattern(RecursivePatternSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitUsingDirective(UsingDirectiveSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
  {
    return node.Modifiers.Any(modifier =>
      modifier.IsKind(SyntaxKind.ConstKeyword))
      ? node
      : base.VisitFieldDeclaration(node)!;
  }

  public override SyntaxNode VisitLocalDeclarationStatement(
    LocalDeclarationStatementSyntax node)
  {
    return node.IsConst ? node : base.VisitLocalDeclarationStatement(node)!;
  }
}