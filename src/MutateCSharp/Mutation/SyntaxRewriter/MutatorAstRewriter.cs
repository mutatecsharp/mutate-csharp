using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Mutator;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Mutation.SchemataGenerator;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.SyntaxRewriter;

/*
 * Discover target nodes that are eligible to undergo mutation, and apply mutation.
 */
public sealed partial class MutatorAstRewriter
  : CSharpSyntaxRewriter
{
  private readonly SemanticModel _semanticModel;
  private readonly FileLevelMutantSchemaRegistry _schemaRegistry;
  private readonly FrozenDictionary<Type, IMutationOperator> _mutators;
  private readonly FrozenDictionary<Type, ImmutableArray<Type>> _mutatorGroups;
  private readonly SyntaxRewriterMode _mutationMode;

  public MutatorAstRewriter(
    Assembly sutAssembly,
    SemanticModel semanticModel,
    FileLevelMutantSchemaRegistry schemaRegistry,
    SyntaxRewriterMode mutationMode,
    bool optimise)
  {
    _semanticModel = semanticModel;
    _schemaRegistry = schemaRegistry;
    _mutationMode = mutationMode;
    var predefinedUnaryOperatorSignatures =
      semanticModel.BuildUnaryNumericOperatorMethodSignature();
    var predefinedBinaryOperatorSignatures =
      semanticModel.BuildBinaryNumericOperatorMethodSignature();

    _mutators = new Dictionary<Type, IMutationOperator>
    {
      {
        typeof(BooleanConstantReplacer),
        new BooleanConstantReplacer(sutAssembly, semanticModel, optimise)
      },
      {
        typeof(StringConstantReplacer),
        new StringConstantReplacer(sutAssembly, semanticModel, optimise)
      },
      {
        typeof(NumericConstantReplacer),
        new NumericConstantReplacer(sutAssembly, semanticModel, optimise)
      },
      {
        typeof(PrefixUnaryExprOpReplacer),
        new PrefixUnaryExprOpReplacer(sutAssembly, semanticModel,
          predefinedUnaryOperatorSignatures, optimise)
      },
      {
        typeof(PostfixUnaryExprOpReplacer),
        new PostfixUnaryExprOpReplacer(sutAssembly, semanticModel,
          predefinedUnaryOperatorSignatures, optimise)
      },
      {
        typeof(BinExprOpReplacer),
        new BinExprOpReplacer(sutAssembly, semanticModel,
          predefinedBinaryOperatorSignatures, optimise)
      },
      {
        typeof(CompoundAssignOpReplacer),
        new CompoundAssignOpReplacer(sutAssembly, semanticModel,
          predefinedBinaryOperatorSignatures, optimise)
      }
    }.ToFrozenDictionary();

    _mutatorGroups = new Dictionary<Type, ImmutableArray<Type>>
    {
      {
        typeof(LiteralExpressionSyntax),
        [
          typeof(BooleanConstantReplacer),
          typeof(StringConstantReplacer),
          typeof(NumericConstantReplacer)
        ]
      },
      {
        typeof(PrefixUnaryExpressionSyntax),
        [typeof(PrefixUnaryExprOpReplacer)]
      },
      {
        typeof(PostfixUnaryExpressionSyntax),
        [typeof(PostfixUnaryExprOpReplacer)]
      },
      {
        typeof(BinaryExpressionSyntax),
        [typeof(BinExprOpReplacer), typeof(CompoundAssignOpReplacer)]
      }
    }.ToFrozenDictionary();
  }

  // There should be at most one mutation operator that can be applied to the
  // current node, since each mutation operator apply to a disjoint set of nodes
  private IMutationOperator? LocateMutator(SyntaxNode currentNode)
  {
    // Infer mutator's type
    var baseType = currentNode.GetType();

    // Locate mutator based on type
    if (!_mutatorGroups.TryGetValue(baseType, out var mutatorCandidates))
      return null;

    var mutator =
      mutatorCandidates
        .Where(mutatorType => _mutators[mutatorType].CanBeApplied(currentNode))
        .Select(mutatorType => _mutators[mutatorType])
        .ToList();
    Trace.Assert(mutator.Count <= 1,
      $"There should be at most one qualifying mutation operator. Candidates: {string.Join(",", mutator)}");
    return mutator.FirstOrDefault();
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

  public ExpressionSyntax VisitLiteralExpressionWithRequiredReturnType(
    LiteralExpressionSyntax node,
    ITypeSymbol? requiredReturnType = default)
  {
    // 1: Apply mutation operator to obtain possible mutations
    var mutationGroup = LocateMutator(node)
      ?.CreateMutationGroup(node, requiredReturnType);
    if (mutationGroup is null) return node;

    // 2: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      _schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));

    // 3: Mutate node
    return SyntaxFactoryUtil.CreateMethodCall(
      MutantSchemataGenerator.Namespace,
      _schemaRegistry.ClassName,
      _schemaRegistry.GetUniqueSchemaName(mutationGroup, _mutationMode),
      baseMutantIdLiteral,
      node);
  }

  public override ExpressionSyntax VisitLiteralExpression(
    LiteralExpressionSyntax node)
  {
    return VisitLiteralExpressionWithRequiredReturnType(node, null);
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
      node.Left.ContainsDeclarationPatternSyntax();
    var rightContainsDeclarationSyntax =
      node.Right.ContainsDeclarationPatternSyntax();

    // 2: Do not mutate current node if any descendants of current node contain
    // declaration pattern syntax
    if (leftContainsDeclarationSyntax || rightContainsDeclarationSyntax)
      return node
        .WithLeft(leftContainsDeclarationSyntax
          ? node.Left
          : (ExpressionSyntax)Visit(node.Left))
        .WithRight(rightContainsDeclarationSyntax
          ? node.Right
          : (ExpressionSyntax)Visit(node.Right));

    // 3: Apply mutation operator to obtain possible mutations
    var mutationGroup = LocateMutator(node)?.CreateMutationGroup(node, null);
    if (mutationGroup is null)
      return node
        .WithLeft((ExpressionSyntax)Visit(node.Left))
        .WithRight((ExpressionSyntax)Visit(node.Right));

    // 4: Handle the special case where syntax node is compound
    // assignment expression and RHS is numeric literal
    BinaryExpressionSyntax? nodeWithMutatedChildren;

    if (CompoundAssignOpReplacer.SupportedOperators.ContainsKey(node.Kind())
        && node.Right.IsKind(SyntaxKind.NumericLiteralExpression))
    {
      var literalSyntax = (LiteralExpressionSyntax)node.Right;
      var rightOperandType = mutationGroup.ParameterTypeSymbols[1];
      var mutatedLiteral =
        VisitLiteralExpressionWithRequiredReturnType(literalSyntax,
          requiredReturnType: rightOperandType);
      nodeWithMutatedChildren = node.WithRight(mutatedLiteral);
    }
    else
    {
      nodeWithMutatedChildren = node
        .WithLeft((ExpressionSyntax)Visit(node.Left))
        .WithRight((ExpressionSyntax)Visit(node.Right));
    }

    // 5: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      _schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));

    // 6: Mutate node
    // Handle short-circuit operators
    var containsShortCircuitOperators =
      CodeAnalysisUtil.ShortCircuitOperators.Contains(node.Kind())
      || mutationGroup.SchemaMutantExpressions.Any(mutant =>
        CodeAnalysisUtil.ShortCircuitOperators.Contains(mutant.Operation));

    // Handle awaitable operand expressions
    var isLeftOperandAwaitable = node.Left is AwaitExpressionSyntax;
    var isRightOperandAwaitable = node.Right is AwaitExpressionSyntax;

    var leftArgument = nodeWithMutatedChildren.Left;
    var rightArgument = nodeWithMutatedChildren.Right;

    if (containsShortCircuitOperators)
    {
      var leftLambdaArgument =
        SyntaxFactory.ParenthesizedLambdaExpression(leftArgument);
      if (isLeftOperandAwaitable)
      {
        leftLambdaArgument = leftLambdaArgument.WithAsyncKeyword(
          SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
      }


      var rightLambdaArgument =
        SyntaxFactory.ParenthesizedLambdaExpression(rightArgument);
      if (isRightOperandAwaitable)
      {
        rightLambdaArgument = rightLambdaArgument.WithAsyncKeyword(
          SyntaxFactory.Token(SyntaxKind.AsyncKeyword));
      }

      leftArgument = leftLambdaArgument;
      rightArgument = rightLambdaArgument;
    }

    var returnExpr = SyntaxFactoryUtil.CreateMethodCall(
      MutantSchemataGenerator.Namespace,
      _schemaRegistry.ClassName,
      _schemaRegistry.GetUniqueSchemaName(mutationGroup, _mutationMode),
      baseMutantIdLiteral,
      leftArgument,
      rightArgument
    );

    return containsShortCircuitOperators &&
           (isLeftOperandAwaitable || isRightOperandAwaitable)
      ? SyntaxFactory.AwaitExpression(returnExpr)
      : returnExpr;
  }

  public override SyntaxNode VisitPrefixUnaryExpression(
    PrefixUnaryExpressionSyntax node)
  {
    // Pre: do not mutate if child node is a declaration pattern syntax
    // Reason: The variable scope will be limited to the mutant schemata
    // and is inaccessible to the current scope
    if (node.Operand.ContainsDeclarationPatternSyntax()) return node;
    
    /* https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/lexical-structure#6453-integer-literals
    * To permit the smallest possible int and long values to be written as
    * integer literals, the following two rules exist:
    *
      - When an Integer_Literal representing the value 2147483648 (2³¹) and no
      Integer_Type_Suffix appears as the token immediately following a unary
      minus operator token (§12.9.3), the result (of both tokens) is a constant
      of type int with the value −2147483648 (−2³¹). In all other situations,
      such an Integer_Literal is of type uint.
      - When an Integer_Literal representing the value 9223372036854775808
      (2⁶³) and no Integer_Type_Suffix or the Integer_Type_Suffix L or l appears
      as the token immediately following a unary minus operator token (§12.9.3),
      the result (of both tokens) is a constant of type long with the value
      −9223372036854775808 (−2⁶³). In all other situations, such an Integer_Literal
      is of type ulong.

      We identify such cases and treat them as a single entity.
    */

    // 1: Mutate child expression node
    // Exception: do not visit child node if node is negative numeric literal
    var nodeWithMutatedChildren = !node.IsNegativeLiteral()
      ? (PrefixUnaryExpressionSyntax)base.VisitPrefixUnaryExpression(node)!
      : node;

    // 2: Apply mutation operator to obtain possible mutations
    var mutator = node.IsNegativeLiteral()
      ? _mutators[typeof(NumericConstantReplacer)] : LocateMutator(node);

    var mutationGroup = mutator?.CreateMutationGroup(node, null);
    if (mutationGroup is null) return nodeWithMutatedChildren;

    // 3: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      _schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

    var baseMutantIdLiteral = SyntaxFactory.LiteralExpression(
      SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(baseMutantId));

    // 4: Add ref keyword to parameter if updatable
    var firstParam = mutationGroup.SchemaParameterTypes[0];
    
    // 5: Set operand as argument
    // Exception: set original node as argument if node is negative numeric literal
    var operand = SyntaxFactory.Argument(
      !node.IsNegativeLiteral() ? nodeWithMutatedChildren.Operand: node);
    if (firstParam.StartsWith("ref"))
      operand =
        operand.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));

    // 5: Mutate node
    return SyntaxFactoryUtil.CreateMethodCallWithFormedArguments(
      MutantSchemataGenerator.Namespace,
      _schemaRegistry.ClassName,
      _schemaRegistry.GetUniqueSchemaName(mutationGroup, _mutationMode),
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
    if (node.Operand.ContainsDeclarationPatternSyntax()) return node;

    // 1: Mutate child expression node
    var nodeWithMutatedChildren =
      (PostfixUnaryExpressionSyntax)base.VisitPostfixUnaryExpression(node)!;

    // 2: Apply mutation operator to obtain possible mutations
    var mutationGroup = LocateMutator(node)?.CreateMutationGroup(node, null);
    if (mutationGroup is null) return nodeWithMutatedChildren;

    // 3: Get assignment of mutant IDs for the mutation group
    var baseMutantId =
      _schemaRegistry.RegisterMutationGroupAndGetIdAssignment(mutationGroup);

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
      _schemaRegistry.ClassName,
      _schemaRegistry.GetUniqueSchemaName(mutationGroup, _mutationMode),
      SyntaxFactory.Argument(baseMutantIdLiteral),
      operand
    );
  }
}

