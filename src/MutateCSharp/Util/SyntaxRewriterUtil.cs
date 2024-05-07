using Microsoft.CodeAnalysis;
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
}