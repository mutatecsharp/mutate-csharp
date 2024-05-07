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

  private static bool ContainsDeclarationPatternSyntax(SyntaxNode node)
  {
    return node.DescendantNodes().OfType<DeclarationPatternSyntax>().Any()
           || node.DescendantNodes().OfType<VarPatternSyntax>().Any();
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
      schemaRegistry.ClassName,
      mutationGroup.SchemaName,
      baseMutantIdLiteral,
      nodeWithMutatedChildren
    );
  }

  public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
  {
    // Pre: do not mutate current node if any of children nodes are declaration pattern syntax
    // Reason: The variable scope will be limited to the mutant schemata
    // and is inaccessible to the current scope
    // It is also not sufficient to contain the declaration pattern syntax intact
    // and pass it as the parameter to the mutant schema method:
    // The compiler's static dataflow analysis is not capable of detecting 
    // if the variable really does match the pattern and does not initialise the
    // declared variable in the pattern.
    //
    // Example:
    // static bool foo(bool x) => x;
    // ...
    // object a = "abc";
    // if (foo(a is string s))
    // (...do something with s) // compile error: Local variable "s" might not be initialized before accessing
    // if (ContainsDeclarationPatternSyntax(node)) return node;
    
    // 1: Mutate children nodes if its descendant does not contain declaration pattern syntax
    var leftContainsDeclarationSyntax =
      ContainsDeclarationPatternSyntax(node.Left);
    var rightContainsDeclarationSyntax =
      ContainsDeclarationPatternSyntax(node.Right);

    var leftChild = leftContainsDeclarationSyntax 
      ? node.Left
      : (ExpressionSyntax)Visit(node.Left);
    var rightChild = rightContainsDeclarationSyntax
      ? node.Right
      : (ExpressionSyntax)Visit(node.Right);
    var nodeWithMutatedChildren =
      node.WithLeft(leftChild).WithRight(rightChild);
    
    // 2: Do not mutate current node if any descendants of current node contain
    // declaration pattern syntax
    if (leftContainsDeclarationSyntax || rightContainsDeclarationSyntax)
      return nodeWithMutatedChildren;

    // 3: Apply mutation operator to obtain possible mutations
    var mutationGroup = LocateMutationOperator(node)?.CreateMutationGroup(node);
    if (mutationGroup is null) return nodeWithMutatedChildren;

    // 4: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));

    // 5: Mutate node
    // Handle short-circuit operators
    var firstParameter = mutationGroup.SchemaParameterTypes[0];
    var secondParameter = mutationGroup.SchemaParameterTypes[1];

    var leftArgument = firstParameter.StartsWith("System.Func")
      ? SyntaxFactory.ParenthesizedLambdaExpression(
        nodeWithMutatedChildren.Left)
      : nodeWithMutatedChildren.Left;

    var rightArgument = secondParameter.StartsWith("System.Func")
      ? SyntaxFactory.ParenthesizedLambdaExpression(
        nodeWithMutatedChildren.Right)
      : nodeWithMutatedChildren.Right;

    return SyntaxFactoryUtil.CreateMethodCall(
      MutantSchemataGenerator.Namespace,
      schemaRegistry.ClassName,
      mutationGroup.SchemaName,
      baseMutantIdLiteral,
      leftArgument,
      rightArgument
    );
  }

  public override SyntaxNode VisitPrefixUnaryExpression(
    PrefixUnaryExpressionSyntax node)
  {
    // Pre: do not mutate if child node is a declaration pattern syntax
    // Reason: The variable scope will be limited to the mutant schemata
    // and is inaccessible to the current scope
    if (ContainsDeclarationPatternSyntax(node.Operand)) return node;

    // 1: Mutate child expression node
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
    var firstParam = mutationGroup.SchemaParameterTypes[0];
    var operand = SyntaxFactory.Argument(nodeWithMutatedChildren.Operand);
    if (firstParam.StartsWith("ref"))
      operand =
        operand.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));

    // 5: Mutate node
    return SyntaxFactoryUtil.CreateMethodCallWithFormedArguments(
      MutantSchemataGenerator.Namespace,
      schemaRegistry.ClassName,
      mutationGroup.SchemaName,
      SyntaxFactory.Argument(baseMutantIdLiteral),
      operand
    );
  }

  public override SyntaxNode VisitPostfixUnaryExpression(
    PostfixUnaryExpressionSyntax node)
  {
    // Pre: Do not mutate if child node is declaration pattern syntax
    // Reason: The variable scope will be limited to the mutant schemata
    // and is inaccessible to the current scope
    if (ContainsDeclarationPatternSyntax(node.Operand)) return node;

    // 1: Mutate child expression node
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
    if (mutationGroup.SchemaParameterTypes.First().StartsWith("ref"))
      operand =
        operand.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));

    // 5: Mutate node
    return SyntaxFactoryUtil.CreateMethodCallWithFormedArguments(
      MutantSchemataGenerator.Namespace,
      schemaRegistry.ClassName,
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
  /* The array size can be dynamically specified in C# in the absence of
   * specification of array elements at the same time.
   *
   * In the case where both the array size and array elements are specified,
   * the size and elements must match during compile-time, or the program
   * is considered semantically invalid and will not compile.
   */
  public override SyntaxNode VisitArrayCreationExpression(
    ArrayCreationExpressionSyntax node)
  {
    var arrayRankSpecifier = node.Type.RankSpecifiers;

    var arrayInitializer =
      node.DescendantNodes().OfType<InitializerExpressionSyntax>();

    if (!arrayRankSpecifier.Any() || !arrayInitializer.Any())
      return base.VisitArrayCreationExpression(node)!;

    var modifiedArrayInitializer =
      (InitializerExpressionSyntax)
      VisitInitializerExpression(node.Initializer!)!;

    // Both array size and elements specified; we do not modify the size
    // and other constructs, and only mutate the elements
    return node.WithInitializer(modifiedArrayInitializer);
  }

  public override SyntaxNode VisitEnumMemberDeclaration(
    EnumMemberDeclarationSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitGotoStatement(GotoStatementSyntax node)
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

  public override SyntaxNode VisitDeclarationPattern(
    DeclarationPatternSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitConstantPattern(ConstantPatternSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitVarPattern(VarPatternSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitRelationalPattern(
    RelationalPatternSyntax node)
  {
    return node;
  }

  public override SyntaxNode
    VisitRecursivePattern(RecursivePatternSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitUnaryPattern(UnaryPatternSyntax node)
  {
    return node;
  }

  public override SyntaxNode VisitBinaryPattern(BinaryPatternSyntax node)
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