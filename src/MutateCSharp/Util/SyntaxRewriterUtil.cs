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
}