/*
 * Widen access modifiers of declaration syntaxes - this allows the mutant
 * schemata to access private or protected types.
 */
public sealed partial class MutatorAstRewriter
{
  /* https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/access-modifiers#class-and-struct-accessibility
   * Classes and structs declared directly within a namespace
   * (aren't nested within other classes or structs) can be either public or
   * internal. internal is the default if no access modifier is specified.
   */
  public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
  {
    var nodeWithMutatedChildren =
      (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;

    // Modify access declaration
    // Set minimum accessibility to public
    var modifier =
      SyntaxRewriterUtil.SetMinimumAccessibilityToPublic(node.Modifiers);

    return nodeWithMutatedChildren.WithModifiers(modifier);
  }

  public override SyntaxNode VisitStructDeclaration(
    StructDeclarationSyntax node)
  {
    var nodeWithMutatedChildren =
      (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;

    // Modify access declaration
    // Set minimum accessibility to public
    var modifier =
      SyntaxRewriterUtil.SetMinimumAccessibilityToPublic(node.Modifiers);

    return nodeWithMutatedChildren.WithModifiers(modifier);
  }

  /*
   * Interfaces declared directly within a namespace can be public or internal
   * and, just like classes and structs, interfaces default to internal access.
   */
  public override SyntaxNode VisitInterfaceDeclaration(
    InterfaceDeclarationSyntax node)
  {
    var nodeWithMutatedChildren =
      (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!;

    // Modify access declaration
    // Set minimum accessibility to internal
    var modifier =
      SyntaxRewriterUtil.SetMinimumAccessibilityToPublic(node.Modifiers);

    return nodeWithMutatedChildren.WithModifiers(modifier);
  }

  public override SyntaxNode VisitEnumDeclaration(
    EnumDeclarationSyntax node)
  {
    var nodeWithMutatedChildren =
      (EnumDeclarationSyntax)base.VisitEnumDeclaration(node)!;

    // Modify access declaration
    // Set minimum accessibility to internal
    var modifier =
      SyntaxRewriterUtil.SetMinimumAccessibilityToPublic(node.Modifiers);

    return nodeWithMutatedChildren.WithModifiers(modifier);
  }

  public override SyntaxNode VisitRecordDeclaration(
    RecordDeclarationSyntax node)
  {
    var nodeWithMutatedChildren =
      (RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!;

    // Modify access declaration
    // Set minimum accessibility to internal
    var modifier =
      SyntaxRewriterUtil.SetMinimumAccessibilityToPublic(node.Modifiers);

    return nodeWithMutatedChildren.WithModifiers(modifier);
  }
}

/*
 * Special cases to handle programming constructs that when mutated causes
 * compilation errors due to safety guards built into the language semantics.
 *
 * There are two situations in which it is a compile-time error for the
 * end point of a statement to be reachable. We handle them here.
 */
public sealed partial class MutatorAstRewriter
{
  /*
   * If the end point of the statement list of a switch section is reachable,
   * a compile-time error occurs. This is known as the “no fall through” rule.
   * When mutating the body of switch case statements that allows the code to
   * reach the end point of a switch section, we insert a break statement to
   * ensure no fall through.
   */
  public override SyntaxNode VisitSwitchSection(SwitchSectionSyntax node)
  {
    // Add break statement at the end unconditionally
    return node.WithStatements(
      node.Statements.Add(SyntaxFactory.BreakStatement()));
  }

  /*
   * It is a compile-time error for the end point of the block of a function
   * member or an anonymous function that computes a value to be reachable.
   * If this error occurs, it typically is an indication that a return
   * statement is missing.
   */
  public override SyntaxNode VisitMethodDeclaration(
    MethodDeclarationSyntax node)
  {
    var nodeWithMutatedChildren =
      (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;

    // Trivial case: method has void/Task return type / empty body
    // Do not insert return statements at the end of enumerable methods
    if (node.Body is null || nodeWithMutatedChildren.Body is null ||
        _semanticModel.GetDeclaredSymbol(node) is not { } methodSymbol ||
        _semanticModel.IsTypeVoid(methodSymbol.ReturnType))
    {
      return nodeWithMutatedChildren;
    }

    // Add yield break statement for iterators
    if (CodeAnalysisUtil.IsIteratorBlock(node.Body))
    {
      return nodeWithMutatedChildren.WithBody(
        SyntaxRewriterUtil.InsertDefaultYieldStatement(nodeWithMutatedChildren
          .Body));
    }

    // Add return statement for regular methods
    return nodeWithMutatedChildren.WithBody(
      SyntaxRewriterUtil.InsertDefaultReturnStatement(nodeWithMutatedChildren
        .Body));
  }

  public override SyntaxNode VisitLocalFunctionStatement(
    LocalFunctionStatementSyntax node)
  {
    var nodeWithMutatedChildren =
      (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;

    // Trivial case: method has void/Task return type / empty body
    // Do not insert return statements at the end of enumerable methods
    if (node.Body is null || nodeWithMutatedChildren.Body is null ||
        _semanticModel.GetDeclaredSymbol(node) is not { } methodSymbol ||
        _semanticModel.IsTypeVoid(methodSymbol.ReturnType))
    {
      return nodeWithMutatedChildren;
    }

    // Add yield break statement for iterators
    if (CodeAnalysisUtil.IsIteratorBlock(node.Body))
    {
      return nodeWithMutatedChildren.WithBody(
        SyntaxRewriterUtil.InsertDefaultYieldStatement(nodeWithMutatedChildren
          .Body));
    }

    // Add return statement for regular methods
    return nodeWithMutatedChildren.WithBody(
      SyntaxRewriterUtil.InsertDefaultReturnStatement(nodeWithMutatedChildren
        .Body));
  }

  public override SyntaxNode VisitPropertyDeclaration(
    PropertyDeclarationSyntax node)
  {
    var nodeWithMutatedChildren =
      (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;
    if (nodeWithMutatedChildren.AccessorList is null ||
        _semanticModel.GetDeclaredSymbol(node) is not { } propertySymbol ||
        _semanticModel.IsTypeVoid(propertySymbol.Type))
      return nodeWithMutatedChildren;

    var modifiedAccessorList = new List<AccessorDeclarationSyntax>();

    foreach (var accessor in nodeWithMutatedChildren.AccessorList.Accessors)
    {
      // Getters must have a non-void return type
      if (!accessor.IsKind(SyntaxKind.GetAccessorDeclaration) ||
          accessor.Body is null)
      {
        modifiedAccessorList.Add(accessor);
        continue;
      }

      if (CodeAnalysisUtil.IsIteratorBlock(accessor.Body))
      {
        // Add yield break statement if yield statements are not present at
        // the endpoint
        var modifiedAccessor = accessor.WithBody(
          SyntaxRewriterUtil.InsertDefaultYieldStatement(accessor.Body));
        modifiedAccessorList.Add(modifiedAccessor);
      }
      else
      {
        // Insert return statement if it does not exist at the endpoint
        var modifiedAccessor = accessor.WithBody(
          SyntaxRewriterUtil.InsertDefaultReturnStatement(accessor.Body));
        modifiedAccessorList.Add(modifiedAccessor);
      }
    }

    // Add return statement
    return nodeWithMutatedChildren.WithAccessorList(
      SyntaxFactory.AccessorList(
        SyntaxFactory.List(modifiedAccessorList)));
  }

  public override SyntaxNode VisitParenthesizedLambdaExpression(
    ParenthesizedLambdaExpressionSyntax node)
  {
    var nodeWithMutatedChildren =
      (ParenthesizedLambdaExpressionSyntax)base
        .VisitParenthesizedLambdaExpression(node)!;

    // Trivial case: method has void return type / empty body
    if (nodeWithMutatedChildren.Block is null ||
        _semanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol
          methodSymbol ||
        _semanticModel.IsTypeVoid(methodSymbol.ReturnType))
      return nodeWithMutatedChildren;

    // Add return statement
    return nodeWithMutatedChildren.WithBody(
      SyntaxRewriterUtil.InsertDefaultReturnStatement(nodeWithMutatedChildren
        .Block));
  }

  public override SyntaxNode VisitSimpleLambdaExpression(
    SimpleLambdaExpressionSyntax node)
  {
    var nodeWithMutatedChildren =
      (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node)!;

    // Trivial case: method has void return type / empty body
    if (nodeWithMutatedChildren.Block is null ||
        _semanticModel.GetSymbolInfo(node).Symbol is not IMethodSymbol
          methodSymbol ||
        _semanticModel.IsTypeVoid(methodSymbol.ReturnType))
      return nodeWithMutatedChildren;

    // Add return statement
    return nodeWithMutatedChildren.WithBody(
      SyntaxRewriterUtil.InsertDefaultReturnStatement(nodeWithMutatedChildren
        .Block));
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

  /*
   * https://learn.microsoft.com/en-us/dotnet/framework/debug-trace-profile/code-contracts
   * "Code contracts provide a way to specify preconditions, postconditions,
   * and object invariants in .NET Framework code."
   *
   * We avoid modifying the preconditions, postconditions, and object invariants
   * for Code Contracts as these are used by the static checker to verify the
   * predicates at compile time, and would cause the code to fail compiling if
   * modified.
   */
  public override SyntaxNode VisitInvocationExpression(
    InvocationExpressionSyntax node)
  {
    return node.InvokesCodeContractMethods()
      ? node
      : base.VisitInvocationExpression(node)!;
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