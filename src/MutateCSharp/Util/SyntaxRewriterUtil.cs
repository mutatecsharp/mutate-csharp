using Microsoft.CodeAnalysis;
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
    var typeSymbol = model.GetTypeInfo(node).ResolveType();
    if (typeSymbol is not null) return true;
    Log.Debug("The type for {Type} cannot be resolved (line {Line}). Ignoring...", 
      node.ToString(), node.GetLocation().GetLineSpan().StartLinePosition.Line);
    return false;
  }
  
  // Pre: type is resolvable
  public static bool ContainsGenericTypeParameterLogged(ref readonly ITypeSymbol typeSymbol)
  {
    var underlyingTypeSymbol = typeSymbol.GetNullableUnderlyingType()!;
    if (!underlyingTypeSymbol.ContainsGenericTypeParameter()) return false;
    
    Log.Debug("{SyntaxNodeType} is a generic type parameter. Ignoring...", 
      underlyingTypeSymbol.ToDisplayString());
    return true;
  }
  
  /* Case 1:
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
             .Any(arg => arg.Expression is DeclarationExpressionSyntax)
           || node.DescendantNodes().OfType<SingleVariableDesignationSyntax>()
             .Any();
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