using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serilog;

namespace MutateCSharp.Util;

/*
 * Helper class containing common logic with logging statements.
 */
public static class SyntaxRewriterUtil
{
  public static bool IsTypeResolvableLogged(
    ref readonly SemanticModel model, ref readonly SyntaxNode node)
  {
    if (node is null) return false;
    var typeSymbol = model.ResolveTypeSymbol(node);
    var convertedTypeSymbol = model.ResolveConvertedTypeSymbol(node);
    if (typeSymbol is not null && convertedTypeSymbol is not null) return true;
    Log.Debug("The type for {Expression} cannot be resolved (line {Line}). Ignoring...", 
      node.ToString(), node.GetLocation().GetLineSpan().StartLinePosition.Line);
    return false;
  }
  
  // Pre: type is resolvable
  public static bool ContainsGenericTypeParameterLogged(ref readonly ITypeSymbol typeSymbol)
  {
    var underlyingTypeSymbol = typeSymbol.GetNullableUnderlyingType()!;
    if (!underlyingTypeSymbol.ContainsGenericTypeParameter()) return false;
    
    Log.Debug("{Type} is a generic type parameter. Ignoring...", 
      underlyingTypeSymbol.ToDisplayString());
    return true;
  }

  public static bool IsSymbolResolvableLogged(ref readonly SemanticModel model,
    SyntaxNode node)
  {
    var symbol = model.GetSymbolInfo(node).Symbol;
    if (symbol is not null) return true;
    Log.Debug("The symbol for {Expression} cannot be resolved (line {Line}). Ignoring...", 
      node.ToString(), node.GetLocation().GetLineSpan().StartLinePosition.Line);
    return false;
  }
  
  /*
   * Case 1:
   * Predicates can specify `is` expressions with patterns that declare variables.
   * This is followed by a variable declaration which has to be visible in the current scope.
   *
   * Case 2:
   * Methods can specify parameters with `out` modifier, which allows a programmer
   * to pass an argument to a method by reference. This is followed by a variable
   * declaration which has to be visible in the current scope.
   *
   * In all cases, wrapping it in
   * a lambda causes the declared variable to be only visible within the scope
   * of the lambda, causing code that refers to the declared variable to be
   * semantically invalid.
   *
   * We address this by not mutating a node if any of its descendant has a
   * parameter with declaration syntax.
   */
  public static bool ContainsDeclarationPatternSyntax(this SyntaxNode node)
  {
    return node.DescendantNodes().OfType<DeclarationPatternSyntax>().Any()
           || node.DescendantNodes().OfType<VarPatternSyntax>().Any()
           || node.DescendantNodes().OfType<ArgumentSyntax>()
             .Any(arg => arg.Expression is DeclarationExpressionSyntax
             || arg.DescendantTokens().Any(token => token.Kind() is SyntaxKind.OutKeyword))
           || node.DescendantNodes().OfType<SingleVariableDesignationSyntax>()
             .Any();
  }

  public static INamedTypeSymbol ConstructNullableValueTypeSymbol(
    this SemanticModel model, ITypeSymbol typeSymbol)
  {
    var nullableType = model.Compilation.GetTypeByMetadataName("System.Nullable`1")!;
    return nullableType.Construct(typeSymbol);
  }

  /*
   * Insert "return default;" if the last statement is not a return statement
   * as a catch-all approach.
   */
  public static BlockSyntax InsertDefaultReturnStatement(BlockSyntax block)
  {
    if (block.Statements.LastOrDefault() is { } lastStatement &&
        !lastStatement.IsKind(SyntaxKind.ReturnStatement))
    {
      var returnDefaultStatement = SyntaxFactory.ReturnStatement(
        SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression));

      return block.WithStatements(block.Statements.Add(returnDefaultStatement));
    }

    return block;
  }
  
  /*
   * Insert "yield break;" if the last statement is not a yield statement
   * as a catch-all approach.
   */
  public static BlockSyntax InsertDefaultYieldStatement(BlockSyntax block)
  {
    if (block.Statements.LastOrDefault() is { } lastStatement &&
        !lastStatement.IsKind(SyntaxKind.YieldBreakStatement) &&
        !lastStatement.IsKind(SyntaxKind.YieldReturnStatement))
    {
      var yieldBreakStatement = SyntaxFactory.YieldStatement(
        SyntaxKind.YieldBreakStatement);

      return block.WithStatements(block.Statements.Add(yieldBreakStatement));
    }

    return block;
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
  public static bool InvokesCodeContractMethods(
    this InvocationExpressionSyntax node)
  {
    return node.Expression is MemberAccessExpressionSyntax 
    { Expression: IdentifierNameSyntax { Identifier.Text: "Contract" },
      Name.Identifier.Text: 
      "Requires" 
      or "Ensures" 
      or "Assert" 
      or "Assume" 
      or "Invariant" 
      or "ForAll" 
      or "Exists" 
      or "OldValue" 
      or "ValueAtReturn" 
      or "Result"
    };
  }

  /* Nested namespaces can be declared except in file scoped namespace declarations.
   * https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/namespace
   * File scoped namespaces can't include additional namespace declarations.
   * You cannot declare a nested namespace or a second file-scoped namespace.
   */
  public static string GetNamespaceName(this CompilationUnitSyntax root)
  {
    var scopedNamespace = root.DescendantNodes()
      .OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
    if (scopedNamespace is not null) return scopedNamespace.Name.ToString();
    var fileScopedNamespace = root.DescendantNodes()
      .OfType<FileScopedNamespaceDeclarationSyntax>().FirstOrDefault();
    return fileScopedNamespace?.Name.ToString() ?? string.Empty;
  }

  public static SyntaxTokenList SetMinimumAccessibilityToPublic(
    SyntaxTokenList modifiers)
  {
    var result = modifiers.Where(modifier =>
      modifier.Kind() is not (
        SyntaxKind.PrivateKeyword or
        SyntaxKind.ProtectedKeyword or
        SyntaxKind.InternalKeyword or
        SyntaxKind.FileKeyword)).ToList();

    if (!result.Any(modifier =>
          modifier.Kind() is SyntaxKind.PublicKeyword))
    {
      result = result.Prepend(SyntaxFactory.Token(SyntaxKind.PublicKeyword)
          .WithTrailingTrivia(SyntaxFactory.Space))
        .ToList();
    }

    return SyntaxFactory.TokenList(result);
  }
}